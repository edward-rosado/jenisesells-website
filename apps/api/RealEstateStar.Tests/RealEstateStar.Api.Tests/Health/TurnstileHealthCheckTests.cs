using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RealEstateStar.Api.Health;

namespace RealEstateStar.Api.Tests.Health;

public class TurnstileHealthCheckTests
{
    private static IConfiguration CreateConfig(string? secretKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Turnstile:SecretKey"] = secretKey
            })
            .Build();

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenSecretKeyConfigured()
    {
        var config = CreateConfig("test-secret-key");
        var check = new TurnstileHealthCheck(config);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("configured");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenSecretKeyMissing()
    {
        var config = CreateConfig(null);
        var check = new TurnstileHealthCheck(config);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("not configured");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenSecretKeyEmpty()
    {
        var config = CreateConfig(string.Empty);
        var check = new TurnstileHealthCheck(config);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("not configured");
    }
}
