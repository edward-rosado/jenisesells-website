using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Workers.Cma;
using RealEstateStar.Workers.HomeSearch;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Leads.Tests;

/// <summary>
/// End-to-end integration tests for the lead pipeline. These tests validate full
/// orchestration flows: dispatching CMA and HomeSearch workers, collecting results,
/// generating PDFs, sending email, and notifying the agent. Workers are simulated
/// via Task.Run background resolvers that consume channel requests and set results.
/// </summary>
public sealed class LeadPipelineIntegrationTests
{
    // ───────────────────────── mocks ─────────────────────────
    private readonly Mock<ILeadStore> _leadStoreMock = new();
    private readonly Mock<IAccountConfigService> _accountConfigMock = new();
    private readonly Mock<ILeadScorer> _scorerMock = new();
    private readonly Mock<ILeadEmailDrafter> _emailDrafterMock = new();
    private readonly Mock<IGmailSender> _gmailMock = new();
    private readonly Mock<IAgentNotifier> _agentNotifierMock = new();
    private readonly Mock<IDocumentStorageProvider> _documentStorageMock = new();

    // ───────────────────────── channels ─────────────────────────
    private readonly LeadOrchestratorChannel _orchestratorChannel = new();
    private readonly CmaProcessingChannel _cmaChannel = new();
    private readonly HomeSearchProcessingChannel _hsChannel = new();
    private readonly PdfProcessingChannel _pdfChannel = new();
    private readonly BackgroundServiceHealthTracker _healthTracker = new();

    // ───────────────────────── builder ─────────────────────────

    private LeadOrchestrator BuildOrchestrator(int? timeoutSeconds = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(timeoutSeconds.HasValue
                ? new Dictionary<string, string?> { ["Pipeline:Lead:WorkerTimeoutSeconds"] = timeoutSeconds.Value.ToString() }
                : [])
            .Build();

        return new LeadOrchestrator(
            _orchestratorChannel,
            _leadStoreMock.Object,
            _accountConfigMock.Object,
            _scorerMock.Object,
            _cmaChannel,
            _hsChannel,
            _pdfChannel,
            _emailDrafterMock.Object,
            _gmailMock.Object,
            _agentNotifierMock.Object,
            _healthTracker,
            NullLogger<LeadOrchestrator>.Instance,
            config);
    }

    // ───────────────────────── domain builders ─────────────────────────

    private static Lead BuildSellerLead(string agentId = "agent-1") => new()
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

