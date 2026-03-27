using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Cma;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Cma.Tests;

public class CmaProcessingWorkerTests
{
    private readonly CmaProcessingChannel _channel = new();
    private readonly Mock<ICompAggregator> _compAggregator = new();
    private readonly Mock<ICmaAnalyzer> _cmaAnalyzer = new();
    private readonly BackgroundServiceHealthTracker _healthTracker = new();
    private readonly Mock<ILogger<CmaProcessingWorker>> _logger = new();

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    /// <summary>Creates a zero-delay retry config with given max retries (default 0 = no retries after first attempt).</summary>
    private static IConfiguration ZeroDelayConfig(int maxRetries = 0) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pipeline:Cma:Retry:MaxRetries"] = maxRetries.ToString(),
                ["Pipeline:Cma:Retry:BaseDelaySeconds"] = "0"
            })
            .Build();

    private CmaProcessingWorker CreateWorker(IConfiguration? config = null) =>
        new(_channel, _compAggregator.Object, _cmaAnalyzer.Object,
            _healthTracker, _logger.Object, config ?? EmptyConfig());

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
        SellerDetails = new SellerDetails { Address = "123 Main", City = "Springfield", State = "NJ", Zip = "07081" }
    };

    private static AgentNotificationConfig MakeAgentConfig() => new()
    {
        AgentId = "test-agent",
        Handle = "test-agent",
        Name = "Test Agent",
        FirstName = "Test",
        Email = "agent@test.com",
        Phone = "555",
        LicenseNumber = "NJ123",
        BrokerageName = "Test Brokerage",
        PrimaryColor = "#000000",
        AccentColor = "#000000",
        State = "NJ",
    };

    private static CmaProcessingRequest MakeRequest(Lead? lead = null)
    {
        var l = lead ?? MakeLead();
        return new CmaProcessingRequest(
            "test-agent", l, MakeAgentConfig(), "corr-123",
            new TaskCompletionSource<CmaWorkerResult>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    private static List<Comp> MakeComps(int count) =>
        Enumerable.Range(0, count).Select(i => new Comp
        {
            Address = $"{i} Elm St",
            SalePrice = 300000 + i * 10000,
            SaleDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-i * 10)),
            Beds = 3,
            Baths = 2,
            Sqft = 1500,
            DistanceMiles = 0.5 + i * 0.1,
            Source = CompSource.RentCast
        }).ToList();

    private static CmaAnalysis MakeAnalysis() => new()
    {
        ValueLow = 300000,
        ValueMid = 350000,
        ValueHigh = 400000,
        MarketNarrative = "test",
        MarketTrend = "Balanced",
        MedianDaysOnMarket = 30
    };

    [Fact]
    public async Task SetsCmaWorkerResult_WhenCompsAndAnalysisSucceed()
    {
        var comps = MakeComps(5);
        var analysis = MakeAnalysis();

        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        var request = MakeRequest();
        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(request, CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        var result = await request.Completion.Task;

        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.EstimatedValue.Should().Be(350000);
        result.PriceRangeLow.Should().Be(300000);
        result.PriceRangeHigh.Should().Be(400000);
        result.MarketAnalysis.Should().Be("test");
        result.Comps.Should().HaveCount(5);
        result.Comps![0].Price.Should().Be(300000);
        result.Comps![0].Beds.Should().Be(3);
        result.Comps![0].Baths.Should().Be(2);
        result.Comps![0].Sqft.Should().Be(1500);
    }

    [Fact]
    public async Task SetsFailureResult_WhenNoCompsFound()
    {
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var request = MakeRequest();
        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(request, CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        var result = await request.Completion.Task;

        result.Success.Should().BeFalse();
        result.Error.Should().Be("No comparable sales found");
        result.EstimatedValue.Should().BeNull();
        result.Comps.Should().BeNull();

        _cmaAnalyzer.Verify(
            a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetsFailureResult_WhenAnalysisPermanentlyFails()
    {
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("claude down"));

        var request = MakeRequest();
        var worker = CreateWorker(ZeroDelayConfig(maxRetries: 0));
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(request, CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        var act = async () => await worker.ExecuteTask!;

        await act.Should().NotThrowAsync();

        var result = await request.Completion.Task;

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        result.EstimatedValue.Should().BeNull();
    }

    [Fact]
    public void DetermineReportType_Comprehensive()
    {
        var result = CmaProcessingWorker.DetermineReportType(6);

        result.Should().Be(ReportType.Comprehensive);
    }

    [Fact]
    public void DetermineReportType_Standard()
    {
        var result = CmaProcessingWorker.DetermineReportType(4);

        result.Should().Be(ReportType.Standard);
    }

    [Fact]
    public void DetermineReportType_Lean()
    {
        var result = CmaProcessingWorker.DetermineReportType(2);

        result.Should().Be(ReportType.Lean);
    }
}
