using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Api.Common;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Features.Cma.Services;
using RealEstateStar.Api.Features.Cma.Services.Analysis;
using RealEstateStar.Api.Features.Cma.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Features.Cma.Services.Pdf;
using RealEstateStar.Api.Features.Cma.Services.Research;
using RealEstateStar.Api.Tests.TestHelpers;

namespace RealEstateStar.Api.Tests.Features.Cma.Services;

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

    private (CmaPipeline pipeline,
        Mock<IAgentConfigService> agentConfigService,
        Mock<CompAggregator> compAggregator,
        Mock<ILeadResearchService> researchService,
        Mock<IAnalysisService> analysisService,
        Mock<ICmaPdfGenerator> pdfGenerator,
        Mock<IGwsService> gwsService) CreatePipeline(
            AgentConfig? agentConfig = null,
            List<Comp>? comps = null,
            CmaAnalysis? analysis = null,
            LeadResearch? research = null)
    {
        var ac = agentConfig ?? MakeAgentConfig();
        var c = comps ?? MakeComps();
        var a = analysis ?? MakeAnalysis();
        var r = research ?? MakeResearch();

        var agentConfigService = new Mock<IAgentConfigService>();
        agentConfigService.Setup(s => s.GetAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ac);

        var compAggregator = new Mock<CompAggregator>(
            Enumerable.Empty<ICompSource>(), (ILogger<CompAggregator>?)null!);
        compAggregator.Setup(s => s.FetchCompsAsync(
                It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(c);

        var researchService = new Mock<ILeadResearchService>();
        researchService.Setup(s => s.ResearchAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(r);

        var analysisService = new Mock<IAnalysisService>();
        analysisService.Setup(s => s.AnalyzeAsync(
                It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<LeadResearch?>(),
                It.IsAny<ReportType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(a);

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

        var pipeline = new CmaPipeline(
            agentConfigService.Object,
            compAggregator.Object,
            researchService.Object,
            analysisService.Object,
            pdfGenerator.Object,
            gwsService.Object);

        return (pipeline, agentConfigService, compAggregator, researchService, analysisService, pdfGenerator, gwsService);
    }

    [Fact]
    public async Task Execute_CompletesAllSteps_ForValidInput()
    {
        var lead = TestData.MakeLead(
            firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "3-6 months",
            beds: 3, baths: 2, sqft: 1800);
        var (pipeline, _, compAggregator, researchService, analysisService, pdfGenerator, _) = CreatePipeline();
        var statuses = new List<CmaJobStatus>();

        var job = CmaJob.Create("test-agent", lead);
        await pipeline.ExecuteAsync(job, "test-agent", lead, s => { statuses.Add(s); return Task.CompletedTask; }, CancellationToken.None);

        job.Status.Should().Be(CmaJobStatus.Complete);
        job.CompletedAt.Should().NotBeNull();
        job.Analysis.Should().NotBeNull();
        job.LeadResearch.Should().NotBeNull();
        job.Comps.Should().NotBeEmpty();

        statuses.Should().Contain(CmaJobStatus.SearchingComps);
        statuses.Should().Contain(CmaJobStatus.Analyzing);
        statuses.Should().Contain(CmaJobStatus.GeneratingPdf);
        statuses.Should().Contain(CmaJobStatus.Complete);

        compAggregator.Verify(s => s.FetchCompsAsync(
            It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        researchService.Verify(s => s.ResearchAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Once);
        analysisService.Verify(s => s.AnalyzeAsync(
            It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<LeadResearch?>(),
            It.IsAny<ReportType>(), It.IsAny<CancellationToken>()), Times.Once);
        pdfGenerator.Verify(s => s.Generate(
            It.IsAny<PdfGenerationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_ThrowsInvalidOperationException_ForUnknownAgent()
    {
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

        var lead = TestData.MakeLead(firstName: "Jane", city: "Old Bridge", zip: "08857", beds: 3, baths: 2, sqft: 1800);
        var job = CmaJob.Create("unknown-agent", lead);
        var act = () => pipeline.ExecuteAsync(job, "unknown-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Agent configuration not found for 'unknown-agent'");
    }

    [Fact]
    public async Task Execute_ContinuesWhenDriveFails()
    {
        var lead = TestData.MakeLead(firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "3-6 months", beds: 3, baths: 2, sqft: 1800);
        var (pipeline, _, _, _, _, _, gwsService) = CreatePipeline();

        gwsService.Setup(s => s.CreateDriveFolderAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Drive unavailable"));

        var job = CmaJob.Create("test-agent", lead);
        await pipeline.ExecuteAsync(job, "test-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        job.Status.Should().Be(CmaJobStatus.Complete);
        job.DriveLink.Should().BeNull();
    }

    [Fact]
    public async Task Execute_ContinuesWhenEmailFails()
    {
        var lead = TestData.MakeLead(firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "3-6 months", beds: 3, baths: 2, sqft: 1800);
        var (pipeline, _, _, _, _, _, gwsService) = CreatePipeline();

        gwsService.Setup(s => s.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Email service down"));

        var job = CmaJob.Create("test-agent", lead);
        await pipeline.ExecuteAsync(job, "test-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        job.Status.Should().Be(CmaJobStatus.Complete);
    }

    [Fact]
    public async Task Execute_ContinuesWhenSheetLoggingFails()
    {
        var lead = TestData.MakeLead(firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "3-6 months", beds: 3, baths: 2, sqft: 1800);
        var (pipeline, _, _, _, _, _, gwsService) = CreatePipeline();

        gwsService.Setup(s => s.AppendSheetRowAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Sheets API error"));

        var job = CmaJob.Create("test-agent", lead);
        await pipeline.ExecuteAsync(job, "test-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        job.Status.Should().Be(CmaJobStatus.Complete);
    }

    [Fact]
    public async Task Execute_SkipsSheetLogging_WhenSpreadsheetIdIsEmpty()
    {
        var agentConfig = new AgentConfig
        {
            Id = "test-agent",
            Identity = new AgentIdentity
            {
                Name = "Test Agent",
                Email = "agent@example.com",
                Phone = "555-9999",
                Brokerage = "Test Realty"
            },
            Integrations = new AgentIntegrations { FormHandlerId = "" }
        };

        var lead = TestData.MakeLead(firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "3-6 months", beds: 3, baths: 2, sqft: 1800);
        var (pipeline, _, _, _, _, _, gwsService) = CreatePipeline(agentConfig: agentConfig);

        var job = CmaJob.Create("test-agent", lead);
        await pipeline.ExecuteAsync(job, "test-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        job.Status.Should().Be(CmaJobStatus.Complete);
        gwsService.Verify(s => s.AppendSheetRowAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_SkipsSheetLogging_WhenIntegrationsIsNull()
    {
        var agentConfig = new AgentConfig
        {
            Id = "test-agent",
            Identity = new AgentIdentity
            {
                Name = "Test Agent",
                Email = "agent@example.com",
                Phone = "555-9999",
                Brokerage = "Test Realty"
            },
            Integrations = null
        };

        var lead = TestData.MakeLead(firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "3-6 months", beds: 3, baths: 2, sqft: 1800);
        var (pipeline, _, _, _, _, _, gwsService) = CreatePipeline(agentConfig: agentConfig);

        var job = CmaJob.Create("test-agent", lead);
        await pipeline.ExecuteAsync(job, "test-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        job.Status.Should().Be(CmaJobStatus.Complete);
        gwsService.Verify(s => s.AppendSheetRowAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_UsesNullResearch_WhenResearchReturnsNull()
    {
        var lead = TestData.MakeLead(firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "3-6 months", beds: 3, baths: 2, sqft: 1800);
        var (pipeline, _, _, researchService, _, _, _) = CreatePipeline();

        researchService.Setup(s => s.ResearchAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<LeadResearch>(null!));

        var job = CmaJob.Create("test-agent", lead);
        await pipeline.ExecuteAsync(job, "test-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        job.Status.Should().Be(CmaJobStatus.Complete);
        job.LeadResearch.Should().BeNull();
    }

    [Fact]
    public async Task Execute_PropagatesException_WhenAnalysisFails()
    {
        var lead = TestData.MakeLead(firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "3-6 months", beds: 3, baths: 2, sqft: 1800);
        var (pipeline, _, _, _, analysisService, _, _) = CreatePipeline();

        analysisService.Setup(s => s.AnalyzeAsync(
                It.IsAny<Lead>(), It.IsAny<List<Comp>>(), It.IsAny<LeadResearch?>(),
                It.IsAny<ReportType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Claude API unreachable"));

        var job = CmaJob.Create("test-agent", lead);
        var act = () => pipeline.ExecuteAsync(job, "test-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Execute_UsesEmptyEmail_WhenIdentityIsNull()
    {
        var agentConfig = new AgentConfig
        {
            Id = "test-agent",
            Identity = null,
            Integrations = new AgentIntegrations { FormHandlerId = "sheet-123" }
        };

        var lead = TestData.MakeLead(firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "3-6 months", beds: 3, baths: 2, sqft: 1800);
        var (pipeline, _, _, _, _, _, gwsService) = CreatePipeline(agentConfig: agentConfig);

        var job = CmaJob.Create("test-agent", lead);
        await pipeline.ExecuteAsync(job, "test-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        job.Status.Should().Be(CmaJobStatus.Complete);
        gwsService.Verify(s => s.CreateDriveFolderAsync(
            "", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_BuildsLeadBrief_WithResearchData()
    {
        var research = new LeadResearch
        {
            Occupation = "Engineer",
            Employer = "Acme Corp",
            PurchaseDate = new DateOnly(2019, 6, 1),
            PurchasePrice = 300_000m,
            TaxAssessment = 380_000m,
            AnnualPropertyTax = 8_500m,
            YearBuilt = 1995,
            LotSize = 0.25m,
            LotSizeUnit = "acres",
            EstimatedEquityLow = 100_000m,
            EstimatedEquityHigh = 150_000m,
            LifeEventInsight = "Relocating for new job"
        };

        var lead = TestData.MakeLead(firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "ASAP", beds: 3, baths: 2, sqft: 1800);
        var (pipeline, _, _, _, _, _, gwsService) = CreatePipeline(research: research);

        string? capturedDocContent = null;
        gwsService.Setup(s => s.CreateDocAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, CancellationToken>((_, _, _, content, _) =>
            {
                capturedDocContent = content;
            })
            .ReturnsAsync("doc-id");

        var job = CmaJob.Create("test-agent", lead);
        await pipeline.ExecuteAsync(job, "test-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        capturedDocContent.Should().NotBeNull();
        capturedDocContent.Should().Contain("Jane Doe");
        capturedDocContent.Should().Contain("Engineer");
    }

    [Fact]
    public async Task Execute_SetsJobDriveLink_WhenUploadSucceeds()
    {
        var lead = TestData.MakeLead(firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "3-6 months", beds: 3, baths: 2, sqft: 1800);
        var (pipeline, _, _, _, _, _, _) = CreatePipeline();

        var job = CmaJob.Create("test-agent", lead);
        await pipeline.ExecuteAsync(job, "test-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        job.DriveLink.Should().Be("https://drive.google.com/file/abc123");
    }

    [Fact]
    public async Task Execute_PropagatesException_WhenCompFetchFails()
    {
        var lead = TestData.MakeLead(firstName: "Jane", lastName: "Doe", email: "jane@example.com",
            phone: "555-1234", address: "123 Main St", city: "Old Bridge",
            state: "NJ", zip: "08857", timeline: "3-6 months", beds: 3, baths: 2, sqft: 1800);
        var (pipeline, _, compAggregator, _, _, _, _) = CreatePipeline();

        compAggregator.Setup(s => s.FetchCompsAsync(
                It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("All comp sources failed"));

        var job = CmaJob.Create("test-agent", lead);
        var act = () => pipeline.ExecuteAsync(job, "test-agent", lead, _ => Task.CompletedTask, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
