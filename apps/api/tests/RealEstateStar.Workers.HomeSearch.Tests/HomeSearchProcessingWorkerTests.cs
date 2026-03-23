using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.HomeSearch;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.HomeSearch.Tests;

public class HomeSearchProcessingWorkerTests
{
    private readonly HomeSearchProcessingChannel _channel = new();
    private readonly Mock<IHomeSearchProvider> _homeSearchProvider = new();
    private readonly Mock<IHomeSearchNotifier> _homeSearchNotifier = new();
    private readonly Mock<ILeadStore> _leadStore = new();
    private readonly BackgroundServiceHealthTracker _healthTracker = new();
    private readonly Mock<ILogger<HomeSearchProcessingWorker>> _logger = new();

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    private HomeSearchProcessingWorker CreateWorker() =>
        new(_channel, _homeSearchProvider.Object, _homeSearchNotifier.Object, _leadStore.Object, _healthTracker, _logger.Object,
            EmptyConfig());

    private static Lead MakeLead() => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "test-agent",
        LeadType = LeadType.Buyer,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-1234",
        Timeline = "3-6m",
        BuyerDetails = new BuyerDetails { City = "Springfield", State = "NJ" }
    };

    private static HomeSearchProcessingRequest MakeRequest(Lead? lead = null) =>
        new("test-agent", lead ?? MakeLead(), "corr-123");

    private static List<Listing> MakeListings(int count) =>
        Enumerable.Range(0, count).Select(i => new Listing(
            Address: $"{100 + i} Oak Ave",
            City: "Springfield",
            State: "NJ",
            Zip: "07081",
            Price: 400000 + i * 10000,
            Beds: 3,
            Baths: 2,
            Sqft: 1600,
            WhyThisFits: null,
            ListingUrl: null)).ToList();

    [Fact]
    public async Task ProcessesHomeSearch_NotifiesBuyer_WhenListingsFound()
    {
        var lead = MakeLead();
        var listings = MakeListings(3);
        var expectedSearchId = $"search-{lead.Id}";

        _homeSearchProvider
            .Setup(p => p.SearchAsync(It.IsAny<HomeSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listings);
        _leadStore
            .Setup(s => s.UpdateHomeSearchIdAsync(It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _homeSearchNotifier
            .Setup(n => n.NotifyBuyerAsync(It.IsAny<string>(), It.IsAny<Lead>(), It.IsAny<List<Listing>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        _homeSearchNotifier.Verify(
            n => n.NotifyBuyerAsync("test-agent", lead, listings, "corr-123", It.IsAny<CancellationToken>()),
            Times.Once);
        _leadStore.Verify(
            s => s.UpdateHomeSearchIdAsync("test-agent", lead.Id, expectedSearchId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SkipsNotification_WhenNoListingsFound()
    {
        _homeSearchProvider
            .Setup(p => p.SearchAsync(It.IsAny<HomeSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        _homeSearchNotifier.Verify(
            n => n.NotifyBuyerAsync(It.IsAny<string>(), It.IsAny<Lead>(), It.IsAny<List<Listing>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _leadStore.Verify(
            s => s.UpdateHomeSearchIdAsync(It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ContinuesAfterNotifierFailure()
    {
        _homeSearchProvider
            .Setup(p => p.SearchAsync(It.IsAny<HomeSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeListings(2));
        _leadStore
            .Setup(s => s.UpdateHomeSearchIdAsync(It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _homeSearchNotifier
            .Setup(n => n.NotifyBuyerAsync(It.IsAny<string>(), It.IsAny<Lead>(), It.IsAny<List<Listing>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        var act = async () => await worker.ExecuteTask!;

        await act.Should().NotThrowAsync();

        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[HS-WORKER-031]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogsError_WhenProviderFails()
    {
        _homeSearchProvider
            .Setup(p => p.SearchAsync(It.IsAny<HomeSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("scraper unavailable"));

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[HS-WORKER-002]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
