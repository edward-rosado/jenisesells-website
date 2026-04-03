using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Health;

namespace RealEstateStar.Api.Tests.Health;

public class DurableFunctionsHealthCheckTests
{
    private static IConfiguration CreateConfig(string? healthUrl = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureFunctions:HealthUrl"] = healthUrl
            })
            .Build();

    private static HealthCheckContext MakeContext(HealthStatus failureStatus = HealthStatus.Unhealthy) =>
        new()
        {
            Registration = new HealthCheckRegistration(
                "durable_functions",
                new Mock<IHealthCheck>().Object,
                failureStatus,
                null)
        };

    private DurableFunctionsHealthCheck CreateCheck(HttpStatusCode? statusCode = null, string? healthUrl = "https://real-estate-star-functions.azurewebsites.net/health") =>
        new(
            CreateHttpClientFactory(statusCode),
            CreateConfig(healthUrl),
            NullLogger<DurableFunctionsHealthCheck>.Instance);

    private static IHttpClientFactory CreateHttpClientFactory(HttpStatusCode? statusCode)
    {
        var factory = new Mock<IHttpClientFactory>();
        if (statusCode.HasValue)
        {
            factory.Setup(f => f.CreateClient(DurableFunctionsHealthCheck.HttpClientName))
                .Returns(new HttpClient(new TestHttpMessageHandler(statusCode.Value)));
        }
        return factory.Object;
    }

    [Fact]
    public async Task ReturnsHealthy_WhenNotConfigured()
    {
        var check = CreateCheck(healthUrl: null);

        var result = await check.CheckHealthAsync(MakeContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("not configured");
        result.Data["configured"].Should().Be(false);
    }

    [Fact]
    public async Task ReturnsHealthy_WhenFunctionsHostReturns200()
    {
        var check = CreateCheck(HttpStatusCode.OK);

        var result = await check.CheckHealthAsync(MakeContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("healthy");
        result.Data["configured"].Should().Be(true);
        result.Data["statusCode"].Should().Be(200);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task ReturnsUnhealthy_WhenFunctionsHostReturnsErrorStatus(HttpStatusCode statusCode)
    {
        var check = CreateCheck(statusCode);

        var result = await check.CheckHealthAsync(MakeContext(HealthStatus.Unhealthy), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data["configured"].Should().Be(true);
        result.Data["statusCode"].Should().Be((int)statusCode);
    }

    [Fact]
    public async Task ReturnsUnhealthy_WhenHttpRequestThrows()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(DurableFunctionsHealthCheck.HttpClientName))
            .Returns(new HttpClient(new ThrowingHttpMessageHandler()));

        var check = new DurableFunctionsHealthCheck(
            factory.Object,
            CreateConfig("https://real-estate-star-functions.azurewebsites.net/health"),
            NullLogger<DurableFunctionsHealthCheck>.Instance);

        var result = await check.CheckHealthAsync(MakeContext(HealthStatus.Unhealthy), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("unreachable");
        result.Exception.Should().NotBeNull();
        result.Data["configured"].Should().Be(true);
        result.Data["url"].Should().Be("https://real-estate-star-functions.azurewebsites.net/health");
    }

    [Fact]
    public async Task IncludesUrlInData_WhenConfigured()
    {
        var check = CreateCheck(HttpStatusCode.OK, "https://my-functions.azurewebsites.net/health");

        var result = await check.CheckHealthAsync(MakeContext(), CancellationToken.None);

        result.Data["url"].Should().Be("https://my-functions.azurewebsites.net/health");
    }

    [Fact]
    public async Task PropagatesCancellation_WhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(DurableFunctionsHealthCheck.HttpClientName))
            .Returns(new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK)));

        var check = new DurableFunctionsHealthCheck(
            factory.Object,
            CreateConfig("https://real-estate-star-functions.azurewebsites.net/health"),
            NullLogger<DurableFunctionsHealthCheck>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => check.CheckHealthAsync(MakeContext(), cts.Token));
    }

    private sealed class TestHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(
                new HttpRequestException("Connection refused"));
    }
}
