using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Activities.Persist;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Services.AgentNotifier;
using RealEstateStar.Services.LeadCommunicator;
using RealEstateStar.Activities.Pdf;
using RealEstateStar.TestUtilities;


namespace RealEstateStar.Workers.Lead.Orchestrator.Tests;

public sealed class LeadOrchestratorTests
{
    // ── mocks ────────────────────────────────────────────────────────────────
    private readonly Mock<ILeadStore> _leadStoreMock = new();
    private readonly Mock<IAccountConfigService> _accountConfigMock = new();
    private readonly Mock<ILeadScorer> _scorerMock = new();
    private readonly Mock<ILeadEmailDrafter> _emailDrafterMock = new();
    private readonly Mock<IGmailSender> _gmailMock = new();
    private readonly Mock<IWhatsAppSender> _whatsAppMock = new();
    private readonly Mock<ICmaPdfGenerator> _pdfGeneratorMock = new();
    private readonly Mock<IPdfDataService> _pdfDataServiceMock = new();

    // ── channels + infrastructure ────────────────────────────────────────────
    private readonly LeadOrchestratorChannel _orchestratorChannel = new();
    private readonly CmaProcessingChannel _cmaChannel = new();
    private readonly HomeSearchProcessingChannel _hsChannel = new();
    private readonly BackgroundServiceHealthTracker _healthTracker = new();

    // ── builder ──────────────────────────────────────────────────────────────

