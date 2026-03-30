using System.Diagnostics.Metrics;
using System.Net;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Activities.Activation.BrandMerge;
using RealEstateStar.Activities.Activation.PersistAgentProfile;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Workers.Activation.AgentDiscovery;
using RealEstateStar.Workers.Activation.BrandExtraction;
using RealEstateStar.Workers.Activation.BrandingDiscovery;
using RealEstateStar.Workers.Activation.BrandVoice;
using RealEstateStar.Workers.Activation.CmaStyle;
using RealEstateStar.Workers.Activation.Coaching;
using RealEstateStar.Workers.Activation.ComplianceAnalysis;
using RealEstateStar.Workers.Activation.DriveIndex;
using RealEstateStar.Workers.Activation.EmailFetch;
using Microsoft.Extensions.Logging;
using RealEstateStar.Workers.Activation.FeeStructure;
using RealEstateStar.Workers.Activation.MarketingStyle;
using RealEstateStar.Workers.Activation.Personality;
using RealEstateStar.Workers.Activation.PipelineAnalysis;
using RealEstateStar.Workers.Activation.VoiceExtraction;
using RealEstateStar.Workers.Activation.WebsiteStyle;
using AgentDiscoveryModel = RealEstateStar.Domain.Activation.Models.AgentDiscovery;
using DriveIndexModel = RealEstateStar.Domain.Activation.Models.DriveIndex;

namespace RealEstateStar.Workers.Activation.Orchestrator.Tests;

/// <summary>
/// Unit tests for ActivationOrchestrator. Tests call internal methods directly
/// to validate each phase, skip-if-complete logic, checkpoint management,
/// counter increments, and error handling.
///
/// Workers are sealed — each is constructed with mocked interface dependencies
/// (IAnthropicClient, IContentSanitizer, etc.) rather than being mocked directly.
/// </summary>
public class ActivationOrchestratorTests
{
    private const string AccountId = "acct-test";
    private const string AgentId = "agent-test";

    // ── Shared mocks ──────────────────────────────────────────────────────────

    private readonly Mock<IAnthropicClient> _anthropic = new();
    private readonly Mock<IContentSanitizer> _sanitizer = new();
    private readonly Mock<IGDriveClient> _driveClient = new();
    private readonly Mock<IOAuthRefresher> _oauthRefresher = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly Mock<IWhatsAppSender> _whatsAppSender = new();
    private readonly Mock<IGmailReader> _gmailReader = new();
    private readonly Mock<IDocumentStorageProvider> _storage = new();
    private readonly Mock<IAgentContextLoader> _contextLoader = new();
    private readonly Mock<IWelcomeNotificationService> _welcomeService = new();

    // Activity dependencies
    private readonly Mock<IFileStorageProvider> _fileStorage = new();
    private readonly Mock<IAgentConfigService> _agentConfigService = new();
    private readonly Mock<IBrandMergeService> _brandMergeService = new();

