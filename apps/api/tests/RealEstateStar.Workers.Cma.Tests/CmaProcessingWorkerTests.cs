using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Workers.Cma;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Cma.Tests;

public class CmaProcessingWorkerTests
{
    private readonly CmaProcessingChannel _channel = new();
    private readonly Mock<ICompAggregator> _compAggregator = new();
    private readonly Mock<ICmaAnalyzer> _cmaAnalyzer = new();
    private readonly Mock<ICmaPdfGenerator> _pdfGenerator = new();
    private readonly Mock<ICmaNotifier> _cmaNotifier = new();
    private readonly Mock<IAccountConfigService> _accountConfigService = new();
    private readonly Mock<IDocumentStorageProvider> _documentStorage = new();
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
            _cmaAnalyzer.Object, _pdfGenerator.Object, _cmaNotifier.Object, _accountConfigService.Object,
            _documentStorage.Object, _healthTracker, _logger.Object, config ?? EmptyConfig());

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

    private static CmaProcessingRequest MakeRequest(Lead? lead = null)
    {
        var l = lead ?? MakeLead();
        return new CmaProcessingRequest("test-agent", l, "corr-123");
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
            Source = CompSource.Zillow
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

    private void SetupAccountConfig() =>
        _accountConfigService
            .Setup(a => a.GetAccountAsync("test-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountConfig { Agent = new AccountAgent { Name = "Test Agent", Email = "agent@test.com" } });

    [Fact]
    public async Task ProcessesCma_FullPipeline_WhenCompsFound()
    {
        var comps = MakeComps(5);
        var analysis = MakeAnalysis();
        var pdfPath = "/tmp/cma-test.pdf";

        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);
        SetupAccountConfig();
        _pdfGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<Lead>(), It.IsAny<CmaAnalysis>(), It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(), It.IsAny<ReportType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfPath);
        _cmaNotifier
            .Setup(n => n.NotifySellerAsync(It.IsAny<string>(), It.IsAny<Lead>(), It.IsAny<string>(),
                It.IsAny<CmaAnalysis>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        _compAggregator.Verify(
            a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _cmaAnalyzer.Verify(
            a => a.AnalyzeAsync(It.IsAny<Lead>(), comps, It.IsAny<CancellationToken>()),
            Times.Once);
        _pdfGenerator.Verify(
            g => g.GenerateAsync(It.IsAny<Lead>(), analysis, comps, It.IsAny<AccountConfig>(),
                It.IsAny<ReportType>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _cmaNotifier.Verify(
            n => n.NotifySellerAsync("test-agent", It.IsAny<Lead>(), pdfPath, analysis, "corr-123", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SkipsCma_WhenNoCompsFound()
    {
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        _cmaAnalyzer.Verify(
            a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _pdfGenerator.Verify(
            g => g.GenerateAsync(It.IsAny<Lead>(), It.IsAny<CmaAnalysis>(), It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(), It.IsAny<ReportType>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _cmaNotifier.Verify(
            n => n.NotifySellerAsync(It.IsAny<string>(), It.IsAny<Lead>(), It.IsAny<string>(),
                It.IsAny<CmaAnalysis>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ContinuesAfterNotifierFailure()
    {
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAnalysis());
        SetupAccountConfig();
        _pdfGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<Lead>(), It.IsAny<CmaAnalysis>(), It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(), It.IsAny<ReportType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/cma-test.pdf");
        _cmaNotifier
            .Setup(n => n.NotifySellerAsync(It.IsAny<string>(), It.IsAny<Lead>(), It.IsAny<string>(),
                It.IsAny<CmaAnalysis>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        // Zero-delay retries so the test completes quickly
        var worker = CreateWorker(ZeroDelayConfig());
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        var act = async () => await worker.ExecuteTask!;

        await act.Should().NotThrowAsync();

        // The step failure is logged by PipelineWorker with "Step 'notify-seller' Failed"
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Step 'notify-seller' Failed")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
    // TryDeleteTempFile — File.Exists(path) == true branch
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeletesTempPdfFile_WhenFileExists()
    {
        var tempFile = Path.GetTempFileName();
        var comps = MakeComps(3);

        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAnalysis());
        SetupAccountConfig();
        _pdfGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<Lead>(), It.IsAny<CmaAnalysis>(), It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(), It.IsAny<ReportType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tempFile); // return a real file path that exists
        _cmaNotifier
            .Setup(n => n.NotifySellerAsync(It.IsAny<string>(), It.IsAny<Lead>(), It.IsAny<string>(),
                It.IsAny<CmaAnalysis>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // The file should have been deleted
        File.Exists(tempFile).Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // AccountConfig not found — worker catches, logs step failure, does not crash
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AccountConfigNotFound_LogsStepFailure_DoesNotCrash()
    {
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAnalysis());

        // Return null → GeneratePdfAsync throws InvalidOperationException
        _accountConfigService
            .Setup(a => a.GetAccountAsync("test-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountConfig?)null);

        // Zero-delay retries so the test completes quickly
        var worker = CreateWorker(ZeroDelayConfig());
        var cts = new CancellationTokenSource();

        await _channel.Writer.WriteAsync(MakeRequest(), CancellationToken.None);
        _channel.Writer.Complete();

        await worker.StartAsync(cts.Token);
        var act = async () => await worker.ExecuteTask!;

        await act.Should().NotThrowAsync();

        // The generate-pdf step failure is logged by PipelineWorker with "Step 'generate-pdf' Failed"
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Step 'generate-pdf' Failed")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
            Address = "123 Main", City = "Springfield", State = "NJ", Zip = "07081"
        }, CancellationToken.None);

        Lead? capturedLead = null;
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, List<Comp>, CancellationToken>((lead, _, _) => capturedLead = lead)
            .ReturnsAsync(MakeAnalysis());
        SetupAccountConfig();
        _pdfGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<Lead>(), It.IsAny<CmaAnalysis>(), It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(), It.IsAny<ReportType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/cma-test.pdf");
        _cmaNotifier
            .Setup(n => n.NotifySellerAsync(It.IsAny<string>(), It.IsAny<Lead>(), It.IsAny<string>(),
                It.IsAny<CmaAnalysis>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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
            Address = "123 Main", City = "Springfield", State = "NJ", Zip = "07081"
        }, CancellationToken.None);

        Lead? capturedLead = null;
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, List<Comp>, CancellationToken>((lead, _, _) => capturedLead = lead)
            .ReturnsAsync(MakeAnalysis());
        SetupAccountConfig();
        _pdfGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<Lead>(), It.IsAny<CmaAnalysis>(), It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(), It.IsAny<ReportType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/cma-test.pdf");
        _cmaNotifier
            .Setup(n => n.NotifySellerAsync(It.IsAny<string>(), It.IsAny<Lead>(), It.IsAny<string>(),
                It.IsAny<CmaAnalysis>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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
            Address = "123 Main", City = "Springfield", State = "NJ", Zip = "07081"
        }, CancellationToken.None);

        Lead? capturedLead = null;
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, List<Comp>, CancellationToken>((lead, _, _) => capturedLead = lead)
            .ReturnsAsync(MakeAnalysis());
        SetupAccountConfig();
        _pdfGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<Lead>(), It.IsAny<CmaAnalysis>(), It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(), It.IsAny<ReportType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/cma-test.pdf");
        _cmaNotifier
            .Setup(n => n.NotifySellerAsync(It.IsAny<string>(), It.IsAny<Lead>(), It.IsAny<string>(),
                It.IsAny<CmaAnalysis>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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
            Address = "123 Main", City = "Springfield", State = "NJ", Zip = "07081"
        }, CancellationToken.None);

        Lead? capturedLead = null;
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, List<Comp>, CancellationToken>((lead, _, _) => capturedLead = lead)
            .ReturnsAsync(MakeAnalysis());
        SetupAccountConfig();
        _pdfGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<Lead>(), It.IsAny<CmaAnalysis>(), It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(), It.IsAny<ReportType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/cma-test.pdf");
        _cmaNotifier
            .Setup(n => n.NotifySellerAsync(It.IsAny<string>(), It.IsAny<Lead>(), It.IsAny<string>(),
                It.IsAny<CmaAnalysis>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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

        Lead? capturedLead = null;
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, List<Comp>, CancellationToken>((lead, _, _) => capturedLead = lead)
            .ReturnsAsync(MakeAnalysis());
        SetupAccountConfig();
        _pdfGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<Lead>(), It.IsAny<CmaAnalysis>(), It.IsAny<List<Comp>>(),
                It.IsAny<AccountConfig>(), It.IsAny<ReportType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/cma-test.pdf");
        _cmaNotifier
            .Setup(n => n.NotifySellerAsync(It.IsAny<string>(), It.IsAny<Lead>(), It.IsAny<string>(),
                It.IsAny<CmaAnalysis>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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
