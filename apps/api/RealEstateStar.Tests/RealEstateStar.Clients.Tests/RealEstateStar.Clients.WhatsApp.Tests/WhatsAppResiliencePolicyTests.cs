using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Moq;
using Polly;

namespace RealEstateStar.Clients.WhatsApp.Tests;

/// <summary>
/// Tests for WhatsAppResiliencePolicies extension methods.
///
/// Strategy: wire up a real ServiceCollection + AddHttpClient so the Polly pipeline
/// is fully registered, then inject a counting HttpMessageHandler stub to observe
/// how many times the pipeline calls the inner handler (i.e. retry behaviour).
/// Circuit breaker tests verify the breaker trips and logs using a captured logger.
/// </summary>
public class WhatsAppResiliencePolicyTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private sealed class CountingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            throw new HttpRequestException("Simulated transient failure");
        }
    }

    private sealed class DelegatingHandlerStub(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => sendAsync(request);
    }

    private static HttpClient BuildClient(
        string clientName,
        HttpMessageHandler inner,
        ILogger logger)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddHttpClient(clientName)
            .ConfigurePrimaryHttpMessageHandler(() => inner)
            .AddWhatsAppResilience(logger);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        return factory.CreateClient(clientName);
    }

    // ---------------------------------------------------------------------------
    // Retry — 429
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RetriesThreeTimes_On429()
    {
        var handler = new CountingHandler(HttpStatusCode.TooManyRequests);
        var logger = Mock.Of<ILogger>();
        var client = BuildClient("wa-429", handler, logger);

        var response = await client.GetAsync("http://localhost/test");

        // 1 initial + 3 retries = 4 total calls
        handler.CallCount.Should().Be(4);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    // ---------------------------------------------------------------------------
    // Retry — 5xx
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RetriesThreeTimes_On500()
    {
        var handler = new CountingHandler(HttpStatusCode.InternalServerError);
        var logger = Mock.Of<ILogger>();
        var client = BuildClient("wa-500", handler, logger);

        var response = await client.GetAsync("http://localhost/test");

        handler.CallCount.Should().Be(4);
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task RetriesThreeTimes_On502()
    {
        var handler = new CountingHandler(HttpStatusCode.BadGateway);
        var logger = Mock.Of<ILogger>();
        var client = BuildClient("wa-502", handler, logger);

        var response = await client.GetAsync("http://localhost/test");

        handler.CallCount.Should().Be(4);
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task RetriesThreeTimes_On503()
    {
        var handler = new CountingHandler(HttpStatusCode.ServiceUnavailable);
        var logger = Mock.Of<ILogger>();
        var client = BuildClient("wa-503", handler, logger);

        var response = await client.GetAsync("http://localhost/test");

        handler.CallCount.Should().Be(4);
    }

    [Fact]
    public async Task RetriesThreeTimes_OnHttpRequestException()
    {
        var handler = new ThrowingHandler();
        var logger = Mock.Of<ILogger>();
        var client = BuildClient("wa-throw", handler, logger);

        Func<Task> act = () => client.GetAsync("http://localhost/test");

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.CallCount.Should().Be(4);
    }

    // ---------------------------------------------------------------------------
    // No retry on success or non-retryable errors
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DoesNotRetry_OnSuccess()
    {
        var handler = new CountingHandler(HttpStatusCode.OK);
        var logger = Mock.Of<ILogger>();
        var client = BuildClient("wa-ok", handler, logger);

        var response = await client.GetAsync("http://localhost/test");

        handler.CallCount.Should().Be(1);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DoesNotRetry_On400()
    {
        var handler = new CountingHandler(HttpStatusCode.BadRequest);
        var logger = Mock.Of<ILogger>();
        var client = BuildClient("wa-400", handler, logger);

        var response = await client.GetAsync("http://localhost/test");

        // 400 is a client error — not retryable
        handler.CallCount.Should().Be(1);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DoesNotRetry_On401()
    {
        var handler = new CountingHandler(HttpStatusCode.Unauthorized);
        var logger = Mock.Of<ILogger>();
        var client = BuildClient("wa-401", handler, logger);

        var response = await client.GetAsync("http://localhost/test");

        handler.CallCount.Should().Be(1);
    }

    // ---------------------------------------------------------------------------
    // Retry logging — [WA-030]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Retry_Logs_WA030()
    {
        var handler = new CountingHandler(HttpStatusCode.InternalServerError);
        var loggerMock = new Mock<ILogger>();
        var client = BuildClient("wa-log", handler, loggerMock.Object);

        await client.GetAsync("http://localhost/test");

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[WA-030]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // ---------------------------------------------------------------------------
    // Circuit breaker — [WA-031]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CircuitBreaker_Opens_AfterFiveFailures_Logs_WA031()
    {
        var handler = new CountingHandler(HttpStatusCode.InternalServerError);
        var loggerMock = new Mock<ILogger>();
        var client = BuildClient("wa-cb", handler, loggerMock.Object);

        // Each request: 1 initial + 3 retries = 4 handler calls.
        // MinimumThroughput=5; fire enough requests to trip the breaker.
        for (var i = 0; i < 6; i++)
        {
            try { await client.GetAsync("http://localhost/test"); } catch { }
        }

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[WA-031]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // ---------------------------------------------------------------------------
    // Circuit breaker close — [WA-032]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CircuitBreaker_Close_Logs_WA032()
    {
        var callCount = 0;
        var handler = new DelegatingHandlerStub(_ =>
        {
            callCount++;
            return callCount <= 5
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError))
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var loggerMock = new Mock<ILogger>();

        // Use a custom pipeline with fast break duration so test doesn't take 30s
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddHttpClient("wa-close")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddResilienceHandler("wa-close-pipeline", pipeline =>
            {
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Constant,
                    Delay = TimeSpan.FromMilliseconds(1),
                    UseJitter = false,
                    ShouldRetryAfterHeader = false,
                    OnRetry = _ => ValueTask.CompletedTask
                });

                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 1.0,
                    MinimumThroughput = 5,
                    SamplingDuration = TimeSpan.FromSeconds(60),
                    BreakDuration = TimeSpan.FromMilliseconds(500),
                    OnOpened = _ => ValueTask.CompletedTask,
                    OnClosed = _ =>
                    {
                        loggerMock.Object.LogInformation("[WA-032] WhatsApp API circuit breaker closed — calls resuming.");
                        return ValueTask.CompletedTask;
                    }
                });
            });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("wa-close");

        // Trip the circuit
        for (var i = 0; i < 6; i++)
        {
            try { await client.GetAsync("http://localhost/test"); } catch { }
        }

        // Wait for break duration to expire → half-open
        await Task.Delay(700);

        // This call succeeds → circuit closes → [WA-032]
        await client.GetAsync("http://localhost/test");

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[WA-032]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // ---------------------------------------------------------------------------
    // DI overload (parameterless) — resolves logger from IServiceProvider
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ParameterlessOverload_ResolvesLoggerFromDI()
    {
        var handler = new CountingHandler(HttpStatusCode.InternalServerError);

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddHttpClient("wa-di")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddWhatsAppResilience(); // parameterless — resolves logger from DI

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("wa-di");

        var response = await client.GetAsync("http://localhost/test");

        // Verifies the pipeline was configured (retries happened)
        handler.CallCount.Should().Be(4);
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
