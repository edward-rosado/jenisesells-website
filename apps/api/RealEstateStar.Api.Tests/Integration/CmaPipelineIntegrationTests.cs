using FluentAssertions;
using Moq;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Analysis;
using RealEstateStar.Api.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Services.Pdf;
using RealEstateStar.Api.Services.Research;

namespace RealEstateStar.Api.Tests.Integration;

public class CmaPipelineIntegrationTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "config", "agent.schema.json")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root");
    }

    [Fact]
    public async Task FullPipeline_GeneratesPdf_AndCompletes()
    {
        // Arrange — real AgentConfigService
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "config", "agents");
        var agentConfigService = new AgentConfigService(configDir);

        // Arrange — mocked ICompSource returning 2 test comps
        var mockCompSource = new Mock<ICompSource>();
        mockCompSource.Setup(s => s.Name).Returns("TestSource");
        mockCompSource
            .Setup(s => s.FetchAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(
            [
                new Comp
                {
                    Address = "100 Oak Ave, Old Bridge, NJ 08857",
                    SalePrice = 425_000m,
                    SaleDate = new DateOnly(2025, 11, 15),
                    Beds = 3,
                    Baths = 2,
                    Sqft = 1750,
                    DaysOnMarket = 18,
                    DistanceMiles = 0.4,
                    Source = CompSource.Mls
                },
                new Comp
                {
                    Address = "200 Maple Dr, Old Bridge, NJ 08857",
                    SalePrice = 460_000m,
                    SaleDate = new DateOnly(2025, 12, 3),
                    Beds = 3,
                    Baths = 2,
                    Sqft = 1900,
                    DaysOnMarket = 22,
                    DistanceMiles = 0.7,
                    Source = CompSource.Zillow
                }
            ]);

        // Arrange — real CompAggregator with mocked source
        var compAggregator = new CompAggregator([mockCompSource.Object]);

        // Arrange — mocked ILeadResearchService
        var mockResearch = new Mock<ILeadResearchService>();
        mockResearch
            .Setup(r => r.ResearchAsync(It.IsAny<Lead>()))
            .ReturnsAsync(new LeadResearch
            {
                Occupation = "Software Engineer",
                Employer = "Acme Corp",
                PurchaseDate = new DateOnly(2018, 6, 1),
                PurchasePrice = 320_000m,
                TaxAssessment = 380_000m,
                AnnualPropertyTax = 8_500m,
                EstimatedEquityLow = 80_000m,
                EstimatedEquityHigh = 140_000m,
                LifeEventInsight = "Growing family — likely needs more space",
                NeighborhoodContext = "Quiet suburban area with good schools",
                YearBuilt = 1995,
                LotSize = 0.25m,
                LotSizeUnit = "acres"
            });

        // Arrange — mocked IAnalysisService
        var mockAnalysis = new Mock<IAnalysisService>();
        mockAnalysis
            .Setup(a => a.AnalyzeAsync(
                It.IsAny<Lead>(), It.IsAny<List<Comp>>(),
                It.IsAny<LeadResearch?>(), It.IsAny<ReportType>()))
            .ReturnsAsync(new CmaAnalysis
            {
                ValueLow = 410_000m,
                ValueMid = 440_000m,
                ValueHigh = 470_000m,
                MarketNarrative = "The Old Bridge market is competitive with homes selling quickly.",
                PricingRecommendation = "Price at $440,000 for maximum exposure.",
                LeadInsights = "Owner since 2018, likely has significant equity.",
                ConversationStarters = ["Ask about the home improvements since 2018", "Discuss school district quality"],
                MarketTrend = "Appreciating",
                MedianDaysOnMarket = 20
            });

        // Arrange — real CmaPdfGenerator
        var pdfGenerator = new CmaPdfGenerator();

        // Arrange — mocked IGwsService
        var mockGws = new Mock<IGwsService>();
        mockGws
            .Setup(g => g.CreateDriveFolderAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("folder-id-123");
        mockGws
            .Setup(g => g.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://drive.google.com/file/d/abc123");
        mockGws
            .Setup(g => g.CreateDocAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("doc-id-456");
        mockGws
            .Setup(g => g.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        mockGws
            .Setup(g => g.AppendSheetRowAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()))
            .Returns(Task.CompletedTask);

        // Arrange — pipeline
        var pipeline = new CmaPipeline(
            agentConfigService,
            compAggregator,
            mockResearch.Object,
            mockAnalysis.Object,
            pdfGenerator,
            mockGws.Object);

        var lead = new Lead
        {
            FirstName = "John",
            LastName = "Smith",
            Email = "john@test.com",
            Phone = "555-0100",
            Address = "123 Main St",
            City = "Old Bridge",
            State = "NJ",
            Zip = "08857",
            Timeline = "ASAP",
            Beds = 3,
            Baths = 2,
            Sqft = 1800
        };

        var statusLog = new List<CmaJobStatus>();
        CmaJob? job = null;

        try
        {
            // Act
            job = await pipeline.ExecuteAsync("jenise-buckalew", lead,
                status => statusLog.Add(status));

            // Assert — job basics
            job.Should().NotBeNull();
            job!.Status.Should().Be(CmaJobStatus.Complete);
            job.Comps.Should().HaveCount(2);
            job.Analysis.Should().NotBeNull();
            job.PdfPath.Should().NotBeNullOrWhiteSpace();
            File.Exists(job.PdfPath).Should().BeTrue("the PDF file should exist on disk");
            job.ReportType.Should().Be(ReportType.Comprehensive, "timeline 'ASAP' maps to Comprehensive");

            // Assert — status progression (the pipeline emits these via the callback)
            statusLog.Should().Equal(
                CmaJobStatus.SearchingComps,
                CmaJobStatus.Analyzing,
                CmaJobStatus.GeneratingPdf,
                CmaJobStatus.OrganizingDrive,
                CmaJobStatus.SendingEmail,
                CmaJobStatus.Logging,
                CmaJobStatus.Complete);

            // Assert — GWS interactions
            mockGws.Verify(
                g => g.CreateDriveFolderAsync("jenisesellsnj@gmail.com", It.IsAny<string>()),
                Times.Once);

            mockGws.Verify(
                g => g.SendEmailAsync("jenisesellsnj@gmail.com", "john@test.com",
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
                Times.Once);
        }
        finally
        {
            // Cleanup — remove generated PDF and temp directory
            if (job?.PdfPath is not null && File.Exists(job.PdfPath))
            {
                var dir = Path.GetDirectoryName(job.PdfPath);
                File.Delete(job.PdfPath);
                if (dir is not null && Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }
    }
}
