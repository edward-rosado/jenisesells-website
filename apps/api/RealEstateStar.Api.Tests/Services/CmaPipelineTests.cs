using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Api.Common;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Analysis;
using RealEstateStar.Api.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Services.Pdf;
using RealEstateStar.Api.Services.Research;
using RealEstateStar.Api.Tests.TestHelpers;

namespace RealEstateStar.Api.Tests.Services;

public class CmaPipelineTests
{
    private static AgentConfig MakeAgentConfig() => new()
    {
        Id = "test-agent",
        Identity = new AgentIdentity
        {
            Name = "Test Agent",
            Email = "agent@example.com",
            Phone = "555-9999",
            Brokerage = "Test Realty"
        },
        Integrations = new AgentIntegrations
        {
            FormHandlerId = "sheet-123"
        }
    };

    private static List<Comp> MakeComps() =>
    [
        new()
        {
            Address = "456 Oak Ave",
            SalePrice = 425_000m,
            SaleDate = new DateOnly(2026, 1, 15),
            Beds = 3,
            Baths = 2,
            Sqft = 1600,
            DaysOnMarket = 18,
            DistanceMiles = 0.8,
            Source = CompSource.Zillow
        }
    ];

    private static CmaAnalysis MakeAnalysis() => new()
    {
        ValueLow = 400_000m,
        ValueMid = 425_000m,
        ValueHigh = 450_000m,
        MarketNarrative = "Strong seller's market.",
        PricingRecommendation = "List at $429,900",
        LeadInsights = "Good equity position.",
        ConversationStarters = ["Ask about timeline", "Mention market conditions"],
        MarketTrend = "Seller's",
        MedianDaysOnMarket = 21
    };

    private static LeadResearch MakeResearch() => new()
    {
        Occupation = "Engineer",
        Employer = "Acme Corp",
        PurchaseDate = new DateOnly(2019, 6, 1),
        PurchasePrice = 300_000m,
        TaxAssessment = 380_000m,
        AnnualPropertyTax = 8_500m,
        YearBuilt = 1995,
        LotSize = 0.25m,
        LotSizeUnit = "acres"
    };

    [Fact]
    public async Task Execute_CompletesAllSteps_ForValidInput()
    {
        // Arrange
        var lead = TestData.MakeLead(
            firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "3-6 months",
            beds: 3, baths: 2, sqft: 1800);
        var agentConfig = MakeAgentConfig();
        var comps = MakeComps();
        var analysis = MakeAnalysis();
        var research = MakeResearch();

        var agentConfigService = new Mock<IAgentConfigService>();
        agentConfigService.Setup(s => s.GetAgentAsync("test-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentConfig);

        var compAggregator = new Mock<CompAggregator>(
            Enumerable.Empty<ICompSource>(), (ILogger<CompAggregator>?)null!);
        compAggregator.Setup(s => s.FetchCompsAsync(
                It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);

        var researchService = new Mock<ILeadResearchService>();
        researchService.Setup(s => s.ResearchAsync(lead, It.IsAny<CancellationToken>()))
            .ReturnsAsync(research);

        var analysisService = new Mock<IAnalysisService>();
        analysisService.Setup(s => s.AnalyzeAsync(lead, comps, research, ReportType.Standard, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        var pdfGenerator = new Mock<ICmaPdfGenerator>();

        var gwsService = new Mock<IGwsService>();
        gwsService.Setup(s => s.CreateDriveFolderAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("folder-id");
        gwsService.Setup(s => s.UploadFileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://drive.google.com/file/abc123");
        gwsService.Setup(s => s.CreateDocAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("doc-id");

        var statuses = new List<CmaJobStatus>();

        var pipeline = new CmaPipeline(
            agentConfigService.Object,
            compAggregator.Object,
            researchService.Object,
            analysisService.Object,
            pdfGenerator.Object,
            gwsService.Object);

        // Act
        var job = CmaJob.Create("test-agent", lead);
        await pipeline.ExecuteAsync(job, "test-agent", lead, s => { statuses.Add(s); return Task.CompletedTask; }, CancellationToken.None);

        // Assert
        job.Status.Should().Be(CmaJobStatus.Complete);
        job.CompletedAt.Should().NotBeNull();
        job.Analysis.Should().Be(analysis);
        job.LeadResearch.Should().Be(research);
        job.Comps.Should().BeEquivalentTo(comps);

        statuses.Should().Contain(CmaJobStatus.SearchingComps);
        statuses.Should().Contain(CmaJobStatus.Analyzing);
        statuses.Should().Contain(CmaJobStatus.GeneratingPdf);
        statuses.Should().Contain(CmaJobStatus.Complete);

        // Verify key service calls
        compAggregator.Verify(s => s.FetchCompsAsync(
            It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        researchService.Verify(s => s.ResearchAsync(lead, It.IsAny<CancellationToken>()), Times.Once);
        analysisService.Verify(s => s.AnalyzeAsync(lead, comps, research, ReportType.Standard, It.IsAny<CancellationToken>()), Times.Once);
        pdfGenerator.Verify(s => s.Generate(
            It.IsAny<PdfGenerationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_ThrowsInvalidOperationException_ForUnknownAgent()
    {
        // Arrange
        var agentConfigService = new Mock<IAgentConfigService>();
        agentConfigService.Setup(s => s.GetAgentAsync("unknown-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentConfig?)null);

        var pipeline = new CmaPipeline(
            agentConfigService.Object,
            new Mock<CompAggregator>(Enumerable.Empty<ICompSource>(), (ILogger<CompAggregator>?)null!).Object,
            new Mock<ILeadResearchService>().Object,
            new Mock<IAnalysisService>().Object,
            new Mock<ICmaPdfGenerator>().Object,
            new Mock<IGwsService>().Object);

        // Act
        var lead = TestData.MakeLead(firstName: "Jane", city: "Old Bridge", zip: "08857", beds: 3, baths: 2, sqft: 1800);
        var job = CmaJob.Create("unknown-agent", lead);
        var act = () => pipeline.ExecuteAsync(job, "unknown-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        // Assert — pipeline should throw instead of silently returning
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Agent configuration not found for 'unknown-agent'");
    }
}
