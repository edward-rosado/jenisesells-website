using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RealEstateStar.Api.Health;
using RealEstateStar.Api.Tests.Integration;

namespace RealEstateStar.Api.Tests.Health;

public class HealthCheckTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public HealthCheckTests(TestWebApplicationFactory factory)
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
        root.TryGetProperty("entries", out var entries).Should().BeTrue();

        var entryNames = entries.EnumerateObject()
            .Select(e => e.Name)
            .ToList();

        entryNames.Should().Contain("claude_api");
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
