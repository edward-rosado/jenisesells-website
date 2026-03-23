using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly BackgroundServiceHealthTracker _healthTracker = new();
    private readonly Mock<ILogger<CmaProcessingWorker>> _logger = new();

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    private CmaProcessingWorker CreateWorker() =>
        new(_channel, _compAggregator.Object, _cmaAnalyzer.Object,
            _pdfGenerator.Object, _cmaNotifier.Object, _accountConfigService.Object, _healthTracker, _logger.Object,
            EmptyConfig());

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
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[CMA-WORKER-051]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
    // AccountConfig not found — worker catches, logs [CMA-WORKER-002], does not crash
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AccountConfigNotFound_LogsCmaWorker002_DoesNotCrash()
    {
        var comps = MakeComps(3);
        _compAggregator
            .Setup(a => a.FetchCompsAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        _cmaAnalyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAnalysis());

        // Return null → triggers InvalidOperationException with [CMA-WORKER-012]
        _accountConfigService
            .Setup(a => a.GetAccountAsync("test-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountConfig?)null);

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
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[CMA-WORKER-002]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
