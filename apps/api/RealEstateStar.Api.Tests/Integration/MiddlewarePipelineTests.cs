using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RealEstateStar.Api.Tests.Integration;

public class MiddlewarePipelineTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MiddlewarePipelineTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CorsHeaders_PresentOnResponse()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", "http://localhost:3000");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().Contain("http://localhost:3000");
    }

    [Fact]
    public async Task CorsPreflightRequest_ReturnsAllowedMethods()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/health/live");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.Contains("Access-Control-Allow-Methods").Should().BeTrue();
    }

    [Fact]
    public async Task RateLimiting_Returns429WhenExceeded()
    {
        var client = _factory.CreateClient();

        // The global limiter allows 100 requests per minute per IP.
        // Send 101 requests to /health/live to exceed the limit.
        var tasks = Enumerable.Range(0, 101)
            .Select(_ => client.GetAsync("/health/live"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        statusCodes.Should().Contain(HttpStatusCode.TooManyRequests,
            "at least one request should be rate-limited after exceeding 100 per minute");
    }
}
