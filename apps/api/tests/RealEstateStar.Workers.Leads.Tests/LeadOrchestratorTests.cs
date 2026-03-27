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

public sealed class LeadOrchestratorTests
{
    // ───────────────────────── mocks ─────────────────────────
    private readonly Mock<ILeadStore> _leadStoreMock = new();
    private readonly Mock<IAccountConfigService> _accountConfigMock = new();
    private readonly Mock<ILeadScorer> _scorerMock = new();
    private readonly Mock<ILeadEmailDrafter> _emailDrafterMock = new();
    private readonly Mock<IGmailSender> _gmailMock = new();
    private readonly Mock<IAgentNotifier> _notifierMock = new();

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
            _notifierMock.Object,
            _healthTracker,
            NullLogger<LeadOrchestrator>.Instance,
            config);
    }

    // ───────────────────────── helpers ─────────────────────────

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
        Comps: [new CompSummary("123 Main St", 490_000m, 3, 2m, 1800, 14, 0.3)],
        MarketAnalysis: "Market trending upward.");

    private static HomeSearchWorkerResult BuildHsResult(string leadId) => new(
        LeadId: leadId,
        Success: true,
        Error: null,
        Listings: [new ListingSummary("456 Oak Ave", 510_000m, 3, 2m, 1900, "Active", null)],
        AreaSummary: "Good inventory available.");

    private static PdfWorkerResult BuildPdfResult(string leadId) => new(
        LeadId: leadId,
        Success: true,
        Error: null,
        StoragePath: "Real Estate Star/1 - Leads/lead-001/CMA/2026-01-01-lead-001-CMA-Report.pdf.b64");

    private static LeadEmail BuildEmailDraft() =>
        new("Subject: Help with your home", "<p>Hello</p>", PdfAttachmentPath: null);

    private void SetupAccountConfig(string agentId = "agent-1") =>
        _accountConfigMock
            .Setup(s => s.GetAccountAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccountConfig(agentId));

    private void SetupScorer() =>
        _scorerMock
            .Setup(s => s.Score(It.IsAny<Lead>()))
            .Returns(BuildScore());

    private void SetupEmailDrafter() =>
        _emailDrafterMock
            .Setup(d => d.DraftAsync(
                It.IsAny<Lead>(), It.IsAny<LeadScore>(),
                It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
                It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmailDraft());

    private void SetupGmail() =>
        _gmailMock
            .Setup(g => g.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    private void SetupNotifier() =>
        _notifierMock
            .Setup(n => n.NotifyAsync(
                It.IsAny<Lead>(), It.IsAny<LeadScore>(),
                It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
                It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    private void SetupLeadStore() =>
        _leadStoreMock
            .Setup(s => s.UpdateStatusAsync(It.IsAny<Lead>(), It.IsAny<LeadStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    /// <summary>
    /// Starts a background task to auto-resolve a CMA TCS when a request lands on the channel.
    /// Returns a task that resolves when the response was sent (for synchronization in tests).
    /// </summary>
    private Task AutoResolveCmaChannelAsync(CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            await foreach (var req in _cmaChannel.Reader.ReadAllAsync(ct))
            {
                req.Completion.TrySetResult(BuildCmaResult(req.Lead.Id.ToString()));
                return;
            }
        }, ct);
    }

    private Task AutoResolveHsChannelAsync(CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            await foreach (var req in _hsChannel.Reader.ReadAllAsync(ct))
            {
                req.Completion.TrySetResult(BuildHsResult(req.Lead.Id.ToString()));
                return;
            }
        }, ct);
    }

    private Task AutoResolvePdfChannelAsync(CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            await foreach (var req in _pdfChannel.Reader.ReadAllAsync(ct))
            {
                req.Completion.TrySetResult(BuildPdfResult(req.LeadId));
                return;
            }
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 1: Seller lead dispatches CMA, collects result, dispatches PDF, sends email + WhatsApp
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task SellerLead_DispatchesCma_CollectsResult_DispatchesPdf_SendsEmailAndNotifies()
    {
        // Arrange
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator();

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupNotifier();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cmaResolver = AutoResolveCmaChannelAsync(cts.Token);
        var pdfResolver = AutoResolvePdfChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-seller-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        await Task.WhenAll(cmaResolver, pdfResolver).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — CMA was dispatched (channel consumed one item)
        _cmaChannel.Count.Should().Be(0);

        // Assert — PDF was dispatched
        _pdfChannel.Count.Should().Be(0);

        // Assert — HomeSearch was NOT dispatched (seller-only lead)
        _hsChannel.Count.Should().Be(0);

        // Assert — email was sent
        _gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — agent notified
        _notifierMock.Verify(n => n.NotifyAsync(
            lead, It.IsAny<LeadScore>(),
            It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
            It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 2: Buyer lead dispatches HomeSearch, no PDF, sends email + WhatsApp
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task BuyerLead_DispatchesHomeSearch_NoPdf_SendsEmailAndNotifies()
    {
        // Arrange
        var lead = BuildBuyerLead();
        var orchestrator = BuildOrchestrator();

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupNotifier();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var hsResolver = AutoResolveHsChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-buyer-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);
        await hsResolver.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — HomeSearch was dispatched
        _hsChannel.Count.Should().Be(0);

        // Assert — CMA was NOT dispatched (buyer-only)
        _cmaChannel.Count.Should().Be(0);

        // Assert — PDF channel was NOT written to (no CMA result)
        _pdfChannel.Count.Should().Be(0);

        // Assert — email sent once
        _gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — agent notified
        _notifierMock.Verify(n => n.NotifyAsync(
            lead, It.IsAny<LeadScore>(),
            It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
            It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 3: Both lead dispatches CMA + HomeSearch in parallel, collects both, dispatches PDF,
    //         sends email with both results
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task BothLead_DispatchesCmaAndHomeSearchInParallel_CollectsBoth_DispatchesPdf_SendsEmail()
    {
        // Arrange
        var lead = BuildBothLead();
        var orchestrator = BuildOrchestrator();

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupNotifier();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cmaResolver = AutoResolveCmaChannelAsync(cts.Token);
        var hsResolver = AutoResolveHsChannelAsync(cts.Token);
        var pdfResolver = AutoResolvePdfChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-both-001");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);
        await Task.WhenAll(cmaResolver, hsResolver, pdfResolver).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — both workers dispatched
        _cmaChannel.Count.Should().Be(0);
        _hsChannel.Count.Should().Be(0);

        // Assert — PDF dispatched (because CMA succeeded)
        _pdfChannel.Count.Should().Be(0);

        // Assert — email drafter received both results
        _emailDrafterMock.Verify(d => d.DraftAsync(
            lead, It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r != null && r.Success),
            It.Is<HomeSearchWorkerResult?>(r => r != null && r.Success),
            It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — notifier received both results
        _notifierMock.Verify(n => n.NotifyAsync(
            lead, It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r != null && r.Success),
            It.Is<HomeSearchWorkerResult?>(r => r != null && r.Success),
            It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 4: CMA timeout — proceeds without CMA, email sent, notifier called with null CMA
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CmaTimeout_ProceedsWithoutCma_EmailSent_NotifierCalledWithNullCma()
    {
        // Arrange — use a 1-second timeout; don't resolve the CMA TCS
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 1);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupNotifier();
        SetupLeadStore();

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-cma-timeout");

        // Drain the CMA channel without resolving (simulate worker that never responds)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _ = Task.Run(async () =>
        {
            // Read the request but never complete the TCS
            await foreach (var _ in _cmaChannel.Reader.ReadAllAsync(cts.Token))
            {
                break; // consume without resolving
            }
        }, cts.Token);

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert — email was still sent
        _gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — notifier called with null CMA
        _notifierMock.Verify(n => n.NotifyAsync(
            lead, It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r == null),
            It.IsAny<HomeSearchWorkerResult?>(),
            It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — PDF was NOT dispatched
        _pdfChannel.Count.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 5: HomeSearch timeout — proceeds without listings, notifier called with null HomeSearch
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task HomeSearchTimeout_ProceedsWithoutListings_NotifierCalledWithNullHomeSearch()
    {
        // Arrange — buyer lead, 1-second timeout, no resolver for HomeSearch
        var lead = BuildBuyerLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 1);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupNotifier();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Consume but never resolve
        _ = Task.Run(async () =>
        {
            await foreach (var _ in _hsChannel.Reader.ReadAllAsync(cts.Token))
            {
                break;
            }
        }, cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-hs-timeout");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert — notifier called with null HomeSearch
        _notifierMock.Verify(n => n.NotifyAsync(
            lead, It.IsAny<LeadScore>(),
            It.IsAny<CmaWorkerResult?>(),
            It.Is<HomeSearchWorkerResult?>(r => r == null),
            It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — email still sent
        _gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 6: All workers fail — orchestrator still calls notifier (graceful degradation)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AllWorkersFail_OrchestratorStillCallsNotifier()
    {
        // Arrange — both lead with 1-second timeout; neither CMA nor HomeSearch resolves
        var lead = BuildBothLead();
        var orchestrator = BuildOrchestrator(timeoutSeconds: 1);

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupNotifier();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Consume both channels without resolving
        _ = Task.Run(async () =>
        {
            await foreach (var _ in _cmaChannel.Reader.ReadAllAsync(cts.Token))
                break;
        }, cts.Token);
        _ = Task.Run(async () =>
        {
            await foreach (var _ in _hsChannel.Reader.ReadAllAsync(cts.Token))
                break;
        }, cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-all-fail");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);

        // Assert — notifier still called (with null results)
        _notifierMock.Verify(n => n.NotifyAsync(
            lead, It.IsAny<LeadScore>(),
            It.Is<CmaWorkerResult?>(r => r == null),
            It.Is<HomeSearchWorkerResult?>(r => r == null),
            It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — status eventually moves to Complete
        _leadStoreMock.Verify(s => s.UpdateStatusAsync(
            lead, LeadStatus.Complete, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 7: Single config read — IAccountConfigService called exactly once
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ProcessRequestAsync_LoadsAccountConfigExactlyOnce()
    {
        // Arrange
        var lead = BuildBothLead();
        var orchestrator = BuildOrchestrator();

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupNotifier();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cmaResolver = AutoResolveCmaChannelAsync(cts.Token);
        var hsResolver = AutoResolveHsChannelAsync(cts.Token);
        var pdfResolver = AutoResolvePdfChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-config-read");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);
        await Task.WhenAll(cmaResolver, hsResolver, pdfResolver).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — account config fetched exactly once
        _accountConfigMock.Verify(s => s.GetAccountAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 8: Lead status progression — Scored → Analyzing → Notified → Complete
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ProcessRequestAsync_UpdatesLeadStatusInOrder_ScoredAnalyzingNotifiedComplete()
    {
        // Arrange
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator();

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupNotifier();

        var statusHistory = new List<LeadStatus>();
        _leadStoreMock
            .Setup(s => s.UpdateStatusAsync(It.IsAny<Lead>(), It.IsAny<LeadStatus>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, LeadStatus, CancellationToken>((_, status, _) => statusHistory.Add(status))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cmaResolver = AutoResolveCmaChannelAsync(cts.Token);
        var pdfResolver = AutoResolvePdfChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-status");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);
        await Task.WhenAll(cmaResolver, pdfResolver).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — status updated in correct order
        statusHistory.Should().ContainInOrder(
            LeadStatus.Scored,
            LeadStatus.Analyzing,
            LeadStatus.Notified,
            LeadStatus.Complete);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 9: Missing agent config — returns early, no further processing
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task MissingAgentConfig_ReturnsEarly_NoScoring_NoEmail_NoNotification()
    {
        // Arrange
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator();

        _accountConfigMock
            .Setup(s => s.GetAccountAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountConfig?)null);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-missing-config");

        // Act
        await orchestrator.ProcessRequestAsync(request, CancellationToken.None);

        // Assert — nothing happens after early return
        _scorerMock.Verify(s => s.Score(It.IsAny<Lead>()), Times.Never);
        _gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _notifierMock.Verify(n => n.NotifyAsync(
            It.IsAny<Lead>(), It.IsAny<LeadScore>(),
            It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
            It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 10: DispatchWorkers — seller dispatches CMA only
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void DispatchWorkers_SellerLead_DispatchesCmaOnly()
    {
        // Arrange
        var lead = BuildSellerLead();
        var agentConfig = new AgentNotificationConfig
        {
            AgentId = "agent-1", Handle = "agent-1", Name = "Test Agent", FirstName = "Test",
            Email = "test@example.com", Phone = "555-0000", LicenseNumber = "NJ-001",
            BrokerageName = "Test Brokerage", PrimaryColor = "#000", AccentColor = "#FFF", State = "NJ"
        };
        var orchestrator = BuildOrchestrator();

        // Act
        var (cmaTcs, hsTcs) = orchestrator.DispatchWorkers(lead, "agent-1", agentConfig, "corr-1");

        // Assert
        Assert.NotNull(cmaTcs);
        Assert.Null(hsTcs);
        _cmaChannel.Count.Should().Be(1);
        _hsChannel.Count.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 11: DispatchWorkers — buyer dispatches HomeSearch only
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void DispatchWorkers_BuyerLead_DispatchesHomeSearchOnly()
    {
        // Arrange
        var lead = BuildBuyerLead();
        var agentConfig = new AgentNotificationConfig
        {
            AgentId = "agent-1", Handle = "agent-1", Name = "Test Agent", FirstName = "Test",
            Email = "test@example.com", Phone = "555-0000", LicenseNumber = "NJ-001",
            BrokerageName = "Test Brokerage", PrimaryColor = "#000", AccentColor = "#FFF", State = "NJ"
        };
        var orchestrator = BuildOrchestrator();

        // Act
        var (cmaTcs, hsTcs) = orchestrator.DispatchWorkers(lead, "agent-1", agentConfig, "corr-2");

        // Assert
        Assert.Null(cmaTcs);
        Assert.NotNull(hsTcs);
        _cmaChannel.Count.Should().Be(0);
        _hsChannel.Count.Should().Be(1);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 12: DispatchWorkers — both lead dispatches both workers
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void DispatchWorkers_BothLead_DispatchesBothWorkers()
    {
        // Arrange
        var lead = BuildBothLead();
        var agentConfig = new AgentNotificationConfig
        {
            AgentId = "agent-1", Handle = "agent-1", Name = "Test Agent", FirstName = "Test",
            Email = "test@example.com", Phone = "555-0000", LicenseNumber = "NJ-001",
            BrokerageName = "Test Brokerage", PrimaryColor = "#000", AccentColor = "#FFF", State = "NJ"
        };
        var orchestrator = BuildOrchestrator();

        // Act
        var (cmaTcs, hsTcs) = orchestrator.DispatchWorkers(lead, "agent-1", agentConfig, "corr-3");

        // Assert
        Assert.NotNull(cmaTcs);
        Assert.NotNull(hsTcs);
        _cmaChannel.Count.Should().Be(1);
        _hsChannel.Count.Should().Be(1);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 13: BuildAgentNotificationConfig maps AccountConfig fields correctly
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void BuildAgentNotificationConfig_MapsAllFieldsCorrectly()
    {
        // Arrange
        var accountConfig = BuildAccountConfig("agent-1");

        // Act
        var result = LeadOrchestrator.BuildAgentNotificationConfig("agent-1", accountConfig);

        // Assert
        result.AgentId.Should().Be("agent-1");
        result.Handle.Should().Be("agent-1");
        result.Name.Should().Be("Jenise Buckalew");
        result.FirstName.Should().Be("Jenise");
        result.Email.Should().Be("jenise@example.com");
        result.Phone.Should().Be("555-9999");
        result.LicenseNumber.Should().Be("NJ-12345");
        result.BrokerageName.Should().Be("Star Realty");
        result.PrimaryColor.Should().Be("#1A3C5E");
        result.AccentColor.Should().Be("#D4A853");
        result.State.Should().Be("NJ");
        result.ServiceAreas.Should().BeEquivalentTo(["Newark", "Jersey City"]);
        result.WhatsAppPhoneNumberId.Should().Be("+15550001234");
    }

    // ═══════════════════════════════════════════════════════════
    // Test 14: Email drafter throws — notifier is still called
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task EmailDrafterThrows_NotifierStillCalled_StatusReachesComplete()
    {
        // Arrange
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator();

        SetupAccountConfig();
        SetupScorer();
        SetupLeadStore();

        _emailDrafterMock
            .Setup(d => d.DraftAsync(
                It.IsAny<Lead>(), It.IsAny<LeadScore>(),
                It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
                It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Claude unavailable"));

        SetupNotifier();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cmaResolver = AutoResolveCmaChannelAsync(cts.Token);
        var pdfResolver = AutoResolvePdfChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-drafter-fail");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);
        await Task.WhenAll(cmaResolver, pdfResolver).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — notifier still called even though email drafting failed
        _notifierMock.Verify(n => n.NotifyAsync(
            lead, It.IsAny<LeadScore>(),
            It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
            It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — Gmail was NOT called (no draft)
        _gmailMock.Verify(g => g.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert — Notified is set even when draft fails (notification was attempted)
        _leadStoreMock.Verify(s => s.UpdateStatusAsync(
            lead, LeadStatus.Notified, It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — status still reaches Complete
        _leadStoreMock.Verify(s => s.UpdateStatusAsync(
            lead, LeadStatus.Complete, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 15: Gmail send throws — status still reaches Complete
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GmailSendThrows_StatusStillReachesComplete_NotifierStillCalled()
    {
        // Arrange
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator();

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupLeadStore();
        SetupNotifier();

        _gmailMock
            .Setup(g => g.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SMTP error"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cmaResolver = AutoResolveCmaChannelAsync(cts.Token);
        var pdfResolver = AutoResolvePdfChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-gmail-fail");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);
        await Task.WhenAll(cmaResolver, pdfResolver).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — notifier still called
        _notifierMock.Verify(n => n.NotifyAsync(
            It.IsAny<Lead>(), It.IsAny<LeadScore>(),
            It.IsAny<CmaWorkerResult?>(), It.IsAny<HomeSearchWorkerResult?>(),
            It.IsAny<AgentNotificationConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — status still reaches Complete
        _leadStoreMock.Verify(s => s.UpdateStatusAsync(
            lead, LeadStatus.Complete, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // Test 16: Health tracker is updated after successful processing
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ProcessRequestAsync_RecordsHealthTrackerActivity_AfterSuccess()
    {
        // Arrange
        var lead = BuildSellerLead();
        var orchestrator = BuildOrchestrator();

        SetupAccountConfig();
        SetupScorer();
        SetupEmailDrafter();
        SetupGmail();
        SetupNotifier();
        SetupLeadStore();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cmaResolver = AutoResolveCmaChannelAsync(cts.Token);
        var pdfResolver = AutoResolvePdfChannelAsync(cts.Token);

        var request = new LeadOrchestrationRequest("agent-1", lead, "corr-health");

        // Act
        await orchestrator.ProcessRequestAsync(request, cts.Token);
        await Task.WhenAll(cmaResolver, pdfResolver).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — health tracker has recorded activity
        _healthTracker.GetLastActivity("LeadOrchestrator").Should().NotBeNull();
        _healthTracker.GetLastActivity("LeadOrchestrator").Should()
            .BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30));
    }
}