    private static Lead BuildBuyerLead(string agentId = "agent-1") => new()
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
        BuyerDetails = new BuyerDetails
        {
            City = "Newark",
            State = "NJ",
            MinBudget = 400_000m,
            MaxBudget = 550_000m,
            PreApproved = "yes"
        }
    };

    private static Lead BuildBothLead(string agentId = "agent-1") => new()
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
        Agent = new AccountAgent
        {
            Name = "Jenise Buckalew",
            Email = "jenise@example.com",
            Phone = "555-9999",
            LicenseNumber = "NJ-12345"
        },
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
        Comps: [new CompSummary("123 Main St", 490_000m, 3, 2m, 1800, 14, 0.3)],
        MarketAnalysis: "Market trending upward.");

    private static HomeSearchWorkerResult BuildHsResult(string leadId) => new(
        LeadId: leadId,
        Success: true,
        Error: null,
        Listings: [new ListingSummary("456 Oak Ave", 510_000m, 3, 2m, 1900, "Active", null)],
        AreaSummary: "Good inventory available.");

    private static LeadEmail BuildEmailDraft() =>
        new("Subject: Help with your home", "<p>Hello</p>", PdfAttachmentPath: null);

    // ───────────────────────── setup helpers ─────────────────────────

    private void SetupAll(string agentId = "agent-1")
    {
        _accountConfigMock
            .Setup(s => s.GetAccountAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccountConfig(agentId));

        _scorerMock
            .Setup(s => s.Score(It.IsAny<Lead>()))
            .Returns(BuildScore());

        _emailDrafterMock
            .Setup(d => d.DraftAsync(
                It.IsAny<Lead>(), It.IsAny<LeadScore>(),
                It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
                It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmailDraft());

        _gmailMock
            .Setup(g => g.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _agentNotifierMock
            .Setup(n => n.NotifyAsync(
                It.IsAny<Lead>(), It.IsAny<LeadScore>(),
                It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
                It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _leadStoreMock
            .Setup(s => s.UpdateStatusAsync(It.IsAny<Lead>(), It.IsAny<LeadStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _documentStorageMock
            .Setup(s => s.WriteDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ───────────────────────── background worker simulators ─────────────────────────

    /// <summary>
    /// Simulates a CMA worker: reads one request from the channel and resolves its TCS with a success result.
    /// </summary>
    private Task SimulateCmaWorkerAsync(CancellationToken ct = default) =>
        Task.Run(async () =>
        {
            await foreach (var req in _cmaChannel.Reader.ReadAllAsync(ct))
            {
                req.Completion.TrySetResult(BuildCmaResult(req.Lead.Id.ToString()));
                return;
            }
        }, ct);

    /// <summary>
    /// Simulates a HomeSearch worker: reads one request and resolves its TCS with a success result.
    /// </summary>
    private Task SimulateHomeSearchWorkerAsync(CancellationToken ct = default) =>
        Task.Run(async () =>
        {
            await foreach (var req in _hsChannel.Reader.ReadAllAsync(ct))
            {
                req.Completion.TrySetResult(BuildHsResult(req.Lead.Id.ToString()));
                return;
            }
        }, ct);

    /// <summary>
    /// Simulates a PDF worker: reads one request from the channel and resolves its TCS with a success result.
    /// </summary>
    private Task SimulatePdfWorkerAsync(CancellationToken ct = default) =>
        Task.Run(async () =>
        {
            await foreach (var req in _pdfChannel.Reader.ReadAllAsync(ct))
            {
                req.Completion.TrySetResult(new PdfWorkerResult(
                    req.LeadId, Success: true, Error: null,
                    StoragePath: $"Real Estate Star/1 - Leads/{req.LeadId}/CMA/2026-01-01-{req.LeadId}-CMA-Report.pdf.b64"));
                return;
            }
        }, ct);

    // ═══════════════════════════════════════════════════════════
    // Integration Test 1: Full seller flow
    // Submit seller lead → score → dispatch CMA → collect result
    // → dispatch PDF → send email → notify agent → status = Complete
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Integration_FullSellerFlow_ScoresCmaEmailNotifiesComplete()
    {
        // Arrange
        SetupAll();
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator();
        var statusHistory = new List<LeadStatus>();

        _leadStoreMock
            .Setup(s => s.UpdateStatusAsync(It.IsAny<Lead>(), It.IsAny<LeadStatus>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, LeadStatus, CancellationToken>((_, status, _) => statusHistory.Add(status))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Start simulated workers before calling the orchestrator
        var cmaWorker = SimulateCmaWorkerAsync(cts.Token);
        var pdfWorker = SimulatePdfWorkerAsync(cts.Token);

        // Act
        var request = new LeadOrchestrationRequest("agent-1", lead, "integ-seller-001");
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Ensure simulated workers finished
        await Task.WhenAll(cmaWorker, pdfWorker).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — CMA was dispatched and consumed
        _cmaChannel.Count.Should().Be(0, "CMA channel should be drained by the simulated worker");

        // Assert — HomeSearch was NOT dispatched (seller-only)
        _hsChannel.Count.Should().Be(0, "HomeSearch should not be dispatched for a seller lead");

        // Assert — PDF was dispatched and consumed
        _pdfChannel.Count.Should().Be(0, "PDF channel should be drained after CMA success");

        // Assert — lead was scored
        _scorerMock.Verify(s => s.Score(lead), Times.Once);

        // Assert — email drafter received CMA result
        _emailDrafterMock.Verify(d => d.DraftAsync(
            lead,
            It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r != null && r.Success),
            It.Is<HomeSearchWorkerResult?>(r => r == null),
            It.IsAny<AgentNotificationConfig>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — email sent to agent
        _gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — agent notified with CMA result
        _agentNotifierMock.Verify(n => n.NotifyAsync(
            lead,
            It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r != null && r.Success),
            It.Is<HomeSearchWorkerResult?>(r => r == null),
            It.IsAny<AgentNotificationConfig>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — status progression: Scored → Analyzing → Notified → Complete
        statusHistory.Should().ContainInOrder(
            LeadStatus.Scored,
            LeadStatus.Analyzing,
            LeadStatus.Notified,
            LeadStatus.Complete);
    }

    // ═══════════════════════════════════════════════════════════
    // Integration Test 2: Full buyer flow
    // Submit buyer lead → score → dispatch HomeSearch → collect result
    // → NO PDF (no CMA) → send email → notify agent → status = Complete
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Integration_FullBuyerFlow_ScoresHomeSearchEmailNotifiesComplete()
    {
        // Arrange
        SetupAll();
        var lead = BuildBuyerLead();
        var orchestrator = BuildOrchestrator();
        var statusHistory = new List<LeadStatus>();

        _leadStoreMock
            .Setup(s => s.UpdateStatusAsync(It.IsAny<Lead>(), It.IsAny<LeadStatus>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, LeadStatus, CancellationToken>((_, status, _) => statusHistory.Add(status))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var hsWorker = SimulateHomeSearchWorkerAsync(cts.Token);

        // Act
        var request = new LeadOrchestrationRequest("agent-1", lead, "integ-buyer-001");
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        await hsWorker.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — HomeSearch dispatched and consumed
        _hsChannel.Count.Should().Be(0, "HomeSearch channel should be drained by the simulated worker");

        // Assert — CMA was NOT dispatched (buyer-only)
        _cmaChannel.Count.Should().Be(0, "CMA should not be dispatched for a buyer lead");

        // Assert — PDF was NOT dispatched (no CMA result)
        _pdfChannel.Count.Should().Be(0, "PDF should not be dispatched when there is no CMA result");

        // Assert — lead was scored
        _scorerMock.Verify(s => s.Score(lead), Times.Once);

        // Assert — email drafter received HomeSearch result but no CMA
        _emailDrafterMock.Verify(d => d.DraftAsync(
            lead,
            It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r == null),
            It.Is<HomeSearchWorkerResult?>(r => r != null && r.Success),
            It.IsAny<AgentNotificationConfig>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — email sent
        _gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — agent notified with HomeSearch result
        _agentNotifierMock.Verify(n => n.NotifyAsync(
            lead,
            It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r == null),
            It.Is<HomeSearchWorkerResult?>(r => r != null && r.Success),
            It.IsAny<AgentNotificationConfig>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — status progression
        statusHistory.Should().ContainInOrder(
            LeadStatus.Scored,
            LeadStatus.Analyzing,
            LeadStatus.Notified,
            LeadStatus.Complete);
    }

    // ═══════════════════════════════════════════════════════════
    // Integration Test 3: Full "both" flow
    // Submit buyer+seller lead → dispatch CMA + HomeSearch in parallel
    // → collect both → dispatch PDF → send email with everything
    // → notify agent → status = Complete
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Integration_FullBothFlow_DispatchesCmaAndHomeSearchInParallel_CollectsBoth_PdfEmailNotify()
    {
        // Arrange
        SetupAll();
        var lead = BuildBothLead();
        var orchestrator = BuildOrchestrator();
        var statusHistory = new List<LeadStatus>();

        _leadStoreMock
            .Setup(s => s.UpdateStatusAsync(It.IsAny<Lead>(), It.IsAny<LeadStatus>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, LeadStatus, CancellationToken>((_, status, _) => statusHistory.Add(status))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Start all three simulated workers in parallel before the orchestrator runs
        var cmaWorker = SimulateCmaWorkerAsync(cts.Token);
        var hsWorker = SimulateHomeSearchWorkerAsync(cts.Token);
        var pdfWorker = SimulatePdfWorkerAsync(cts.Token);

        // Act
        var request = new LeadOrchestrationRequest("agent-1", lead, "integ-both-001");
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        await Task.WhenAll(cmaWorker, hsWorker, pdfWorker).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — both CMA and HomeSearch dispatched and consumed
        _cmaChannel.Count.Should().Be(0, "CMA channel should be drained");
        _hsChannel.Count.Should().Be(0, "HomeSearch channel should be drained");

        // Assert — PDF dispatched (because CMA succeeded)
        _pdfChannel.Count.Should().Be(0, "PDF channel should be drained after CMA success");

        // Assert — account config loaded exactly once (efficiency check)
        _accountConfigMock.Verify(s => s.GetAccountAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — email drafter received both results
        _emailDrafterMock.Verify(d => d.DraftAsync(
            lead,
            It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r != null && r.Success),
            It.Is<HomeSearchWorkerResult?>(r => r != null && r.Success),
            It.IsAny<AgentNotificationConfig>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — email sent once
        _gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — agent notified with both results
        _agentNotifierMock.Verify(n => n.NotifyAsync(
            lead,
            It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r != null && r.Success),
            It.Is<HomeSearchWorkerResult?>(r => r != null && r.Success),
            It.IsAny<AgentNotificationConfig>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — correct status progression
        statusHistory.Should().ContainInOrder(
            LeadStatus.Scored,
            LeadStatus.Analyzing,
            LeadStatus.Notified,
            LeadStatus.Complete);
    }

    // ═══════════════════════════════════════════════════════════
    // Integration Test 4: Partial failure — CMA times out
    // CMA times out → PDF NOT dispatched → email sent without PDF
    // → agent notified with null CMA → status = Complete
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Integration_CmaTimesOut_EmailSentWithoutPdf_AgentNotifiedWithNullCma()
    {
        // Arrange — 1-second timeout so the test completes quickly
        SetupAll();
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 1);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Drain the CMA channel without ever resolving the TCS (simulates a hung worker)
        _ = Task.Run(async () =>
        {
            await foreach (var _ in _cmaChannel.Reader.ReadAllAsync(cts.Token))
            {
                // Intentionally do NOT call req.Completion.TrySetResult(...)
                break;
            }
        }, cts.Token);

        // Act
        var request = new LeadOrchestrationRequest("agent-1", lead, "integ-cma-timeout-001");
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert — PDF was NOT dispatched (no CMA result)
        _pdfChannel.Count.Should().Be(0, "PDF should not be dispatched when CMA timed out");

        // Assert — email was still sent (graceful degradation)
        _gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — email drafter received null CMA
        _emailDrafterMock.Verify(d => d.DraftAsync(
            lead,
            It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r == null),
            It.IsAny<HomeSearchWorkerResult?>(),
            It.IsAny<AgentNotificationConfig>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — agent notified with null CMA (graceful degradation)
        _agentNotifierMock.Verify(n => n.NotifyAsync(
            lead,
            It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r == null),
            It.IsAny<HomeSearchWorkerResult?>(),
            It.IsAny<AgentNotificationConfig>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — pipeline still completes
        _leadStoreMock.Verify(s => s.UpdateStatusAsync(
            lead, LeadStatus.Complete, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // Integration Test 5: Resume after crash (checkpoint skip)
    // Note: The current orchestrator implementation does not have persistent checkpoint/resume
    // (the checkpoint/resume pattern is documented for the lead pipeline but implemented at
    // the individual step level via PipelineWorker.RunStepAsync). This test validates that
    // the orchestrator can process a lead that is already past the "Received" status —
    // simulating a restart scenario where the lead has already been scored in a prior run.
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Integration_LeadAlreadyScored_PipelineRunsFromScoringStep_CompletesNormally()
    {
        // Arrange — lead that was already scored in a prior run (e.g. after a crash)
        SetupAll();
        var lead = BuildSellerLead();
        lead.Status = LeadStatus.Scored; // already past Received
        lead.Score = BuildScore();       // already has a score from prior run

        var orchestrator = BuildOrchestrator();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var cmaWorker = SimulateCmaWorkerAsync(cts.Token);
        var pdfWorker = SimulatePdfWorkerAsync(cts.Token);

        // Act
        var request = new LeadOrchestrationRequest("agent-1", lead, "integ-resume-001");
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        await Task.WhenAll(cmaWorker, pdfWorker).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — orchestrator re-scores (scorer is always called; no skip logic in orchestrator)
        _scorerMock.Verify(s => s.Score(lead), Times.Once);

        // Assert — CMA was dispatched and consumed
        _cmaChannel.Count.Should().Be(0);

        // Assert — PDF was dispatched and consumed (CMA succeeded)
        _pdfChannel.Count.Should().Be(0);

        // Assert — email sent
        _gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — agent notified
        _agentNotifierMock.Verify(n => n.NotifyAsync(
            lead,
            It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r != null && r.Success),
            It.IsAny<HomeSearchWorkerResult?>(),
            It.IsAny<AgentNotificationConfig>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — pipeline reaches Complete
        _leadStoreMock.Verify(s => s.UpdateStatusAsync(
            lead, LeadStatus.Complete, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
