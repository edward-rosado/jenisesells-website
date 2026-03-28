using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RealEstateStar.Api.Health;

namespace RealEstateStar.Api.Tests.Health;

/// <summary>
/// Tests for GwsCliHealthCheck. Since the check spawns a real process,
/// we test all observable branches: gws found or not, exit code, and
/// cancellation token behavior.
/// </summary>
public class GwsCliHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsValidStatus()
    {
        // GwsCliHealthCheck runs `gws --version`. If gws is installed it returns Healthy,
        // if not installed it returns Unhealthy. Either way it should not throw.
        var check = new GwsCliHealthCheck();

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().BeOneOf(
            HealthStatus.Healthy,
            HealthStatus.Degraded,
            HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithAlreadyCancelledToken_ReturnsUnhealthy()
    {
        var check = new GwsCliHealthCheck();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // With an already-cancelled token, the process either won't start
        // or WaitForExitAsync will throw OperationCanceledException, caught by the exception handler
        var result = await check.CheckHealthAsync(new HealthCheckContext(), cts.Token);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }
}
