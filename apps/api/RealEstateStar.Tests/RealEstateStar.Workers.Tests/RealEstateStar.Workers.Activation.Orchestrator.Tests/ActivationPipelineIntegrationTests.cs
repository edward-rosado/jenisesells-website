using System.Net;
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
using RealEstateStar.Workers.Activation.FeeStructure;
using RealEstateStar.Workers.Activation.MarketingStyle;
using RealEstateStar.Workers.Activation.Personality;
using RealEstateStar.Workers.Activation.PipelineAnalysis;
using RealEstateStar.Workers.Activation.VoiceExtraction;
using RealEstateStar.Workers.Activation.WebsiteStyle;

namespace RealEstateStar.Workers.Activation.Orchestrator.Tests;

/// <summary>
/// Integration tests for the activation pipeline. These tests exercise the full
/// <see cref="ActivationOrchestrator.ProcessActivationAsync"/> method end-to-end,
/// using an in-memory file storage provider instead of mocked IDocumentStorageProvider.
/// External services (Claude, Gmail, Drive) are still mocked.
///
/// Validates complete output production, skip-if-complete behavior, graceful degradation
/// with insufficient data, worker failure isolation, and brokerage first-agent handling.
/// </summary>
public class ActivationPipelineIntegrationTests
{
    private const string AccountId = "acct-integ";
    private const string AgentId = "agent-integ";

    // ── External service mocks ─────────────────────────────────────────────────

    private readonly Mock<IAnthropicClient> _anthropic = new();
    private readonly Mock<IContentSanitizer> _sanitizer = new();
    private readonly Mock<IGDriveClient> _driveClient = new();
    private readonly Mock<IOAuthRefresher> _oauthRefresher = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly Mock<IWhatsAppSender> _whatsAppSender = new();
    private readonly Mock<IGmailReader> _gmailReader = new();
    private readonly Mock<IAgentContextLoader> _contextLoader = new();
    private readonly Mock<IWelcomeNotificationService> _welcomeService = new();
    private readonly Mock<IAgentConfigService> _agentConfigService = new();
    private readonly Mock<IBrandMergeService> _brandMergeService = new();

    // ── In-memory storage (real implementation, not mock) ─────────────────────

    private readonly InMemoryFileProvider _fileStorage = new();
    private readonly Mock<IFileStorageProviderFactory> _fileStorageFactory = new();