    private LeadOrchestrator BuildOrchestrator(int? timeoutSeconds = null, IContentCache? contentCache = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(timeoutSeconds.HasValue
                ? new Dictionary<string, string?> { ["Pipeline:Lead:WorkerTimeoutSeconds"] = timeoutSeconds.Value.ToString() }
                : [])
            .Build();

        var pdfActivity = new PdfActivity(
            _pdfGeneratorMock.Object,
            _pdfDataServiceMock.Object,
            NullLogger<PdfActivity>.Instance);

        var persistStorage = new Mock<IDocumentStorageProvider>();
        persistStorage
            .Setup(s => s.ReadDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        persistStorage
            .Setup(s => s.WriteDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        persistStorage
            .Setup(s => s.EnsureFolderExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var persistActivity = new PersistActivity(
            persistStorage.Object,
            _leadStoreMock.Object,
            NullLogger<PersistActivity>.Instance);

        // Use a real FakeContentCache when none is provided (null-returning mock is only for non-cache-hit tests)
        contentCache ??= new FakeContentCache();

        var communicationService = new LeadCommunicatorService(
            _emailDrafterMock.Object,
            _gmailMock.Object,
            NullLogger<LeadCommunicatorService>.Instance);

        var agentNotificationService = new AgentNotifierService(
            _whatsAppMock.Object,
            _gmailMock.Object,
            NullLogger<AgentNotifierService>.Instance);

        return new LeadOrchestrator(
            _orchestratorChannel,
            _accountConfigMock.Object,
            _scorerMock.Object,
            _cmaChannel,
            _hsChannel,
            pdfActivity,
            persistActivity,
            communicationService,
            agentNotificationService,
            contentCache,
            _healthTracker,
            NullLogger<LeadOrchestrator>.Instance,
            config);
    }

    // ── test data ─────────────────────────────────────────────────────────────

    private static RealEstateStar.Domain.Leads.Models.Lead BuildSellerLead(string agentId = "agent-1", int submissionCount = 1) => new()
    {
        Id = Guid.NewGuid(),
        AgentId = agentId,
        LeadType = LeadType.Seller,
        FirstName = "Alice",
        LastName = "Seller",
        Email = "alice@example.com",
        Phone = "555-0001",
        Timeline = "asap",
        Status = LeadStatus.Received,
        ReceivedAt = DateTime.UtcNow,
        SubmissionCount = submissionCount,
        SellerDetails = new SellerDetails
        {
            Address = "10 Pine St",
            City = "Newark",
            State = "NJ",
            Zip = "07101",
            Beds = 3,
            Baths = 2,
            Sqft = 1800
        }
    };

    private static RealEstateStar.Domain.Leads.Models.Lead BuildBuyerLead(string agentId = "agent-1", int submissionCount = 1) => new()
    {
        Id = Guid.NewGuid(),
        AgentId = agentId,
        LeadType = LeadType.Buyer,
        FirstName = "Bob",
        LastName = "Buyer",
        Email = "bob@example.com",
        Phone = "555-0002",
        Timeline = "3-6months",
        Status = LeadStatus.Received,
        ReceivedAt = DateTime.UtcNow,
        SubmissionCount = submissionCount,
        BuyerDetails = new BuyerDetails
        {
            City = "Newark",
            State = "NJ",
            MinBudget = 400_000m,
            MaxBudget = 550_000m,
            PreApproved = "yes"
        }
    };

    private static RealEstateStar.Domain.Leads.Models.Lead BuildBothLead(string agentId = "agent-1") => new()
    {
        Id = Guid.NewGuid(),
        AgentId = agentId,
        LeadType = LeadType.Both,
        FirstName = "Carol",
        LastName = "Both",
        Email = "carol@example.com",
        Phone = "555-0003",
        Timeline = "1-3months",
        Status = LeadStatus.Received,
        ReceivedAt = DateTime.UtcNow,
        SellerDetails = new SellerDetails
        {
            Address = "20 Oak Ave",
            City = "Jersey City",
            State = "NJ",
            Zip = "07302",
            Beds = 4,
            Baths = 2,
            Sqft = 2000
        },
        BuyerDetails = new BuyerDetails
        {
            City = "Hoboken",
            State = "NJ",
            MinBudget = 600_000m,
            MaxBudget = 800_000m
        }
    };

    private static AccountConfig BuildAccountConfig(string agentId = "agent-1") => new()
    {
        Handle = agentId,
        Agent = new AccountAgent { Name = "Jenise Buckalew", Email = "jenise@example.com", Phone = "555-9999", LicenseNumber = "NJ-12345" },
        Brokerage = new AccountBrokerage { Name = "Star Realty" },
        Branding = new AccountBranding { PrimaryColor = "#1A3C5E", AccentColor = "#D4A853" },
        Location = new AccountLocation { State = "NJ", ServiceAreas = ["Newark", "Jersey City"] },
        Integrations = new AccountIntegrations { WhatsApp = new AccountWhatsApp { PhoneNumber = "+15550001234" } }
    };

    private static LeadScore BuildScore() => new()
    {
        OverallScore = 75,
        Factors = [],
        Explanation = "Hot seller lead — asap timeline, score 75/100"
    };

    private static CmaWorkerResult BuildCmaResult(string leadId) => new(
        LeadId: leadId,
        Success: true,
        Error: null,
        EstimatedValue: 500_000m,
        PriceRangeLow: 480_000m,
        PriceRangeHigh: 520_000m,
        Comps: [new CompSummary("123 Main St", 490_000m, 3, 2m, 1800, 14, 0.3, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)))],
        MarketAnalysis: "Market trending upward.");

    private static HomeSearchWorkerResult BuildHsResult(string leadId) => new(
        LeadId: leadId,
        Success: true,
        Error: null,
        Listings: [new ListingSummary("456 Oak Ave", 510_000m, 3, 2m, 1900, "Active", null)],
        AreaSummary: "Good inventory available.");

    private static LeadEmail BuildEmailDraft() =>
        new("Subject: Help with your home", "<p>Hello</p>", PdfAttachmentPath: null);

    // ── setup helpers ──────────────────────────────────────────────────────────

    private void SetupAccountConfig(string agentId = "agent-1") =>
        _accountConfigMock
            .Setup(s => s.GetAccountAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccountConfig(agentId));

    private void SetupScorer() =>
        _scorerMock
            .Setup(s => s.Score(It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>()))
            .Returns(BuildScore());

    private void SetupEmailDrafter() =>
        _emailDrafterMock
            .Setup(d => d.DraftAsync(
                It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>(), It.IsAny<LeadScore>(),
                It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
                It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmailDraft());

    private void SetupGmail() =>
        _gmailMock
            .Setup(g => g.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    private void SetupWhatsApp() =>
        _whatsAppMock
            .Setup(w => w.SendTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

    private void SetupLeadStore() =>
        _leadStoreMock
            .Setup(s => s.UpdateStatusAsync(It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>(), It.IsAny<LeadStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    private void SetupPdfGenerator()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllBytes(tempFile, [0x25, 0x50, 0x44, 0x46]); // %PDF magic bytes
        _pdfGeneratorMock
            .Setup(g => g.GenerateAsync(
                It.IsAny<Domain.Leads.Models.Lead>(),
                It.IsAny<Domain.Cma.Models.CmaAnalysis>(),
                It.IsAny<List<Domain.Cma.Models.Comp>>(),
                It.IsAny<AccountConfig>(),
                It.IsAny<Domain.Cma.Models.ReportType>(),
                It.IsAny<byte[]?>(),
                It.IsAny<byte[]?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tempFile);

        _pdfDataServiceMock
            .Setup(s => s.StorePdfAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Real Estate Star/1 - Leads/Test Lead/CMA/2026-01-01-test-CMA-Report.pdf.b64");
    }

    /// <summary>Runs a background task that auto-resolves CMA channel requests.</summary>
    private Task AutoResolveCmaChannelAsync(CancellationToken ct = default) =>
        Task.Run(async () =>
        {
            await foreach (var req in _cmaChannel.Reader.ReadAllAsync(ct))
            {
                req.Completion.TrySetResult(BuildCmaResult(req.Lead.Id.ToString()));
                return;
            }
        }, ct);

    /// <summary>Runs a background task that auto-resolves HomeSearch channel requests.</summary>
    private Task AutoResolveHsChannelAsync(CancellationToken ct = default) =>
        Task.Run(async () =>
        {
            await foreach (var req in _hsChannel.Reader.ReadAllAsync(ct))
            {
                req.Completion.TrySetResult(BuildHsResult(req.Lead.Id.ToString()));
                return;
            }
        }, ct);

    // ═══════════════════════════════════════════════════════════════════════════
    // Dispatch logic — by lead type
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SellerLead_DispatchesCmaChannel_NotHomeSearch()
    {
        // Arrange
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 10);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();
        SetupPdfGenerator();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cmaResolver = AutoResolveCmaChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-seller-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);
        await cmaResolver.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        _cmaChannel.Count.Should().Be(0, "CMA request was consumed");
        _hsChannel.Count.Should().Be(0, "HomeSearch not dispatched for seller-only");
    }

    [Fact]
    public async Task BuyerLead_DispatchesHomeSearchChannel_NotCma()
    {
        // Arrange
        var lead = BuildBuyerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 10);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var hsResolver = AutoResolveHsChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-buyer-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);
        await hsResolver.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        _cmaChannel.Count.Should().Be(0, "CMA not dispatched for buyer-only");
        _hsChannel.Count.Should().Be(0, "HomeSearch request was consumed");
    }

    [Fact]
    public async Task BothLead_DispatchesCmaAndHomeSearch()
    {
        // Arrange
        var lead = BuildBothLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 10);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();
        SetupPdfGenerator();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cmaResolver = AutoResolveCmaChannelAsync(cts.Token);
        var hsResolver = AutoResolveHsChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-both-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);
        await Task.WhenAll(cmaResolver, hsResolver).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        _cmaChannel.Count.Should().Be(0, "CMA request was consumed");
        _hsChannel.Count.Should().Be(0, "HomeSearch request was consumed");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PdfActivity is called (not PdfProcessingChannel)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SellerLead_WithSuccessfulCmaResult_CallsPdfActivity()
    {
        // Arrange
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 10);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();
        SetupPdfGenerator();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _ = AutoResolveCmaChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-pdf-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert — PdfGenerator was called (PdfActivity called it internally)
        _pdfGeneratorMock.Verify(g => g.GenerateAsync(
            It.IsAny<Domain.Leads.Models.Lead>(),
            It.IsAny<Domain.Cma.Models.CmaAnalysis>(),
            It.IsAny<List<Domain.Cma.Models.Comp>>(),
            It.IsAny<AccountConfig>(),
            It.IsAny<Domain.Cma.Models.ReportType>(),
            It.IsAny<byte[]?>(),
            It.IsAny<byte[]?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuyerLead_NoCmaResult_PdfActivityNotCalled()
    {
        // Arrange
        var lead = BuildBuyerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 10);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _ = AutoResolveHsChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-no-pdf-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert — PDF generator not called because no CMA result
        _pdfGeneratorMock.Verify(g => g.GenerateAsync(
            It.IsAny<Domain.Leads.Models.Lead>(),
            It.IsAny<Domain.Cma.Models.CmaAnalysis>(),
            It.IsAny<List<Domain.Cma.Models.Comp>>(),
            It.IsAny<AccountConfig>(),
            It.IsAny<Domain.Cma.Models.ReportType>(),
            It.IsAny<byte[]?>(),
            It.IsAny<byte[]?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // LeadCommunicatorService.DraftAsync + SendAsync called in sequence
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task BuyerLead_EmailDrafterAndGmailSender_CalledInSequence()
    {
        // Arrange
        var lead = BuildBuyerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 10);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _ = AutoResolveHsChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-email-seq-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert — drafter was called (draft step)
        _emailDrafterMock.Verify(d => d.DraftAsync(
            It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>(), It.IsAny<LeadScore>(),
            It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
            It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — gmail was called for lead email (send step)
        _gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), lead.Email,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuyerLead_EmailDraftFails_PipelineStillCompletes()
    {
        // Arrange
        var lead = BuildBuyerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 10);

        SetupAccountConfig();
        SetupScorer();

        _emailDrafterMock
            .Setup(d => d.DraftAsync(
                It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>(), It.IsAny<LeadScore>(),
                It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
                It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Claude unavailable"));

        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _ = AutoResolveHsChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-draft-fail-001");

        // Act — should not throw
        var act = async () => await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert
        await act.Should().NotThrowAsync();

        // Lead store should still be called for status updates
        _leadStoreMock.Verify(s => s.UpdateStatusAsync(lead, It.IsAny<LeadStatus>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AgentNotifierService called
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SellerLead_AgentNotifierService_Called()
    {
        // Arrange
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 10);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();
        SetupPdfGenerator();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _ = AutoResolveCmaChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-notify-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert — WhatsApp or Gmail called for agent notification
        // AgentNotifierService tries WhatsApp first, then Gmail as fallback
        var whatsAppCalled = _whatsAppMock.Invocations.Count > 0;
        var gmailCalledForNotification = _gmailMock.Invocations.Count > 1; // first call is lead email, second would be agent notification

        (whatsAppCalled || gmailCalledForNotification).Should().BeTrue(
            "AgentNotifierService must attempt to notify the agent via WhatsApp or email");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Timeout handling
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SellerLead_CmaTimeout_PipelineCompletesWithoutPdf()
    {
        // Arrange — very short timeout, no resolver running
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 1);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-cma-timeout-001");

        // Act — CMA channel has no consumer, timeout should fire
        var act = async () => await orchestrator.ProcessRequestAsync(request, CancellationToken.None);

        // Assert — pipeline does not throw; timeout is handled gracefully
        await act.Should().NotThrowAsync();

        // PDF not called because CMA timed out
        _pdfGeneratorMock.Verify(g => g.GenerateAsync(
            It.IsAny<Domain.Leads.Models.Lead>(),
            It.IsAny<Domain.Cma.Models.CmaAnalysis>(),
            It.IsAny<List<Domain.Cma.Models.Comp>>(),
            It.IsAny<AccountConfig>(),
            It.IsAny<Domain.Cma.Models.ReportType>(),
            It.IsAny<byte[]?>(),
            It.IsAny<byte[]?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BuyerLead_HomeSearchTimeout_PipelineCompletesWithoutListings()
    {
        // Arrange — very short timeout, no resolver running
        var lead = BuildBuyerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 1);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-hs-timeout-001");

        // Act — HomeSearch channel has no consumer, timeout should fire
        var act = async () => await orchestrator.ProcessRequestAsync(request, CancellationToken.None);

        // Assert — pipeline does not throw
        await act.Should().NotThrowAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // LeadPipelineContext populated correctly after each step
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ProcessRequest_AgentConfigNull_EarlyReturn_NoFurtherProcessing()
    {
        // Arrange
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator();

        _accountConfigMock
            .Setup(s => s.GetAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountConfig?)null);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-no-config-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, CancellationToken.None);

        // Assert — scorer never called (early return before scoring)
        _scorerMock.Verify(s => s.Score(It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>()), Times.Never);
    }

    [Fact]
    public async Task ProcessRequest_ScorerCalled_LeadScoreSetOnLead()
    {
        // Arrange
        var lead = BuildBuyerLead();
        var expectedScore = BuildScore();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 10);

        SetupAccountConfig();
        _scorerMock.Setup(s => s.Score(It.IsAny<RealEstateStar.Domain.Leads.Models.Lead>())).Returns(expectedScore);
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _ = AutoResolveHsChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-scorer-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert — lead.Score set by orchestrator after scoring step
        lead.Score.Should().NotBeNull();
        lead.Score!.OverallScore.Should().Be(expectedScore.OverallScore);
    }

    [Fact]
    public async Task ProcessRequest_StatusProgression_ScoredAnalyzingComplete()
    {
        // Arrange
        var lead = BuildBuyerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 10);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _ = AutoResolveHsChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-status-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert — status update sequence: Scored → Analyzing → Complete
        // Scored and Analyzing are written inline via persistActivity.PersistStatusAsync (concurrency gates).
        // Complete is written inside persistActivity.ExecuteAsync at end of pipeline.
        // All three flow through _leadStoreMock because PersistActivity wraps ILeadStore.
        _leadStoreMock.Verify(s => s.UpdateStatusAsync(lead, LeadStatus.Scored, It.IsAny<CancellationToken>()), Times.Once);
        _leadStoreMock.Verify(s => s.UpdateStatusAsync(lead, LeadStatus.Analyzing, It.IsAny<CancellationToken>()), Times.Once);
        _leadStoreMock.Verify(s => s.UpdateStatusAsync(lead, LeadStatus.Complete, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRequest_HealthTracker_RecordedAfterCompletion()
    {
        // Arrange
        var lead = BuildBuyerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 10);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _ = AutoResolveHsChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-health-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert
        _healthTracker.GetLastActivity("LeadOrchestrator").Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BuildAgentNotificationConfig helper
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void BuildAgentNotificationConfig_MapsAllFieldsCorrectly()
    {
        // Arrange
        var config = BuildAccountConfig("jenise-buckalew");

        // Act
        var result = LeadOrchestrator.BuildAgentNotificationConfig("jenise-buckalew", config);

        // Assert
        result.AgentId.Should().Be("jenise-buckalew");
        result.Handle.Should().Be("jenise-buckalew");
        result.Name.Should().Be("Jenise Buckalew");
        result.FirstName.Should().Be("Jenise");
        result.Email.Should().Be("jenise@example.com");
        result.Phone.Should().Be("555-9999");
        result.LicenseNumber.Should().Be("NJ-12345");
        result.BrokerageName.Should().Be("Star Realty");
        result.PrimaryColor.Should().Be("#1A3C5E");
        result.AccentColor.Should().Be("#D4A853");
        result.State.Should().Be("NJ");
        result.ServiceAreas.Should().Contain("Newark");
        result.WhatsAppPhoneNumberId.Should().Be("+15550001234");
    }

    [Fact]
    public void BuildAgentNotificationConfig_WithNullOptionalFields_UsesDefaults()
    {
        // Arrange
        var sparse = new AccountConfig { Handle = "sparse-agent" };

        // Act
        var result = LeadOrchestrator.BuildAgentNotificationConfig("sparse-agent", sparse);

        // Assert
        result.Name.Should().Be("");
        result.Email.Should().Be("");
        result.PrimaryColor.Should().Be("#000000");
        result.AccentColor.Should().Be("#000000");
        result.State.Should().Be("");
        result.ServiceAreas.Should().BeEmpty();
        result.WhatsAppPhoneNumberId.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DispatchWorkers unit test
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DispatchWorkers_SellerLead_WritesCmaOnly()
    {
        // Arrange
        var lead = BuildSellerLead();
        var config = BuildAccountConfig();
        var agentConfig = LeadOrchestrator.BuildAgentNotificationConfig("agent-1", config);
        var orchestrator = BuildOrchestrator();

        // Act
        var (cmaTcs, hsTcs) = orchestrator.DispatchWorkers(lead, "agent-1", agentConfig, "corr-disp-001");

        // Assert
        ((object?)cmaTcs).Should().NotBeNull();
        ((object?)hsTcs).Should().BeNull();
        _cmaChannel.Count.Should().Be(1);
        _hsChannel.Count.Should().Be(0);
    }

    [Fact]
    public void DispatchWorkers_BuyerLead_WritesHomeSearchOnly()
    {
        // Arrange
        var lead = BuildBuyerLead();
        var config = BuildAccountConfig();
        var agentConfig = LeadOrchestrator.BuildAgentNotificationConfig("agent-1", config);
        var orchestrator = BuildOrchestrator();

        // Act
        var (cmaTcs, hsTcs) = orchestrator.DispatchWorkers(lead, "agent-1", agentConfig, "corr-disp-002");

        // Assert
        ((object?)cmaTcs).Should().BeNull();
        ((object?)hsTcs).Should().NotBeNull();
        _cmaChannel.Count.Should().Be(0);
        _hsChannel.Count.Should().Be(1);
    }

    [Fact]
    public void DispatchWorkers_BothLead_WritesBothChannels()
    {
        // Arrange
        var lead = BuildBothLead();
        var config = BuildAccountConfig();
        var agentConfig = LeadOrchestrator.BuildAgentNotificationConfig("agent-1", config);
        var orchestrator = BuildOrchestrator();

        // Act
        var (cmaTcs, hsTcs) = orchestrator.DispatchWorkers(lead, "agent-1", agentConfig, "corr-disp-003");

        // Assert
        ((object?)cmaTcs).Should().NotBeNull();
        ((object?)hsTcs).Should().NotBeNull();
        _cmaChannel.Count.Should().Be(1);
        _hsChannel.Count.Should().Be(1);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ContentHash helper tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ComputeCmaInputHash_SameSellerDetails_ReturnsSameHash()
    {
        var lead1 = BuildSellerLead();
        var lead2 = BuildSellerLead();
        // Set same details explicitly
        lead2.SellerDetails!.GetType();

        var hash1 = LeadOrchestrator.ComputeCmaInputHash(lead1);
        var hash2 = LeadOrchestrator.ComputeCmaInputHash(lead2);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHsInputHash_SameBuyerDetails_ReturnsSameHash()
    {
        var lead1 = BuildBuyerLead();
        var lead2 = BuildBuyerLead();

        var hash1 = LeadOrchestrator.ComputeHsInputHash(lead1);
        var hash2 = LeadOrchestrator.ComputeHsInputHash(lead2);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeCmaInputHash_DifferentAddress_ReturnsDifferentHash()
    {
        var lead1 = BuildSellerLead();
        var lead2 = new Domain.Leads.Models.Lead
        {
            Id = Guid.NewGuid(),
            AgentId = "agent-1",
            LeadType = LeadType.Seller,
            FirstName = "Alice",
            LastName = "Seller",
            Email = "alice@example.com",
            Phone = "555-0001",
            Timeline = "asap",
            Status = LeadStatus.Received,
            ReceivedAt = DateTime.UtcNow,
            SellerDetails = new SellerDetails
            {
                Address = "99 Different Blvd", // Different address
                City = "Newark",
                State = "NJ",
                Zip = "07101"
            }
        };

        var hash1 = LeadOrchestrator.ComputeCmaInputHash(lead1);
        var hash2 = LeadOrchestrator.ComputeCmaInputHash(lead2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputePdfInputHash_NullCmaResult_ReturnsConsistentHash()
    {
        var hash1 = LeadOrchestrator.ComputePdfInputHash(null);
        var hash2 = LeadOrchestrator.ComputePdfInputHash(null);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputePdfInputHash_SameCmaResult_ReturnsSameHash()
    {
        var leadId = Guid.NewGuid().ToString();
        var cma1 = BuildCmaResult(leadId);
        var cma2 = BuildCmaResult(leadId);

        var hash1 = LeadOrchestrator.ComputePdfInputHash(cma1);
        var hash2 = LeadOrchestrator.ComputePdfInputHash(cma2);

        hash1.Should().Be(hash2);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Retry state — CMA skip
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SellerLead_WithCompletedCmaRetryState_SkipsCmaDispatch()
    {
        // Arrange — lead already has RetryState showing CMA complete for this address
        var lead = BuildSellerLead();
        var cmaHash = LeadOrchestrator.ComputeCmaInputHash(lead);
        lead.RetryState = new LeadRetryState
        {
            CompletedActivityKeys = { ["cma"] = cmaHash },
            CompletedResultPaths = { ["cma"] = "path/to/cma" }
        };

        var orchestrator = BuildOrchestrator(timeoutSeconds: 5);
        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-retry-cma-001");

        // Act — no CMA channel resolver, but retry state says CMA is done
        var act = async () => await orchestrator.ProcessRequestAsync(request, CancellationToken.None);

        await act.Should().NotThrowAsync("pipeline should handle skipped CMA gracefully");

        // Assert — CMA channel was NOT dispatched
        _cmaChannel.Count.Should().Be(0, "CMA was skipped via retry state");
    }

    [Fact]
    public async Task BuyerLead_WithCompletedHsRetryState_SkipsHomeSearchDispatch()
    {
        // Arrange — lead already has RetryState showing HS complete
        var lead = BuildBuyerLead();
        var hsHash = LeadOrchestrator.ComputeHsInputHash(lead);
        lead.RetryState = new LeadRetryState
        {
            CompletedActivityKeys = { ["homeSearch"] = hsHash },
            CompletedResultPaths = { ["homeSearch"] = "path/to/hs" }
        };

        var orchestrator = BuildOrchestrator(timeoutSeconds: 5);
        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-retry-hs-001");

        // Act
        var act = async () => await orchestrator.ProcessRequestAsync(request, CancellationToken.None);

        await act.Should().NotThrowAsync();

        // Assert — HS channel was NOT dispatched
        _hsChannel.Count.Should().Be(0, "HomeSearch was skipped via retry state");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Content cache — cross-lead CMA hit
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SellerLead_CmaCacheHit_SkipsChannelDispatch()
    {
        // Arrange — pre-populate the content cache with a CMA result for this address
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 5);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();
        SetupPdfGenerator();

        // Pre-populate cache using the orchestrator's own cache instance via the request
        // We build a first lead that runs through the pipeline and caches the result
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cmaResolver = AutoResolveCmaChannelAsync(cts.Token);

        var request1 = new LeadOrchestrationRequest("agent-1", lead, "corr-cache-1");
        await orchestrator.ProcessRequestAsync(request1, cts.Token);
        await cmaResolver.WaitAsync(TimeSpan.FromSeconds(5));

        // Second lead — same address, different lead instance but same seller details
        var lead2 = new Domain.Leads.Models.Lead
        {
            Id = Guid.NewGuid(),
            AgentId = "agent-1",
            LeadType = LeadType.Seller,
            FirstName = "Bob",
            LastName = "Also-Seller",
            Email = "bob2@example.com",
            Phone = "555-9999",
            Timeline = "asap",
            Status = LeadStatus.Received,
            ReceivedAt = DateTime.UtcNow,
            SellerDetails = lead.SellerDetails  // SAME property address → cache hit
        };

        _leadStoreMock.Reset();
        SetupLeadStore();
        _accountConfigMock.Reset();
        SetupAccountConfig();
        _scorerMock.Reset();
        SetupScorer();
        _emailDrafterMock.Reset();
        SetupEmailDrafter();
        _gmailMock.Reset();
        SetupGmail();
        _whatsAppMock.Reset();
        SetupWhatsApp();

        var request2 = new LeadOrchestrationRequest("agent-1", lead2, "corr-cache-2");
        await orchestrator.ProcessRequestAsync(request2, CancellationToken.None);

        // Assert — CMA channel should be empty (cache hit, no dispatch needed)
        _cmaChannel.Count.Should().Be(0, "second lead reuses cached CMA result");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PricingStrategy flows from CmaWorkerResult into CmaAnalysis for PDF
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SellerLead_PricingStrategyFromCmaResult_PassedToPdfGenerator()
    {
        // Arrange — CMA worker returns a result with a PricingStrategy
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 10);
        const string expectedStrategy = "List at $499,000 to generate multiple offers.";

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupWhatsApp();
        SetupLeadStore();

        Domain.Cma.Models.CmaAnalysis? capturedAnalysis = null;
        var tempFile = Path.GetTempFileName();
        File.WriteAllBytes(tempFile, [0x25, 0x50, 0x44, 0x46]);
        _pdfGeneratorMock
            .Setup(g => g.GenerateAsync(
                It.IsAny<Domain.Leads.Models.Lead>(),
                It.IsAny<Domain.Cma.Models.CmaAnalysis>(),
                It.IsAny<List<Domain.Cma.Models.Comp>>(),
                It.IsAny<AccountConfig>(),
                It.IsAny<Domain.Cma.Models.ReportType>(),
                It.IsAny<byte[]?>(),
                It.IsAny<byte[]?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Domain.Leads.Models.Lead, Domain.Cma.Models.CmaAnalysis, List<Domain.Cma.Models.Comp>,
                AccountConfig, Domain.Cma.Models.ReportType, byte[]?, byte[]?, CancellationToken>(
                (_, analysis, _, _, _, _, _, _) => capturedAnalysis = analysis)
            .ReturnsAsync(tempFile);

        _pdfDataServiceMock
            .Setup(s => s.StorePdfAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("path/to/cma.pdf");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Auto-resolve CMA channel with a result that includes PricingStrategy
        _ = Task.Run(async () =>
        {
            await foreach (var req in _cmaChannel.Reader.ReadAllAsync(cts.Token))
            {
                req.Completion.TrySetResult(new CmaWorkerResult(
                    LeadId: req.Lead.Id.ToString(),
                    Success: true,
                    Error: null,
                    EstimatedValue: 500_000m,
                    PriceRangeLow: 480_000m,
                    PriceRangeHigh: 520_000m,
                    Comps: [],
                    MarketAnalysis: "Strong market.",
                    PricingStrategy: expectedStrategy));
                return;
            }
        }, cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-pricing-strategy-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert — PricingStrategy must be on the CmaAnalysis passed to PdfGenerator
        capturedAnalysis.Should().NotBeNull();
        capturedAnalysis!.PricingStrategy.Should().Be(expectedStrategy);
    }
}
