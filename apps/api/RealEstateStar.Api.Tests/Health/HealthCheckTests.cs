using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RealEstateStar.Api.Health;

namespace RealEstateStar.Api.Tests.Health;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsJsonWithCheckNames()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("status", out _).Should().BeTrue();
        root.TryGetProperty("checks", out var checks).Should().BeTrue();

        var checkNames = checks.EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToList();

        checkNames.Should().Contain("gws_cli");
        checkNames.Should().Contain("claude_api");
    }

    [Fact]
    public async Task GwsCliHealthCheck_ReturnsValidResult()
    {
        var check = new GwsCliHealthCheck();

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        result.Status.Should().BeOneOf(
            HealthStatus.Healthy,
            HealthStatus.Degraded,
            HealthStatus.Unhealthy);
    }
}