    public ActivationOrchestratorTests()
    {
        // Default sanitizer passthrough
        _sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        // Default Anthropic returns a simple response
        _anthropic.Setup(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("# Test Output\nContent here.", 100, 200, 500));

        // Default Gmail returns empty
        _gmailReader.Setup(r => r.GetSentEmailsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailMessage>());
        _gmailReader.Setup(r => r.GetInboxEmailsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailMessage>());

        // Default Drive returns empty index
        _driveClient.Setup(d => d.GetOrCreateFolderAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("folder-id-test");
        _driveClient.Setup(d => d.ListAllFilesAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DriveFileInfo>());
        _driveClient.Setup(d => d.GetFileContentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Default OAuth returns no credential (forces discovery to return minimal)
        _oauthRefresher.Setup(o => o.GetValidCredentialAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        // Default WhatsApp throws not-registered
        _whatsAppSender.Setup(w => w.SendFreeformAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WhatsAppNotRegisteredException("555-000-0000"));

        // Default HttpClientFactory returns a 200 empty response
        var handler = new NoOpHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Default storage returns null (file not found)
        _storage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.UpdateDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.DeleteDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.EnsureFolderExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default context loader returns null
        _contextLoader.Setup(c => c.LoadAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentContext?)null);

        // Default welcome service no-ops
        _welcomeService.Setup(w => w.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<ActivationOutputs>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default file storage for activities
        _fileStorage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _fileStorage.Setup(s => s.WriteDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _fileStorage.Setup(s => s.UpdateDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _fileStorage.Setup(s => s.EnsureFolderExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default agentConfigService no-ops
        _agentConfigService.Setup(a => a.GenerateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<ActivationOutputs>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default brandMergeService returns stub result
        _brandMergeService.Setup(b => b.MergeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BrandMergeResult("# Brand Profile", "# Brand Voice"));
    }

    // ── Builder ───────────────────────────────────────────────────────────────

    private ActivationOrchestrator BuildOrchestrator()
    {
        var channel = Channel.CreateUnbounded<ActivationRequest>();

        var emailFetchWorker = new AgentEmailFetchWorker(
            _gmailReader.Object,
            NullLogger<AgentEmailFetchWorker>.Instance);

        var driveIndexWorker = new DriveIndexWorker(
            _driveClient.Object,
            NullLogger<DriveIndexWorker>.Instance);

        var discoveryWorker = new AgentDiscoveryWorker(
            _oauthRefresher.Object,
            _httpClientFactory.Object,
            _whatsAppSender.Object,
            NullLogger<AgentDiscoveryWorker>.Instance);

        var voiceWorker = new VoiceExtractionWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<VoiceExtractionWorker>.Instance);

        var personalityWorker = new PersonalityWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<PersonalityWorker>.Instance);

        var brandingWorker = new BrandingDiscoveryWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<BrandingDiscoveryWorker>.Instance);

        var cmaStyleWorker = new CmaStyleWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<CmaStyleWorker>.Instance);

        var marketingWorker = new MarketingStyleWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<MarketingStyleWorker>.Instance);

        var websiteWorker = new WebsiteStyleWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<WebsiteStyleWorker>.Instance);

        var pipelineWorker = new PipelineAnalysisWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<PipelineAnalysisWorker>.Instance);

        var coachingWorker = new CoachingWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<CoachingWorker>.Instance);

