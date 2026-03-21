using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Moq;
using Polly;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Tests.Infrastructure;

/// <summary>
/// Tests for PollyPolicies resilience extension methods.
///
/// Strategy: wire up a real ServiceCollection + AddHttpClient so the Polly pipeline
/// is fully registered, then inject a counting HttpMessageHandler stub to observe
/// how many times the pipeline calls the inner handler (i.e. retry behaviour).
/// Circuit breaker tests verify the breaker trips and logs using a captured logger.
/// </summary>
public class PollyPolicyTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// HttpMessageHandler that always returns the given status code and counts calls.
    /// </summary>
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

    /// <summary>
    /// HttpMessageHandler that throws HttpRequestException every time (transient error).
    /// </summary>
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

    /// <summary>
    /// Builds an HttpClient whose Polly pipeline is configured via the given <paramref name="configure"/>
    /// delegate and whose inner handler is replaced by <paramref name="inner"/>.
    /// </summary>
    private static HttpClient BuildClient(
        string clientName,
        HttpMessageHandler inner,
        ILogger logger,
        Action<IHttpClientBuilder, ILogger> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var clientBuilder = services.AddHttpClient(clientName)
            .ConfigurePrimaryHttpMessageHandler(() => inner);

        configure(clientBuilder, logger);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        return factory.CreateClient(clientName);
    }

    // ---------------------------------------------------------------------------
    // Claude API — Retry
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ClaudeApi_RetriesThreeTimes_OnTransientError()
    {
        // Arrange — 500 is treated as transient by HttpRetryStrategyOptions
        var handler = new CountingHandler(HttpStatusCode.InternalServerError);
        var logger = Mock.Of<ILogger>();

        var client = BuildClient("claude", handler, logger,
            (b, l) => b.AddClaudeApiResilience(l));

        // Act
        var response = await client.GetAsync("http://localhost/test");

        // Assert: 1 initial + 3 retries = 4 total calls
        handler.CallCount.Should().Be(4);
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ClaudeApi_DoesNotRetry_OnSuccess()
    {
        var handler = new CountingHandler(HttpStatusCode.OK);
        var logger = Mock.Of<ILogger>();

        var client = BuildClient("claude-ok", handler, logger,
            (b, l) => b.AddClaudeApiResilience(l));

        var response = await client.GetAsync("http://localhost/test");

        handler.CallCount.Should().Be(1);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------------
    // Claude API — Retry log [CLAUDE-001]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ClaudeApi_Retry_Logs_CLAUDE001()
    {
        var handler = new CountingHandler(HttpStatusCode.InternalServerError);
        var loggerMock = new Mock<ILogger>();

        var client = BuildClient("claude-log", handler, loggerMock.Object,
            (b, l) => b.AddClaudeApiResilience(l));

        await client.GetAsync("http://localhost/test");

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[CLAUDE-001]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // ---------------------------------------------------------------------------
    // Claude API — Circuit breaker
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ClaudeApi_CircuitBreaker_Opens_AfterFiveFailures_Logs_CLAUDE002()
    {
        var handler = new CountingHandler(HttpStatusCode.InternalServerError);
        var loggerMock = new Mock<ILogger>();

        var client = BuildClient("claude-cb", handler, loggerMock.Object,
            (b, l) => b.AddClaudeApiResilience(l));

        for (var i = 0; i < 6; i++)
        {
            try { await client.GetAsync("http://localhost/test"); } catch { /* breaker throws */ }
        }

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[CLAUDE-002]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ClaudeApi_CircuitBreaker_Close_Logs_CLAUDE003()
    {
        var callCount = 0;
        // Handler that fails first N times, then succeeds — allows the circuit to close (half-open → closed)
        var handler = new DelegatingHandlerStub(req =>
        {
            callCount++;
            // Calls 1-5 fail to trip the circuit breaker (MinimumThroughput = 5).
            // Call 6+ succeeds so the half-open probe succeeds and the circuit closes.
            if (callCount <= 5)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var loggerMock = new Mock<ILogger>();

        // Configure a pipeline with the minimum allowed BreakDuration (500ms) so the
        // circuit recovers in ~500ms during the test.  Retry delay is set to 1ms to
        // keep the test fast while satisfying the minimum-value constraint.
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddHttpClient("claude-close")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddResilienceHandler("claude-close-pipeline", pipeline =>
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
                    BreakDuration = TimeSpan.FromMilliseconds(500),  // minimum allowed
                    OnOpened = _ => ValueTask.CompletedTask,
                    OnClosed = _ =>
                    {
                        loggerMock.Object.LogInformation("[CLAUDE-003] Claude API circuit breaker closed.");
                        return ValueTask.CompletedTask;
                    }
                });
            });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("claude-close");

        // Trip the circuit (each request: 1 initial + 3 retries = 4 handler calls)
        for (var i = 0; i < 6; i++)
        {
            try { await client.GetAsync("http://localhost/test"); } catch { }
        }

        // Wait for the break duration to expire so the circuit transitions to half-open
        await Task.Delay(700);

        // This call should succeed (handler returns 200 after callCount > 25) → closed → [CLAUDE-003]
        await client.GetAsync("http://localhost/test");

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[CLAUDE-003]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // ---------------------------------------------------------------------------
    // ScraperAPI — Retry
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ScraperApi_RetriesTwice_OnTransientError()
    {
        var handler = new CountingHandler(HttpStatusCode.InternalServerError);
        var logger = Mock.Of<ILogger>();

        var client = BuildClient("scraper", handler, logger,
            (b, l) => b.AddScraperApiResilience(l));

        await client.GetAsync("http://localhost/test");

        // 1 initial + 2 retries = 3 total
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task ScraperApi_CircuitBreaker_Opens_AfterTenFailures_Logs_SCRAPER002()
    {
        var handler = new CountingHandler(HttpStatusCode.InternalServerError);
        var loggerMock = new Mock<ILogger>();

        var client = BuildClient("scraper-cb", handler, loggerMock.Object,
            (b, l) => b.AddScraperApiResilience(l));

        for (var i = 0; i < 12; i++)
        {
            try { await client.GetAsync("http://localhost/test"); } catch { }
        }

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[SCRAPER-002]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // ---------------------------------------------------------------------------
    // Google Chat — Retry
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GoogleChat_RetriesOnce_OnTransientError()
    {
        var handler = new CountingHandler(HttpStatusCode.InternalServerError);
        var logger = Mock.Of<ILogger>();

        var client = BuildClient("google", handler, logger,
            (b, l) => b.AddGoogleChatResilience(l));

        await client.GetAsync("http://localhost/test");

        // 1 initial + 1 retry = 2 total
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task GoogleChat_DoesNotRetry_OnSuccess()
    {
        var handler = new CountingHandler(HttpStatusCode.OK);
        var logger = Mock.Of<ILogger>();

        var client = BuildClient("google-ok", handler, logger,
            (b, l) => b.AddGoogleChatResilience(l));

        var response = await client.GetAsync("http://localhost/test");

        handler.CallCount.Should().Be(1);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------------
    // Helper: DelegatingHandlerStub
    // ---------------------------------------------------------------------------

    private sealed class DelegatingHandlerStub(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => sendAsync(request);
    }
}
