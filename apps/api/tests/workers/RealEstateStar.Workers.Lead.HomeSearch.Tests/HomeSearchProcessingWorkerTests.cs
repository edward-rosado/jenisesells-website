using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Lead.HomeSearch.Tests;

public class HomeSearchProcessingWorkerTests
{
    private readonly HomeSearchProcessingChannel _channel = new();
    private readonly Mock<IHomeSearchProvider> _homeSearchProvider = new();
    private readonly BackgroundServiceHealthTracker _healthTracker = new();
    private readonly Mock<ILogger<HomeSearchProcessingWorker>> _logger = new();

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    /// <summary>Creates a zero-delay retry config with given max retries (default 0 = no retries after first attempt).</summary>
    private static IConfiguration ZeroDelayConfig(int maxRetries = 0) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pipeline:HomeSearch:Retry:MaxRetries"] = maxRetries.ToString(),
                ["Pipeline:HomeSearch:Retry:BaseDelaySeconds"] = "0"
            })
            .Build();

    private HomeSearchProcessingWorker CreateWorker(IConfiguration? config = null) =>
        new(_channel, _homeSearchProvider.Object, _healthTracker, _logger.Object,
            config ?? EmptyConfig());

    private static AgentNotificationConfig MakeAgentConfig() => new()
    {
        AgentId = "test-agent",
        Handle = "test-agent",
        Name = "Test Agent",
        FirstName = "Test",
        Email = "agent@test.com",
        Phone = "555-0000",
        LicenseNumber = "LIC123",
        BrokerageName = "Test Brokerage",
        PrimaryColor = "#000000",
        AccentColor = "#ffffff",
        State = "NJ"
    };

    private static global::RealEstateStar.Domain.Leads.Models.Lead MakeLead() => new()
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

    private static HomeSearchProcessingRequest MakeRequest(global::RealEstateStar.Domain.Leads.Models.Lead? lead = null, TaskCompletionSource<HomeSearchWorkerResult>? tcs = null) =>
        new("test-agent", lead ?? MakeLead(), MakeAgentConfig(), "corr-123", tcs ?? new TaskCompletionSource<HomeSearchWorkerResult>());

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
    public async Task CompletesWithListingSummaries_WhenListingsFound()
    {
        var lead = MakeLead();
        var listings = MakeListings(3);
        var tcs = new TaskCompletionSource<HomeSearchWorkerResult>();

        _homeSearchProvider
            .Setup(p => p.SearchAsync(It.IsAny<HomeSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listings);

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead, tcs), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        var result = await tcs.Task;
        result.Success.Should().BeTrue();
        result.LeadId.Should().Be(lead.Id.ToString());
        result.Listings.Should().HaveCount(3);
        result.Listings![0].Address.Should().Be("100 Oak Ave, Springfield, NJ 07081");
        result.Listings![0].Price.Should().Be(400000);
        result.Listings![0].Beds.Should().Be(3);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task CompletesWithEmptyListings_WhenNoListingsFound()
    {
        var tcs = new TaskCompletionSource<HomeSearchWorkerResult>();

        _homeSearchProvider
            .Setup(p => p.SearchAsync(It.IsAny<HomeSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(tcs: tcs), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        var result = await tcs.Task;
        result.Success.Should().BeTrue();
        result.Listings.Should().BeEmpty();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task CompletesWithFailure_WhenProviderPermanentlyFails()
    {
        var tcs = new TaskCompletionSource<HomeSearchWorkerResult>();

        _homeSearchProvider
            .Setup(p => p.SearchAsync(It.IsAny<HomeSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("scraper unavailable"));

        // Zero-delay retries with 0 max retries → permanent failure after 1 attempt
        var worker = CreateWorker(ZeroDelayConfig());
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(tcs: tcs), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        var result = await tcs.Task;
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();

        // The step failure is logged by PipelineWorker with "Step 'fetch-listings' Failed"
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Step 'fetch-listings' Failed")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MapsListingSummary_WithCorrectFields()
    {
        var lead = MakeLead();
        var listing = new Listing(
            Address: "42 Elm St",
            City: "Newark",
            State: "NJ",
            Zip: "07102",
            Price: 550000,
            Beds: 4,
            Baths: 2.5m,
            Sqft: 2200,
            WhyThisFits: "Close to schools",
            ListingUrl: "https://example.com/listing/42");
        var tcs = new TaskCompletionSource<HomeSearchWorkerResult>();

        _homeSearchProvider
            .Setup(p => p.SearchAsync(It.IsAny<HomeSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([listing]);

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead, tcs), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        var result = await tcs.Task;
        result.Success.Should().BeTrue();
        var summary = result.Listings!.Single();
        summary.Address.Should().Be("42 Elm St, Newark, NJ 07102");
        summary.Price.Should().Be(550000);
        summary.Beds.Should().Be(4);
        summary.Baths.Should().Be(2.5m);
        summary.Sqft.Should().Be(2200);
        summary.Url.Should().Be("https://example.com/listing/42");
        summary.Status.Should().BeNull();
    }

    [Fact]
    public async Task BuildsSearchCriteria_FromBuyerDetails()
    {
        var lead = MakeLead();
        lead.BuyerDetails = new BuyerDetails
        {
            City = "Hoboken",
            State = "NJ",
            MinBudget = 300000,
            MaxBudget = 600000,
            Bedrooms = 2,
            Bathrooms = 1
        };
        var tcs = new TaskCompletionSource<HomeSearchWorkerResult>();

        _homeSearchProvider
            .Setup(p => p.SearchAsync(It.IsAny<HomeSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead, tcs), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        _homeSearchProvider.Verify(p => p.SearchAsync(
            It.Is<HomeSearchCriteria>(c =>
                c.Area == "Hoboken, NJ" &&
                c.MinPrice == 300000 &&
                c.MaxPrice == 600000 &&
                c.MinBeds == 2 &&
                c.MinBaths == 1),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UsesCityOnly_WhenStateIsEmpty()
    {
        var lead = MakeLead();
        lead.BuyerDetails = new BuyerDetails { City = "Hoboken", State = "" };
        var tcs = new TaskCompletionSource<HomeSearchWorkerResult>();

        _homeSearchProvider
            .Setup(p => p.SearchAsync(It.IsAny<HomeSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead, tcs), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        _homeSearchProvider.Verify(p => p.SearchAsync(
            It.Is<HomeSearchCriteria>(c => c.Area == "Hoboken"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
