using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Api.Health;

namespace RealEstateStar.Api.Tests.Health;

public class OtlpExportHealthCheckTests
{
    private static IConfiguration CreateConfig(string? endpoint = null, string? headers = null)
    {
        var dict = new Dictionary<string, string?>();
        if (endpoint is not null) dict["Otel:Endpoint"] = endpoint;
        if (headers is not null) dict["Otel:Headers"] = headers;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static OtlpExportHealthCheck CreateCheck(
        IConfiguration config,
        HttpStatusCode? responseStatus = null,
        Exception? throwException = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        HttpMessageHandler handler = throwException is not null
            ? new ThrowingHandler(throwException)
            : new TestHandler(responseStatus ?? HttpStatusCode.OK);
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var logger = new Mock<ILogger<OtlpExportHealthCheck>>();
        return new OtlpExportHealthCheck(config, factory.Object, logger.Object);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://localhost:4317")]
    [InlineData("http://127.0.0.1:4317")]
    public async Task DegradedWhenEndpointIsLocalOrMissing(string? endpoint)
    {
        var check = CreateCheck(CreateConfig(endpoint, "Authorization=Basic abc"));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("not configured for production");
    }

    [Fact]
    public async Task UnhealthyWhenHeadersMissing()
    {
        var check = CreateCheck(CreateConfig(
            "https://otlp-gateway.grafana.net/otlp", headers: ""));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("headers not configured");
    }

    [Fact]
    public async Task UnhealthyWhenHeadersNull()
    {
        var check = CreateCheck(CreateConfig(
            "https://otlp-gateway.grafana.net/otlp", headers: null));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("headers not configured");
    }

    [Fact]
    public async Task HealthyWhenEndpointReturns200()
    {
        var check = CreateCheck(
            CreateConfig("https://otlp-gateway.grafana.net/otlp", "Authorization=Basic abc"),
            HttpStatusCode.OK);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("reachable");
        result.Data["statusCode"].Should().Be(200);
    }

    [Fact]
    public async Task HealthyWhenEndpointReturns204()
    {
        var check = CreateCheck(
            CreateConfig("https://otlp-gateway.grafana.net/otlp", "Authorization=Basic abc"),
            HttpStatusCode.NoContent);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["statusCode"].Should().Be(204);
    }

    [Fact]
    public async Task HealthyWhenEndpointReturns400_EmptyPayloadExpected()
    {
        var check = CreateCheck(
            CreateConfig("https://otlp-gateway.grafana.net/otlp", "Authorization=Basic abc"),
            HttpStatusCode.BadRequest);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("empty probe payload");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task UnhealthyWhenAuthFails(HttpStatusCode status)
    {
        var check = CreateCheck(
            CreateConfig("https://otlp-gateway.grafana.net/otlp", "Authorization=Basic bad"),
            status);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("OTEL-HC-001");
        result.Description.Should().Contain("auth failed");
    }

    [Fact]
    public async Task UnhealthyWhenEndpointReturns404()
    {
        var check = CreateCheck(
            CreateConfig("https://otlp-gateway.grafana.net/otlp", "Authorization=Basic abc"),
            HttpStatusCode.NotFound);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("OTEL-HC-002");
        result.Description.Should().Contain("not found");
    }

    [Fact]
    public async Task DegradedWhenEndpointReturns500()
    {
        var check = CreateCheck(
            CreateConfig("https://otlp-gateway.grafana.net/otlp", "Authorization=Basic abc"),
            HttpStatusCode.InternalServerError);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("OTEL-HC-003");
    }

    [Fact]
    public async Task UnhealthyWhenConnectionFails()
    {
        var check = CreateCheck(
            CreateConfig("https://otlp-gateway.grafana.net/otlp", "Authorization=Basic abc"),
            throwException: new HttpRequestException("Connection refused"));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("OTEL-HC-004");
        result.Exception.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task UnhealthyWhenTimeout()
    {
        var check = CreateCheck(
            CreateConfig("https://otlp-gateway.grafana.net/otlp", "Authorization=Basic abc"),
            throwException: new TaskCanceledException("Timeout"));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("OTEL-HC-004");
    }

    [Fact]
    public async Task ProbesCorrectTracesEndpoint()
    {
        Uri? capturedUri = null;
        var handler = new CapturingHandler(HttpStatusCode.OK, uri => capturedUri = uri);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var logger = new Mock<ILogger<OtlpExportHealthCheck>>();
        var check = new OtlpExportHealthCheck(
            CreateConfig("https://otlp-gateway.grafana.net/otlp", "Authorization=Basic abc"),
            factory.Object, logger.Object);

        await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        capturedUri.Should().NotBeNull();
        capturedUri!.AbsolutePath.Should().Be("/otlp/v1/traces");
    }

    [Fact]
    public async Task ProbesCorrectEndpointWithTrailingSlash()
    {
        Uri? capturedUri = null;
        var handler = new CapturingHandler(HttpStatusCode.OK, uri => capturedUri = uri);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var logger = new Mock<ILogger<OtlpExportHealthCheck>>();
        var check = new OtlpExportHealthCheck(
            CreateConfig("https://otlp-gateway.grafana.net/otlp/", "Authorization=Basic abc"),
            factory.Object, logger.Object);

        await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        capturedUri!.AbsolutePath.Should().Be("/otlp/v1/traces");
    }

    [Fact]
    public async Task ParsesAuthHeaderCorrectly()
    {
        string? capturedAuth = null;
        var handler = new CapturingHandler(HttpStatusCode.OK, headerCapture: headers =>
            capturedAuth = headers?.Authorization?.ToString());
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var logger = new Mock<ILogger<OtlpExportHealthCheck>>();
        var check = new OtlpExportHealthCheck(
            CreateConfig("https://otlp-gateway.grafana.net/otlp", "Authorization=Basic MTU1NzE1MDp0b2tlbg=="),
            factory.Object, logger.Object);

        await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        capturedAuth.Should().Be("Basic MTU1NzE1MDp0b2tlbg==");
    }

    [Fact]
    public async Task DataIncludesEndpointUrl()
    {
        var check = CreateCheck(
            CreateConfig("https://otlp-gateway.grafana.net/otlp", "Authorization=Basic abc"),
            HttpStatusCode.OK);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Data["endpoint"].Should().Be("https://otlp-gateway.grafana.net/otlp/v1/traces");
    }

    private class TestHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) => throw exception;
    }

    private class CapturingHandler(
        HttpStatusCode statusCode,
        Action<Uri>? uriCapture = null,
        Action<System.Net.Http.Headers.HttpRequestHeaders>? headerCapture = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            uriCapture?.Invoke(request.RequestUri!);
            headerCapture?.Invoke(request.Headers);
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
