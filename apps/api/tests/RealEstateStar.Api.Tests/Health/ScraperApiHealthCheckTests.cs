using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using RealEstateStar.Api.Health;

namespace RealEstateStar.Api.Tests.Health;

public class ScraperApiHealthCheckTests
{
    private static IConfiguration CreateConfig(string? apiKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ScraperApi:ApiKey"] = apiKey
            })
            .Build();

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenApiKeyConfigured()
    {
        var config = CreateConfig("test-api-key");
        var factory = new Mock<IHttpClientFactory>();
        var check = new ScraperApiHealthCheck(config, factory.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("configured");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenApiKeyMissing()
    {
        var config = CreateConfig(null);
        var factory = new Mock<IHttpClientFactory>();
        var check = new ScraperApiHealthCheck(config, factory.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("not configured");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenApiKeyEmpty()
    {
        var config = CreateConfig(string.Empty);
        var factory = new Mock<IHttpClientFactory>();
        var check = new ScraperApiHealthCheck(config, factory.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("not configured");
    }
}
