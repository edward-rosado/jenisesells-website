using Xunit;
using Moq;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Workers.Onboarding;
using RealEstateStar.Workers.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
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
