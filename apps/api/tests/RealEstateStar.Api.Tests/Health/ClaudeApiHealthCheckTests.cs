using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using RealEstateStar.Api.Health;

namespace RealEstateStar.Api.Tests.Health;

public class ClaudeApiHealthCheckTests
{
    private static IConfiguration CreateConfig(string apiKey = "test-key") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Anthropic:ApiKey"] = apiKey })
            .Build();
    [Fact]
    public async Task CheckHealthAsync_HealthyWhenApiReturns200()
    {
        var handler = new TestHttpMessageHandler(HttpStatusCode.OK);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var check = new ClaudeApiHealthCheck(factory.Object, CreateConfig());

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("reachable");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task CheckHealthAsync_DegradedWhenApiReturnsErrorStatus(HttpStatusCode statusCode)
    {
        var handler = new TestHttpMessageHandler(statusCode);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var check = new ClaudeApiHealthCheck(factory.Object, CreateConfig());

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain(statusCode.ToString());
    }

    [Fact]
    public async Task CheckHealthAsync_UnhealthyWhenExceptionThrown()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("DNS failure"));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var check = new ClaudeApiHealthCheck(factory.Object, CreateConfig());

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("unreachable");
        result.Exception.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task CheckHealthAsync_UnhealthyWhenTimeoutOccurs()
    {
        var handler = new ThrowingHttpMessageHandler(new TaskCanceledException("Timeout"));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var check = new ClaudeApiHealthCheck(factory.Object, CreateConfig());

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    private class TestHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw exception;
        }
    }
}