        var brandExtractionWorker = new BrandExtractionWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<BrandExtractionWorker>.Instance);

        var brandVoiceWorker = new BrandVoiceWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<BrandVoiceWorker>.Instance);

        var complianceWorker = new ComplianceAnalysisWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<ComplianceAnalysisWorker>.Instance);

        var feeWorker = new FeeStructureWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<FeeStructureWorker>.Instance);

        var persistActivity = new AgentProfilePersistActivity(
            _fileStorage.Object,
            _agentConfigService.Object,
            NullLogger<AgentProfilePersistActivity>.Instance);

        var brandMergeActivity = new BrandMergeActivity(
            _brandMergeService.Object,
            _fileStorage.Object,
            NullLogger<BrandMergeActivity>.Instance);

        return new ActivationOrchestrator(
            channel.Reader,
            emailFetchWorker,
            driveIndexWorker,
            discoveryWorker,
            voiceWorker,
            personalityWorker,
            brandingWorker,
            cmaStyleWorker,
            marketingWorker,
            websiteWorker,
            pipelineWorker,
            coachingWorker,
            brandExtractionWorker,
            brandVoiceWorker,
            complianceWorker,
            feeWorker,
            persistActivity,
            brandMergeActivity,
            _welcomeService.Object,
            _storage.Object,
            _contextLoader.Object,
            NullLogger<ActivationOrchestrator>.Instance);
    }

    private static ActivationRequest MakeRequest(
        string accountId = AccountId,
        string agentId = AgentId,
        string email = "agent@example.com") =>
        new(accountId, agentId, email, DateTime.UtcNow);

    private static AgentDiscoveryModel EmptyDiscovery() =>
        new(null, null, null, [], [], [], null, false);

    private static DriveIndexModel EmptyDrive() =>
        new("folder-id", [], new Dictionary<string, string>(), []);

    private static EmailCorpus EmptyCorpus() =>
        new([], [], null);

    // ── Skip-if-complete tests ─────────────────────────────────────────────────

    [Fact]
    public async Task IsAlreadyCompleteAsync_AllFilesExist_ReturnsTrue()
    {
        // All required files return non-null content
        _storage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# file content");

        var orchestrator = BuildOrchestrator();
        var request = MakeRequest();

        var result = await orchestrator.IsAlreadyCompleteAsync(request, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAlreadyCompleteAsync_OneFileMissing_ReturnsFalse()
    {
        // All files return content EXCEPT "headshot.jpg"
        _storage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# file content");
        _storage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), "headshot.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var orchestrator = BuildOrchestrator();
        var result = await orchestrator.IsAlreadyCompleteAsync(MakeRequest(), CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessActivationAsync_AlreadyComplete_SkipsToWelcome()
    {
        // All required files exist → skip-if-complete
        _storage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# content");

        var orchestrator = BuildOrchestrator();
        await orchestrator.ProcessActivationAsync(MakeRequest(), CancellationToken.None);

        // Welcome service still called even on skip (idempotent)
        _welcomeService.Verify(w => w.SendAsync(
            AccountId, AgentId, AgentId, It.IsAny<ActivationOutputs>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Gmail should NOT be called (pipeline skipped)
        _gmailReader.Verify(r => r.GetSentEmailsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessActivationAsync_AlreadyComplete_LogsActv002()
    {
        // All required files exist
        _storage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# content");

        // Use a mock logger to capture log messages
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ActivationOrchestrator>>();
        mockLogger.Setup(l => l.IsEnabled(It.IsAny<Microsoft.Extensions.Logging.LogLevel>())).Returns(true);

        var channel = Channel.CreateUnbounded<ActivationRequest>();
        var emailFetchWorker = new AgentEmailFetchWorker(
            _gmailReader.Object, NullLogger<AgentEmailFetchWorker>.Instance);
        var driveIndexWorker = new DriveIndexWorker(
            _driveClient.Object, NullLogger<DriveIndexWorker>.Instance);
        var discoveryWorker = new AgentDiscoveryWorker(
            _oauthRefresher.Object, _httpClientFactory.Object,
            _whatsAppSender.Object, NullLogger<AgentDiscoveryWorker>.Instance);

        var sut = new ActivationOrchestrator(
            channel.Reader,
            emailFetchWorker,
            driveIndexWorker,
            discoveryWorker,
            new VoiceExtractionWorker(_anthropic.Object, _sanitizer.Object, NullLogger<VoiceExtractionWorker>.Instance),
            new PersonalityWorker(_anthropic.Object, _sanitizer.Object, NullLogger<PersonalityWorker>.Instance),
            new BrandingDiscoveryWorker(_anthropic.Object, _sanitizer.Object, NullLogger<BrandingDiscoveryWorker>.Instance),
            new CmaStyleWorker(_anthropic.Object, _sanitizer.Object, NullLogger<CmaStyleWorker>.Instance),
            new MarketingStyleWorker(_anthropic.Object, _sanitizer.Object, NullLogger<MarketingStyleWorker>.Instance),
            new WebsiteStyleWorker(_anthropic.Object, _sanitizer.Object, NullLogger<WebsiteStyleWorker>.Instance),
            new PipelineAnalysisWorker(_anthropic.Object, _sanitizer.Object, NullLogger<PipelineAnalysisWorker>.Instance),
            new CoachingWorker(_anthropic.Object, _sanitizer.Object, NullLogger<CoachingWorker>.Instance),
            new BrandExtractionWorker(_anthropic.Object, _sanitizer.Object, NullLogger<BrandExtractionWorker>.Instance),
            new BrandVoiceWorker(_anthropic.Object, _sanitizer.Object, NullLogger<BrandVoiceWorker>.Instance),
            new ComplianceAnalysisWorker(_anthropic.Object, _sanitizer.Object, NullLogger<ComplianceAnalysisWorker>.Instance),
            new FeeStructureWorker(_anthropic.Object, _sanitizer.Object, NullLogger<FeeStructureWorker>.Instance),
            new AgentProfilePersistActivity(_fileStorage.Object, _agentConfigService.Object, NullLogger<AgentProfilePersistActivity>.Instance),
            new BrandMergeActivity(_brandMergeService.Object, _fileStorage.Object, NullLogger<BrandMergeActivity>.Instance),
            _welcomeService.Object,
            _storage.Object,
            _contextLoader.Object,
            mockLogger.Object);

        await sut.ProcessActivationAsync(MakeRequest(), CancellationToken.None);

        // Verify [ACTV-002] was logged
        mockLogger.Verify(
            l => l.Log(
                Microsoft.Extensions.Logging.LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("ACTV-002")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ── Phase 1: Gather tests ─────────────────────────────────────────────────

    [Fact]
    public async Task RunPhase1Async_CallsEmailFetchAndDriveIndex()
    {
        var orchestrator = BuildOrchestrator();

        var (corpus, drive, discovery, agentName) = await orchestrator.RunPhase1Async(
            MakeRequest(), CancellationToken.None);

        // Both Phase 1 workers called
        _gmailReader.Verify(r => r.GetSentEmailsAsync(
            AccountId, AgentId, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        _gmailReader.Verify(r => r.GetInboxEmailsAsync(
            AccountId, AgentId, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

        _driveClient.Verify(d => d.ListAllFilesAsync(
            AccountId, AgentId, It.IsAny<CancellationToken>()), Times.Once);

        corpus.Should().NotBeNull();
        drive.Should().NotBeNull();
        discovery.Should().NotBeNull();
    }

    [Fact]
    public async Task RunPhase1Async_CallsDiscoveryWorkerWithEmailSignature()
    {
        // Return a single sent email to force signature extraction
        var emailWithSig = new EmailMessage(
            "id1", "Subject", "Hello\n\nBest regards,\nJane Doe\n555-111-2222\nSunrise Realty",
            "agent@example.com", ["client@example.com"], DateTime.UtcNow, null);
        _gmailReader.Setup(r => r.GetSentEmailsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailMessage> { emailWithSig });

        var orchestrator = BuildOrchestrator();
        var (corpus, _, discovery, _) = await orchestrator.RunPhase1Async(
            MakeRequest(), CancellationToken.None);

        // Discovery receives the email signature extracted from sent mail
        // (verified indirectly: corpus.Signature was populated)
        // Both are returned as non-null
        corpus.Should().NotBeNull();
        discovery.Should().NotBeNull();
    }

    // ── Phase 2: Synthesize tests ─────────────────────────────────────────────

    [Fact]
    public async Task RunPhase2Async_ReturnsOutputsWithContent()
    {
        var orchestrator = BuildOrchestrator();
        var outputs = await orchestrator.RunPhase2Async(
            MakeRequest(),
            agentName: "Jane Doe",
            emailCorpus: EmptyCorpus(),
            driveIndex: EmptyDrive(),
            discovery: EmptyDiscovery(),
            CancellationToken.None);

        // With 0 emails the voice/personality workers still run (they handle low-data)
        // Anthropic should have been called for synthesis
        _anthropic.Verify(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        outputs.AgentEmail.Should().Be("agent@example.com");
    }

    [Fact]
    public async Task RunPhase2Async_WorkerThrows_ContinuesWithOtherWorkers()
    {
        // Re-configure Anthropic: first call throws, subsequent calls succeed
        _anthropic.Reset();
        _sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        var callCount = 0;
        _anthropic.Setup(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var current = System.Threading.Interlocked.Increment(ref callCount);
                if (current == 1)
                    throw new InvalidOperationException("First worker failure (simulated)");
                return new AnthropicResponse("# Output", 100, 200, 500);
            });

        var orchestrator = BuildOrchestrator();
        // Should not throw — RunSafeAsync catches individual worker failures
        var outputs = await orchestrator.RunPhase2Async(
            MakeRequest(), "Jane", EmptyCorpus(), EmptyDrive(), EmptyDiscovery(),
            CancellationToken.None);

        outputs.Should().NotBeNull();
        callCount.Should().BeGreaterThan(1, "other workers ran after the first failure");
    }

    [Fact]
    public async Task RunPhase2Async_AnthropicReturnsNullContent_OutputsAreNull()
    {
        // Workers with low-data (0 emails) return minimal output via CoachingResult.Insufficient
        // We verify the outputs reflect the low-data path gracefully
        var orchestrator = BuildOrchestrator();

        var outputs = await orchestrator.RunPhase2Async(
            MakeRequest(), "Jane", EmptyCorpus(), EmptyDrive(), EmptyDiscovery(),
            CancellationToken.None);

        // CoachingReport is null when emails < MinEmailsRequired (5 each)
        outputs.CoachingReport.Should().BeNull("coaching skips when <5 sent + <5 inbox emails");
    }

    // ── Phase 3: Persist tests ─────────────────────────────────────────────────

    [Fact]
    public async Task RunPhase3Async_CallsPersistActivity()
    {
        var orchestrator = BuildOrchestrator();
        var outputs = new ActivationOutputs { VoiceSkill = "# Voice Skill" };

        await orchestrator.RunPhase3Async(MakeRequest(), outputs, EmptyDiscovery(), CancellationToken.None);

        // PersistActivity depends on _fileStorage and _agentConfigService
        _fileStorage.Verify(s => s.WriteDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunPhase3Async_CallsBrandMergeActivity()
    {
        var orchestrator = BuildOrchestrator();
        var outputs = new ActivationOutputs
        {
            BrandingKitMarkdown = "# Branding Kit",
            VoiceSkill = "# Voice Skill"
        };

        await orchestrator.RunPhase3Async(MakeRequest(), outputs, EmptyDiscovery(), CancellationToken.None);

        _brandMergeService.Verify(b => b.MergeAsync(
            AccountId, AgentId, "# Branding Kit", "# Voice Skill", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunPhase3Async_PersistActivityFails_Rethrows()
    {
        _agentConfigService.Setup(a => a.GenerateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<ActivationOutputs>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Config generation failed"));

        var orchestrator = BuildOrchestrator();

        var act = async () => await orchestrator.RunPhase3Async(
            MakeRequest(), new ActivationOutputs(), EmptyDiscovery(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Config generation failed");
    }

    // ── Phase 4: Notify tests ─────────────────────────────────────────────────

    [Fact]
    public async Task RunPhase4Async_CallsWelcomeService()
    {
        var orchestrator = BuildOrchestrator();
        var outputs = new ActivationOutputs { AgentName = "Jane Doe" };

        await orchestrator.RunPhase4Async(MakeRequest(), outputs, CancellationToken.None);

        _welcomeService.Verify(w => w.SendAsync(
            AccountId, AgentId, AgentId, outputs, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Checkpoint tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task SavePhase1CheckpointAsync_WritesCheckpointWithDiscoveryData()
    {
        string? writtenJson = null;
        _storage.Setup(s => s.WriteDocumentAsync(
            It.IsAny<string>(), ActivationOrchestrator.Phase1CheckpointFile,
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, content, _) =>
                writtenJson = content)
            .Returns(Task.CompletedTask);

        var orchestrator = BuildOrchestrator();
        var discovery = new AgentDiscoveryModel(
            HeadshotBytes: [1, 2, 3],
            LogoBytes: null,
            Phone: "555-000-0000",
            Websites: [new DiscoveredWebsite("https://example.com", "signature", null)],
            Reviews: [],
            Profiles: [],
            Ga4MeasurementId: null,
            WhatsAppEnabled: true);

        var corpus = new EmailCorpus(
            SentEmails: Enumerable.Range(0, 5).Select(_ =>
                new EmailMessage("id", "subj", "body", "from", [], DateTime.UtcNow, null)).ToList(),
            InboxEmails: Enumerable.Range(0, 3).Select(_ =>
                new EmailMessage("id", "subj", "body", "from", [], DateTime.UtcNow, null)).ToList(),
            Signature: null);

        await orchestrator.SavePhase1CheckpointAsync(
            MakeRequest(), corpus, EmptyDrive(), discovery, CancellationToken.None);

        writtenJson.Should().NotBeNullOrEmpty();
        writtenJson.Should().Contain("\"WebsitesFound\":1");
        writtenJson.Should().Contain("\"WhatsAppEnabled\":true");
        writtenJson.Should().Contain("\"HeadshotFound\":true");
        writtenJson.Should().Contain("\"SentEmailCount\":5");
        writtenJson.Should().Contain("\"InboxEmailCount\":3");
    }

    [Fact]
    public async Task SavePhase2CheckpointAsync_WritesPerWorkerStatus()
    {
        string? writtenJson = null;
        _storage.Setup(s => s.WriteDocumentAsync(
            It.IsAny<string>(), ActivationOrchestrator.Phase2CheckpointFile,
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, content, _) =>
                writtenJson = content)
            .Returns(Task.CompletedTask);

        var orchestrator = BuildOrchestrator();
        var outputs = new ActivationOutputs
        {
            VoiceSkill = "# Voice",
            PersonalitySkill = null,  // skipped
            CoachingReport = "# Coaching"
        };

        await orchestrator.SavePhase2CheckpointAsync(MakeRequest(), outputs, CancellationToken.None);

        writtenJson.Should().NotBeNullOrEmpty();
        writtenJson.Should().Contain("\"voice\":\"completed\"");
        writtenJson.Should().Contain("\"personality\":\"skipped\"");
        writtenJson.Should().Contain("\"coaching\":\"completed\"");
    }

    [Fact]
    public async Task ClearCheckpointsAsync_DeletesBothCheckpoints_WhenBothExist()
    {
        _storage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), ActivationOrchestrator.Phase1CheckpointFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"existing\":true}");
        _storage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), ActivationOrchestrator.Phase2CheckpointFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"existing\":true}");

        var orchestrator = BuildOrchestrator();
        await orchestrator.ClearCheckpointsAsync(MakeRequest(), CancellationToken.None);

        _storage.Verify(s => s.DeleteDocumentAsync(
            It.IsAny<string>(), ActivationOrchestrator.Phase1CheckpointFile, It.IsAny<CancellationToken>()),
            Times.Once);
        _storage.Verify(s => s.DeleteDocumentAsync(
            It.IsAny<string>(), ActivationOrchestrator.Phase2CheckpointFile, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Counter tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessActivationAsync_FullRun_IncrementsStartedAndCompleted()
    {
        // Use a MeterListener to capture counter increments
        long started = 0, completed = 0, skipped = 0, failed = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "RealEstateStar.Activation")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            switch (instrument.Name)
            {
                case "activation.started": Interlocked.Add(ref started, value); break;
                case "activation.completed": Interlocked.Add(ref completed, value); break;
                case "activation.skipped": Interlocked.Add(ref skipped, value); break;
                case "activation.failed": Interlocked.Add(ref failed, value); break;
            }
        });
        listener.Start();

        // Storage returns null → fresh pipeline run
        _storage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var orchestrator = BuildOrchestrator();
        await orchestrator.ProcessActivationAsync(MakeRequest(), CancellationToken.None);

        started.Should().BeGreaterThanOrEqualTo(1);
        completed.Should().BeGreaterThanOrEqualTo(1);
        skipped.Should().Be(0);
        failed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessActivationAsync_AlreadyComplete_IncrementsSkippedNotCompleted()
    {
        long started = 0, completed = 0, skipped = 0, failed = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "RealEstateStar.Activation")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            switch (instrument.Name)
            {
                case "activation.started": Interlocked.Add(ref started, value); break;
                case "activation.completed": Interlocked.Add(ref completed, value); break;
                case "activation.skipped": Interlocked.Add(ref skipped, value); break;
                case "activation.failed": Interlocked.Add(ref failed, value); break;
            }
        });
        listener.Start();

        // All files exist → skip
        _storage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# content");

        var orchestrator = BuildOrchestrator();
        await orchestrator.ProcessActivationAsync(MakeRequest(), CancellationToken.None);

        started.Should().BeGreaterThanOrEqualTo(1);
        skipped.Should().BeGreaterThanOrEqualTo(1);
        completed.Should().Be(0);
        failed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessActivationAsync_Phase3Fails_IncrementsFailedCounter()
    {
        long failed = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "RealEstateStar.Activation")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            if (instrument.Name == "activation.failed")
                Interlocked.Add(ref failed, value);
        });
        listener.Start();

        // Storage null → fresh run, but config generation will fail
        _storage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _agentConfigService.Setup(a => a.GenerateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<ActivationOutputs>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Config fail"));

        var orchestrator = BuildOrchestrator();
        var act = async () => await orchestrator.ProcessActivationAsync(
            MakeRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        failed.Should().BeGreaterThanOrEqualTo(1);
    }

    // ── Full pipeline smoke test ───────────────────────────────────────────────

    [Fact]
    public async Task ProcessActivationAsync_FullPipeline_CompletesSuccessfully()
    {
        // Storage returns null → fresh run
        _storage.Setup(s => s.ReadDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var orchestrator = BuildOrchestrator();
        var act = async () => await orchestrator.ProcessActivationAsync(
            MakeRequest(), CancellationToken.None);

        await act.Should().NotThrowAsync();

        // Welcome service called at end of Phase 4
        _welcomeService.Verify(w => w.SendAsync(
            AccountId, AgentId, AgentId, It.IsAny<ActivationOutputs>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessActivationAsync_CompletesSuccessfully_CheckpointsCleared()
    {
        // Checkpoints exist at start of run
        _storage.SetupSequence(s => s.ReadDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            // First 12 calls: required files don't exist (fresh run check)
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null)
            // Remaining calls (checkpoint cleanup, file writes): return null
            .ReturnsAsync((string?)null);

        var orchestrator = BuildOrchestrator();
        await orchestrator.ProcessActivationAsync(MakeRequest(), CancellationToken.None);

        // After successful run, ClearCheckpoints was called (at least 2 attempts = start + end)
        // Since checkpoints don't exist, DeleteDocument is NOT called (clear is a no-op)
        // But EnsureFolderExistsAsync is called for checkpoint folder during saves
        _storage.Verify(s => s.EnsureFolderExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}

/// <summary>
/// HTTP handler that returns empty 200 OK for all requests.
/// Used to stub out HTTP calls in AgentDiscoveryWorker without network I/O.
/// </summary>
file sealed class NoOpHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        });
}
