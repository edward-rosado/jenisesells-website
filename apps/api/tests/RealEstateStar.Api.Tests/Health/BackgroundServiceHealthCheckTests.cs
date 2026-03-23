using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Api.Health;
using RealEstateStar.Workers.Cma;
using RealEstateStar.Workers.HomeSearch;
using RealEstateStar.Workers.Leads;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Api.Tests.Health;

public class BackgroundServiceHealthCheckTests
{
    private readonly BackgroundServiceHealthTracker _tracker = new();
    private readonly LeadProcessingChannel _leadChannel = new();
    private readonly CmaProcessingChannel _cmaChannel = new();
    private readonly HomeSearchProcessingChannel _homeSearchChannel = new();
    private readonly Mock<ILogger<BackgroundServiceHealthCheck>> _logger = new();

    private BackgroundServiceHealthCheck CreateCheck() =>
        new(_tracker, _leadChannel, _cmaChannel, _homeSearchChannel, _logger.Object);

    private static HealthCheckContext MakeContext() => new()
    {
        Registration = new HealthCheckRegistration("test", new Mock<IHealthCheck>().Object, null, null)
    };

    [Fact]
    public async Task ReturnsHealthy_WhenAllChannelsEmpty()
    {
        var check = CreateCheck();

        var result = await check.CheckHealthAsync(MakeContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("All workers active or idle");
    }

    [Fact]
    public async Task ReturnsHealthy_WhenChannelHasItems_AndWorkerRecentlyActive()
    {
        // Worker just processed something
        _tracker.RecordActivity("LeadProcessingWorker");

        // Channel has an item queued
        await _leadChannel.Writer.WriteAsync(
            new LeadProcessingRequest("agent", MakeLead(), "corr-1"), CancellationToken.None);

        var check = CreateCheck();
        var result = await check.CheckHealthAsync(MakeContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task ReturnsUnhealthy_WhenChannelHasItems_AndWorkerNeverActive()
    {
        // Channel has an item queued but worker has never processed
        await _leadChannel.Writer.WriteAsync(
            new LeadProcessingRequest("agent", MakeLead(), "corr-1"), CancellationToken.None);

        var check = CreateCheck();
        var result = await check.CheckHealthAsync(MakeContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("LeadProcessingWorker");
        result.Description.Should().Contain("never active");
    }

    [Fact]
    public async Task ReturnsUnhealthy_WhenChannelHasItems_AndWorkerStale()
    {
        // Record activity 10 minutes ago — beyond the 5-minute staleness threshold
        var staleTime = DateTime.UtcNow.AddMinutes(-10);
        _tracker.RecordActivity("CmaProcessingWorker", staleTime);

        // Channel has an item queued
        await _cmaChannel.Writer.WriteAsync(
            new CmaProcessingRequest("agent", MakeLead(), "corr-1"), CancellationToken.None);

        var check = CreateCheck();
        var result = await check.CheckHealthAsync(MakeContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("CmaProcessingWorker");
        result.Description.Should().Contain("queued, idle");
    }

    [Fact]
    public async Task ReturnsUnhealthy_WhenMultipleWorkersStuck()
    {
        // Two channels with items, neither worker ever active
        await _leadChannel.Writer.WriteAsync(
            new LeadProcessingRequest("agent", MakeLead(), "corr-1"), CancellationToken.None);
        await _cmaChannel.Writer.WriteAsync(
            new CmaProcessingRequest("agent", MakeLead(), "corr-2"), CancellationToken.None);

        var check = CreateCheck();
        var result = await check.CheckHealthAsync(MakeContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("LeadProcessingWorker");
        result.Description.Should().Contain("CmaProcessingWorker");
    }

    [Fact]
    public async Task IncludesQueueDepthAndLastActivity_InData()
    {
        _tracker.RecordActivity("LeadProcessingWorker");

        var check = CreateCheck();
        var result = await check.CheckHealthAsync(MakeContext(), CancellationToken.None);

        result.Data.Should().ContainKey("LeadProcessingWorker.queueDepth");
        result.Data.Should().ContainKey("LeadProcessingWorker.lastActivity");
        result.Data["LeadProcessingWorker.queueDepth"].Should().Be(0);
        ((string)result.Data["LeadProcessingWorker.lastActivity"]!).Should().NotBe("never");
    }

    [Fact]
    public async Task ReportsNever_ForWorkerWithNoActivity()
    {
        var check = CreateCheck();
        var result = await check.CheckHealthAsync(MakeContext(), CancellationToken.None);

        result.Data["LeadProcessingWorker.lastActivity"].Should().Be("never");
    }

    private static Lead MakeLead() => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "test",
        LeadType = LeadType.Seller,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "j@e.com",
        Phone = "555",
        Timeline = "3-6m",
        SellerDetails = new SellerDetails
        {
            Address = "123 Main",
            City = "Springfield",
            State = "NJ",
            Zip = "07081"
        }
    };
}
