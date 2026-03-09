using FluentAssertions;
using Moq;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Analysis;
using RealEstateStar.Api.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Services.Pdf;
using RealEstateStar.Api.Services.Research;

namespace RealEstateStar.Api.Tests.Services;

public class CmaPipelineTests
{
    private static Lead MakeLead() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-1234",
        Address = "123 Main St",
        City = "Old Bridge",
        State = "NJ",
        Zip = "08857",
        Timeline = "3-6 months",
        Beds = 3,
        Baths = 2,
        Sqft = 1800
    };

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
        var lead = MakeLead();
        var agentConfig = MakeAgentConfig();
        var comps = MakeComps();
        var analysis = MakeAnalysis();
        var research = MakeResearch();

        var agentConfigService = new Mock<IAgentConfigService>();
        agentConfigService.Setup(s => s.GetAgentAsync("test-agent"))
            .ReturnsAsync(agentConfig);

        var compAggregator = new Mock<CompAggregator>(
            Enumerable.Empty<ICompSource>(), (Microsoft.Extensions.Logging.ILogger<CompAggregator>?)null);
        compAggregator.Setup(s => s.FetchCompsAsync(
                lead.Address, lead.City, lead.State, lead.Zip,
                lead.Beds, lead.Baths, lead.Sqft))
            .ReturnsAsync(comps);

        var researchService = new Mock<ILeadResearchService>();
        researchService.Setup(s => s.ResearchAsync(lead))
            .ReturnsAsync(research);

        var analysisService = new Mock<IAnalysisService>();
        analysisService.Setup(s => s.AnalyzeAsync(lead, comps, research, ReportType.Standard))
            .ReturnsAsync(analysis);

        var pdfGenerator = new Mock<ICmaPdfGenerator>();

        var gwsService = new Mock<IGwsService>();
        gwsService.Setup(s => s.CreateDriveFolderAsync(
                It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("folder-id");
        gwsService.Setup(s => s.UploadFileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://drive.google.com/file/abc123");
        gwsService.Setup(s => s.CreateDocAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
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
        var job = CmaJob.Create(Guid.NewGuid(), lead);
        await pipeline.ExecuteAsync(job, "test-agent", lead, s => statuses.Add(s));

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
            lead.Address, lead.City, lead.State, lead.Zip,
            lead.Beds, lead.Baths, lead.Sqft), Times.Once);
        researchService.Verify(s => s.ResearchAsync(lead), Times.Once);
        analysisService.Verify(s => s.AnalyzeAsync(lead, comps, research, ReportType.Standard), Times.Once);
        pdfGenerator.Verify(s => s.Generate(
            It.IsAny<string>(), agentConfig, lead, comps, analysis, research, ReportType.Standard), Times.Once);
    }

    [Fact]
    public async Task Execute_ReturnsNull_ForUnknownAgent()
    {
        // Arrange
        var agentConfigService = new Mock<IAgentConfigService>();
        agentConfigService.Setup(s => s.GetAgentAsync("unknown-agent"))
            .ReturnsAsync((AgentConfig?)null);

        var pipeline = new CmaPipeline(
            agentConfigService.Object,
            new Mock<CompAggregator>(Enumerable.Empty<ICompSource>(), null).Object,
            new Mock<ILeadResearchService>().Object,
            new Mock<IAnalysisService>().Object,
            new Mock<ICmaPdfGenerator>().Object,
            new Mock<IGwsService>().Object);

        // Act
        var job = CmaJob.Create(Guid.NewGuid(), MakeLead());
        await pipeline.ExecuteAsync(job, "unknown-agent", MakeLead(), _ => { });

        // Assert — job should still be in Parsing status (pipeline returned early)
        job.Status.Should().Be(CmaJobStatus.Parsing);
    }
}