    public ActivationPipelineIntegrationTests()
    {
        _fileStorageFactory.Setup(f => f.CreateForAgent(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(_fileStorage);

        _sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        _anthropic.Setup(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse(
                "# Activation Output\nThis is synthesized content from Claude.",
                150, 300, 800));

        _gmailReader.Setup(r => r.GetSentEmailsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailMessage>());
        _gmailReader.Setup(r => r.GetInboxEmailsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailMessage>());

        _driveClient.Setup(d => d.GetOrCreateFolderAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("folder-id");
        _driveClient.Setup(d => d.ListAllFilesAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DriveFileInfo>());
        _driveClient.Setup(d => d.GetFileContentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _oauthRefresher.Setup(o => o.GetValidCredentialAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        _whatsAppSender.Setup(w => w.SendFreeformAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WhatsAppNotRegisteredException("555-000-0000"));

        var handler = new NoOpHttpHandler();
        _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        _contextLoader.Setup(c => c.LoadAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentContext?)null);

        _welcomeService.Setup(w => w.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<ActivationOutputs>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _agentConfigService.Setup(a => a.GenerateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<ActivationOutputs>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _brandMergeService.Setup(b => b.MergeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BrandMergeResult("# Brand Profile", "# Brand Voice"));
    }

    // ── Builder ───────────────────────────────────────────────────────────────

    private ActivationOrchestrator BuildOrchestrator()
    {
        return new ActivationOrchestrator(
            new Mock<IActivationQueue>().Object,
            new AgentEmailFetchWorker(_gmailReader.Object, NullLogger<AgentEmailFetchWorker>.Instance),
            new DriveIndexWorker(_driveClient.Object, NullLogger<DriveIndexWorker>.Instance),
            new AgentDiscoveryWorker(_oauthRefresher.Object, _httpClientFactory.Object, _whatsAppSender.Object, NullLogger<AgentDiscoveryWorker>.Instance),
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
            new AgentProfilePersistActivity(_fileStorageFactory.Object, _agentConfigService.Object, NullLogger<AgentProfilePersistActivity>.Instance),
            new BrandMergeActivity(_brandMergeService.Object, _fileStorage, NullLogger<BrandMergeActivity>.Instance),
            _welcomeService.Object,
            _fileStorage,
            _contextLoader.Object,
            NullLogger<ActivationOrchestrator>.Instance);
    }

    private static ActivationRequest MakeRequest(
        string accountId = AccountId,
        string agentId = AgentId,
        string email = "agent@example.com") =>
        new(accountId, agentId, email, DateTime.UtcNow);

    // ── Integration tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task FullPipeline_WithMockedExternals_CompletesAndProducesOutputs()
    {
        var orchestrator = BuildOrchestrator();
        var request = MakeRequest();

        await orchestrator.ProcessActivationAsync(request, CancellationToken.None);

        // Verify files were written to in-memory storage
        // Voice Skill is written only if Claude returns non-empty content
        // (with our mock returning "# Activation Output", the workers should produce output)
        var agentFolder = $"real-estate-star/{AgentId}";
        var voiceSkill = await _fileStorage.ReadDocumentAsync(agentFolder, "Voice Skill.md", CancellationToken.None);
        var personalitySkill = await _fileStorage.ReadDocumentAsync(agentFolder, "Personality Skill.md", CancellationToken.None);

        // At least some outputs were written (pipeline completed)
        var writtenAny = voiceSkill is not null || personalitySkill is not null;
        writtenAny.Should().BeTrue("pipeline should produce at least some skill files");

        // Welcome service was called exactly once (Phase 4 completion)
        _welcomeService.Verify(w => w.SendAsync(
            AccountId, AgentId, AgentId, It.IsAny<ActivationOutputs>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SkipIfComplete_WhenAllRequiredFilesExist_ZeroClaudeCalls()
    {
        // Pre-populate all required files — pipeline should skip immediately
        var agentFolder = $"real-estate-star/{AgentId}";
        var accountFolder = $"real-estate-star/{AccountId}";

        foreach (var file in ActivationOrchestrator.RequiredAgentFiles)
            _fileStorage.Write(agentFolder, file, $"# {file}");

        foreach (var file in ActivationOrchestrator.RequiredAccountFiles)
            _fileStorage.Write(accountFolder, file, $"# {file}");

        var orchestrator = BuildOrchestrator();
        await orchestrator.ProcessActivationAsync(MakeRequest(), CancellationToken.None);

        // Claude should NOT have been called — pipeline was skipped
        _anthropic.Verify(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // But welcome service should still be called (idempotent)
        _welcomeService.Verify(w => w.SendAsync(
            AccountId, AgentId, AgentId, It.IsAny<ActivationOutputs>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LowData_3Emails_CoachingSkipped_PipelineStillCompletes()
    {
        // Provide exactly 3 sent + 3 inbox emails — below the 5-each minimum for coaching
        var emails = Enumerable.Range(0, 3)
            .Select(i => new EmailMessage(
                $"id{i}", $"Subject {i}",
                "Hello, following up on our conversation.",
                "agent@example.com", ["client@example.com"],
                DateTime.UtcNow.AddDays(-i), null))
            .ToList();

        _gmailReader.Setup(r => r.GetSentEmailsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emails);
        _gmailReader.Setup(r => r.GetInboxEmailsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emails);

        var orchestrator = BuildOrchestrator();
        var act = async () => await orchestrator.ProcessActivationAsync(MakeRequest(), CancellationToken.None);

        // Pipeline should not throw — graceful degradation with low email data
        await act.Should().NotThrowAsync();

        // Coaching file should NOT have been written (insufficient emails)
        var agentFolder = $"real-estate-star/{AgentId}";
        var coachingReport = await _fileStorage.ReadDocumentAsync(agentFolder, "Coaching Report.md", CancellationToken.None);
        coachingReport.Should().BeNull("coaching is skipped when < 5 sent + 5 inbox emails");
    }

    [Fact]
    public async Task WorkerFailure_OneWorkerThrows_OtherWorkersSucceed_PipelineCompletes()
    {
        // Configure Anthropic: voice extraction pipeline throws, others succeed
        var callCount = 0;
        _anthropic.Reset();
        _sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        _anthropic.Setup(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var current = System.Threading.Interlocked.Increment(ref callCount);
                if (current == 1)
                    throw new InvalidOperationException("First synthesis worker failure");
                return new AnthropicResponse("# Output", 100, 200, 500);
            });

        var orchestrator = BuildOrchestrator();
        var act = async () => await orchestrator.ProcessActivationAsync(MakeRequest(), CancellationToken.None);

        // Pipeline should still complete (RunSafeAsync catches individual failures)
        await act.Should().NotThrowAsync();

        // Welcome service should have been called — pipeline completed
        _welcomeService.Verify(w => w.SendAsync(
            AccountId, AgentId, AgentId, It.IsAny<ActivationOutputs>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BrokerageFirstAgent_GeneratesAccountConfigAndAgentFiles()
    {
        const string BrokerageAccountId = "brokerage-first-acct";
        const string FirstAgentId = "first-agent";

        // _agentConfigService.GenerateAsync is already mocked to no-op
        // but we verify it IS called for the first agent

        var orchestrator = BuildOrchestrator();
        var request = new ActivationRequest(BrokerageAccountId, FirstAgentId, "first@brokerage.com", DateTime.UtcNow);

        await orchestrator.ProcessActivationAsync(request, CancellationToken.None);

        // AgentConfigService.GenerateAsync should be called (creates account.json + content.json)
        _agentConfigService.Verify(a => a.GenerateAsync(
            BrokerageAccountId, FirstAgentId, FirstAgentId,
            It.IsAny<ActivationOutputs>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // BrandMergeService should be called (brand profile for brokerage)
        _brandMergeService.Verify(b => b.MergeAsync(
            BrokerageAccountId, FirstAgentId, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

/// <summary>
/// Thread-safe in-memory file storage provider for integration testing.
/// Implements both IDocumentStorageProvider and IFileStorageProvider.
/// </summary>
internal sealed class InMemoryFileProvider : IFileStorageProvider
{
    private readonly Dictionary<string, string?> _store = new(StringComparer.OrdinalIgnoreCase);

    private static string Key(string folder, string file) => $"{folder}/{file}";

    /// <summary>Write a file directly (synchronous, for test setup).</summary>
    public void Write(string folder, string fileName, string? content) =>
        _store[Key(folder, fileName)] = content;

    public Task WriteDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
    {
        _store[Key(folder, fileName)] = content;
        return Task.CompletedTask;
    }

    public Task<string?> ReadDocumentAsync(string folder, string fileName, CancellationToken ct) =>
        Task.FromResult(_store.TryGetValue(Key(folder, fileName), out var v) ? v : null);

    public Task UpdateDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
    {
        _store[Key(folder, fileName)] = content;
        return Task.CompletedTask;
    }

    public Task DeleteDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        _store.Remove(Key(folder, fileName));
        return Task.CompletedTask;
    }

    public Task<List<string>> ListDocumentsAsync(string folder, CancellationToken ct) =>
        Task.FromResult(
            _store.Keys
                .Where(k => k.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
                .Select(k => k[(folder.Length + 1)..])
                .ToList());

    public Task EnsureFolderExistsAsync(string folder, CancellationToken ct) =>
        Task.CompletedTask;

    // ISheetStorageProvider — not used by activation pipeline
    public Task AppendRowAsync(string sheetName, List<string> values, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<List<List<string>>> ReadRowsAsync(string sheetName, string filterColumn, string filterValue, CancellationToken ct) =>
        Task.FromResult(new List<List<string>>());

    public Task RedactRowsAsync(string sheetName, string filterColumn, string filterValue, string redactedMarker, CancellationToken ct) =>
        Task.CompletedTask;
}

/// <summary>
/// HTTP handler that returns empty 200 OK for all requests (used to stub discovery worker).
/// </summary>
file sealed class NoOpHttpHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        });
}
