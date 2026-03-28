using RealEstateStar.Domain.Leads.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Lead.CMA.Tests;

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

    private static RentCastCompSource MakeRentCastCompSource(RentCastValuation? valuation = null)
    {
        var mockClient = new Mock<IRentCastClient>();
        mockClient
            .Setup(c => c.GetValuationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(valuation);
        return new RentCastCompSource(mockClient.Object, NullLogger<RentCastCompSource>.Instance);
    }

    private CmaProcessingWorker CreateWorker(IConfiguration? config = null, RentCastCompSource? rentCastCompSource = null) =>
        new(_channel, _compAggregator.Object, rentCastCompSource ?? MakeRentCastCompSource(),
            _cmaAnalyzer.Object, _healthTracker, _logger.Object, config ?? EmptyConfig());

    private static RealEstateStar.Domain.Leads.Models.Lead MakeLead() => new()
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

    private static CmaProcessingRequest MakeRequest(RealEstateStar.Domain.Leads.Models.Lead? lead = null)
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
            .Setup(a => a.AnalyzeAsync(It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
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
            a => a.AnalyzeAsync(It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()),
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
            .Setup(a => a.AnalyzeAsync(It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
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
        var result = CmaProcessingWorker.DetermineReportType(5);

        result.Should().Be(ReportType.Comprehensive);
    }

    [Fact]
    public void DetermineReportType_Standard_WhenFourComps()
    {
        // 4 comps is Standard (below the >= 5 Comprehensive threshold)
        var result = CmaProcessingWorker.DetermineReportType(4);

        result.Should().Be(ReportType.Standard);
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

    // ---------------------------------------------------------------------------
    // EnrichSubjectAsync — fills missing beds/baths/sqft from RentCast subject
    // ---------------------------------------------------------------------------

    private static RentCastValuation MakeValuationWithSubject(
        int? beds = 4, decimal? baths = 2.5m, int? sqft = 2000) => new()
        {
            Price = 450_000m,
            PriceRangeLow = 420_000m,
            PriceRangeHigh = 480_000m,
            Comparables = [],
            SubjectProperty = new RentCastSubjectProperty
            {
                FormattedAddress = "123 Main St, Springfield, NJ 07081",
                Bedrooms = beds,
                Bathrooms = baths,
                SquareFootage = sqft
            }
        };

    [Fact]
    public async Task EnrichSubject_FillsBedsFromRentCast_WhenLeadHasNoBeds()
    {
        var valuation = MakeValuationWithSubject(beds: 4);
        var compSource = MakeRentCastCompSource(valuation);
        // Pre-populate LastValuation by simulating FetchAsync having run
        await compSource.FetchAsync(new CompSearchRequest
        {
            Address = "123 Main",
            City = "Springfield",
            State = "NJ",
            Zip = "07081"
        }, CancellationToken.None);

        RealEstateStar.Domain.Leads.Models.Lead? capturedLead = null;
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .Callback<RealEstateStar.Domain.Leads.Models.Lead, List<Comp>, CancellationToken>((lead, _, _) => capturedLead = lead)
            .ReturnsAsync(MakeAnalysis());

        // Lead has no beds
        var lead = MakeLead();
        lead.SellerDetails = lead.SellerDetails! with { Beds = null };
        var worker = CreateWorker(rentCastCompSource: compSource);
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        capturedLead.Should().NotBeNull();
        capturedLead!.SellerDetails!.Beds.Should().Be(4);
    }

    [Fact]
    public async Task EnrichSubject_FillsBathsFromRentCast_WhenLeadHasNoBaths()
    {
        var valuation = MakeValuationWithSubject(baths: 2.5m);
        var compSource = MakeRentCastCompSource(valuation);
        await compSource.FetchAsync(new CompSearchRequest
        {
            Address = "123 Main",
            City = "Springfield",
            State = "NJ",
            Zip = "07081"
        }, CancellationToken.None);

        RealEstateStar.Domain.Leads.Models.Lead? capturedLead = null;
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .Callback<RealEstateStar.Domain.Leads.Models.Lead, List<Comp>, CancellationToken>((lead, _, _) => capturedLead = lead)
            .ReturnsAsync(MakeAnalysis());

        // Lead has no baths — 2.5 from RentCast should round to 3
        var lead = MakeLead();
        lead.SellerDetails = lead.SellerDetails! with { Baths = null };
        var worker = CreateWorker(rentCastCompSource: compSource);
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        capturedLead.Should().NotBeNull();
        capturedLead!.SellerDetails!.Baths.Should().Be(3); // 2.5 rounded up
    }

    [Fact]
    public async Task EnrichSubject_FillsSqftFromRentCast_WhenLeadHasNoSqft()
    {
        var valuation = MakeValuationWithSubject(sqft: 2200);
        var compSource = MakeRentCastCompSource(valuation);
        await compSource.FetchAsync(new CompSearchRequest
        {
            Address = "123 Main",
            City = "Springfield",
            State = "NJ",
            Zip = "07081"
        }, CancellationToken.None);

        RealEstateStar.Domain.Leads.Models.Lead? capturedLead = null;
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .Callback<RealEstateStar.Domain.Leads.Models.Lead, List<Comp>, CancellationToken>((lead, _, _) => capturedLead = lead)
            .ReturnsAsync(MakeAnalysis());

        var lead = MakeLead();
        lead.SellerDetails = lead.SellerDetails! with { Sqft = null };
        var worker = CreateWorker(rentCastCompSource: compSource);
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        capturedLead.Should().NotBeNull();
        capturedLead!.SellerDetails!.Sqft.Should().Be(2200);
    }

    [Fact]
    public async Task EnrichSubject_DoesNotOverwrite_WhenLeadAlreadyHasData()
    {
        var valuation = MakeValuationWithSubject(beds: 4, baths: 2.5m, sqft: 2200);
        var compSource = MakeRentCastCompSource(valuation);
        await compSource.FetchAsync(new CompSearchRequest
        {
            Address = "123 Main",
            City = "Springfield",
            State = "NJ",
            Zip = "07081"
        }, CancellationToken.None);

        RealEstateStar.Domain.Leads.Models.Lead? capturedLead = null;
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .Callback<RealEstateStar.Domain.Leads.Models.Lead, List<Comp>, CancellationToken>((lead, _, _) => capturedLead = lead)
            .ReturnsAsync(MakeAnalysis());

        // Lead already has all three fields — should not be overwritten
        var lead = MakeLead();
        lead.SellerDetails = lead.SellerDetails! with { Beds = 3, Baths = 2, Sqft = 1800 };
        var worker = CreateWorker(rentCastCompSource: compSource);
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        capturedLead.Should().NotBeNull();
        capturedLead!.SellerDetails!.Beds.Should().Be(3);
        capturedLead.SellerDetails.Baths.Should().Be(2);
        capturedLead.SellerDetails.Sqft.Should().Be(1800);
    }

    [Fact]
    public async Task EnrichSubject_Skips_WhenNoRentCastValuation()
    {
        // RentCastCompSource has no LastValuation (client returned null)
        var compSource = MakeRentCastCompSource(valuation: null);

        RealEstateStar.Domain.Leads.Models.Lead? capturedLead = null;
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .Callback<RealEstateStar.Domain.Leads.Models.Lead, List<Comp>, CancellationToken>((lead, _, _) => capturedLead = lead)
            .ReturnsAsync(MakeAnalysis());

        var lead = MakeLead();
        lead.SellerDetails = lead.SellerDetails! with { Beds = null, Baths = null, Sqft = null };
        var worker = CreateWorker(rentCastCompSource: compSource);
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(lead), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // Fields remain null — no enrichment happened
        capturedLead.Should().NotBeNull();
        capturedLead!.SellerDetails!.Beds.Should().BeNull();
        capturedLead.SellerDetails.Baths.Should().BeNull();
        capturedLead.SellerDetails.Sqft.Should().BeNull();
    }
}
