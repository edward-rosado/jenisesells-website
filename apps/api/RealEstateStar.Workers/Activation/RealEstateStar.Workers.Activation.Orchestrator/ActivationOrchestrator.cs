using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.Activities.Activation.BrandMerge;
using RealEstateStar.Activities.Activation.PersistAgentProfile;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using AgentDiscoveryNs = RealEstateStar.Workers.Activation.AgentDiscovery;
using AgentDiscoveryModel = RealEstateStar.Domain.Activation.Models.AgentDiscovery;
using DriveIndexModel = RealEstateStar.Domain.Activation.Models.DriveIndex;
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

namespace RealEstateStar.Workers.Activation.Orchestrator;

/// <summary>
/// BackgroundService that reads from Channel&lt;ActivationRequest&gt; and coordinates
/// the 5-phase activation pipeline. Dispatches workers in parallel, calls activities
/// and services, and manages checkpoints for retry/resume semantics.
/// </summary>
public class ActivationOrchestrator : BackgroundService
{
    // ── Observability ─────────────────────────────────────────────────────────

    internal static readonly ActivitySource ActivitySource = new("RealEstateStar.Activation");

    private static readonly Meter _meter = new("RealEstateStar.Activation");
    internal static readonly Counter<long> StartedCounter =
        _meter.CreateCounter<long>("activation.started", description: "Activation pipeline starts");
    internal static readonly Counter<long> CompletedCounter =
        _meter.CreateCounter<long>("activation.completed", description: "Activation pipeline completions");
    internal static readonly Counter<long> SkippedCounter =
        _meter.CreateCounter<long>("activation.skipped", description: "Activation pipelines skipped (already complete)");
    internal static readonly Counter<long> FailedCounter =
        _meter.CreateCounter<long>("activation.failed", description: "Activation pipeline failures");

    // ── Checkpoint keys ───────────────────────────────────────────────────────

    internal const string CheckpointFolder = "activation";
    internal const string Phase1CheckpointFile = "checkpoint-phase1-gather.json";
    internal const string Phase2CheckpointFile = "checkpoint-phase2-synthesis.json";

    // ── Skip-if-complete file list ────────────────────────────────────────────

    internal static readonly IReadOnlyList<(string Folder, string File)> RequiredAgentFiles =
    [
        ("agentId", "Voice Skill.md"),
        ("agentId", "Personality Skill.md"),
        ("agentId", "Marketing Style.md"),
        ("agentId", "Sales Pipeline.md"),
        ("agentId", "Coaching Report.md"),
        ("agentId", "Agent Discovery.md"),
        ("agentId", "Branding Kit.md"),
        ("agentId", "Email Signature.md"),
        ("agentId", "headshot.jpg"),
        ("agentId", "Drive Index.md"),
    ];

    internal static readonly IReadOnlyList<(string Folder, string File)> RequiredAccountFiles =
    [
        ("accountId", "Brand Profile.md"),
        ("accountId", "Brand Voice.md"),
    ];

    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly ChannelReader<ActivationRequest> _channel;

    // Phase 1: gather workers
    private readonly AgentEmailFetchWorker _emailFetchWorker;
    private readonly DriveIndexWorker _driveIndexWorker;
    private readonly AgentDiscoveryNs.AgentDiscoveryWorker _discoveryWorker;

    // Phase 2: synthesis workers
    private readonly VoiceExtractionWorker _voiceWorker;
    private readonly PersonalityWorker _personalityWorker;
    private readonly BrandingDiscoveryWorker _brandingWorker;
    private readonly CmaStyleWorker _cmaStyleWorker;
    private readonly MarketingStyleWorker _marketingWorker;
    private readonly WebsiteStyleWorker _websiteWorker;
    private readonly PipelineAnalysisWorker _pipelineWorker;
    private readonly CoachingWorker _coachingWorker;
    private readonly BrandExtractionWorker _brandExtractionWorker;
    private readonly BrandVoiceWorker _brandVoiceWorker;
    private readonly ComplianceAnalysisWorker _complianceWorker;
    private readonly FeeStructureWorker _feeWorker;

    // Phase 3: activities
    private readonly AgentProfilePersistActivity _persistActivity;
    private readonly BrandMergeActivity _brandMergeActivity;

    // Phase 4: service
    private readonly IWelcomeNotificationService _welcomeService;

    // Infrastructure
    private readonly IDocumentStorageProvider _storage;
    private readonly IAgentContextLoader _contextLoader;
    private readonly ILogger<ActivationOrchestrator> _logger;

    public ActivationOrchestrator(
        ChannelReader<ActivationRequest> channel,
        AgentEmailFetchWorker emailFetchWorker,
        DriveIndexWorker driveIndexWorker,
        AgentDiscoveryNs.AgentDiscoveryWorker discoveryWorker,
        VoiceExtractionWorker voiceWorker,
        PersonalityWorker personalityWorker,
        BrandingDiscoveryWorker brandingWorker,
        CmaStyleWorker cmaStyleWorker,
        MarketingStyleWorker marketingWorker,
        WebsiteStyleWorker websiteWorker,
        PipelineAnalysisWorker pipelineWorker,
        CoachingWorker coachingWorker,
        BrandExtractionWorker brandExtractionWorker,
        BrandVoiceWorker brandVoiceWorker,
        ComplianceAnalysisWorker complianceWorker,
        FeeStructureWorker feeWorker,
        AgentProfilePersistActivity persistActivity,
        BrandMergeActivity brandMergeActivity,
        IWelcomeNotificationService welcomeService,
        IDocumentStorageProvider storage,
        IAgentContextLoader contextLoader,
        ILogger<ActivationOrchestrator> logger)
    {
        _channel = channel;
        _emailFetchWorker = emailFetchWorker;
        _driveIndexWorker = driveIndexWorker;
        _discoveryWorker = discoveryWorker;
        _voiceWorker = voiceWorker;
        _personalityWorker = personalityWorker;
        _brandingWorker = brandingWorker;
        _cmaStyleWorker = cmaStyleWorker;
        _marketingWorker = marketingWorker;
        _websiteWorker = websiteWorker;
        _pipelineWorker = pipelineWorker;
        _coachingWorker = coachingWorker;
        _brandExtractionWorker = brandExtractionWorker;
        _brandVoiceWorker = brandVoiceWorker;
        _complianceWorker = complianceWorker;
        _feeWorker = feeWorker;
        _persistActivity = persistActivity;
        _brandMergeActivity = brandMergeActivity;
        _welcomeService = welcomeService;
        _storage = storage;
        _contextLoader = contextLoader;
        _logger = logger;
    }

    // ── BackgroundService ─────────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var request in _channel.ReadAllAsync(ct))
        {
            await ProcessActivationAsync(request, ct);
        }
    }

    // ── Core pipeline ─────────────────────────────────────────────────────────

    internal virtual async Task ProcessActivationAsync(ActivationRequest request, CancellationToken ct)
    {
        using var rootSpan = ActivitySource.StartActivity(
            "activation.pipeline",
            ActivityKind.Internal,
            default(ActivityContext),
            [
                new("accountId", request.AccountId),
                new("agentId", request.AgentId),
            ]);

        StartedCounter.Add(1);

        _logger.LogInformation(
            "[ACTV-001] Starting activation pipeline for accountId={AccountId}, agentId={AgentId}",
            request.AccountId, request.AgentId);

        // Phase 0: skip-if-complete check
        if (await IsAlreadyCompleteAsync(request, ct))
        {
            _logger.LogInformation(
                "[ACTV-002] Activation already complete for accountId={AccountId}, agentId={AgentId} — sending welcome (idempotent)",
                request.AccountId, request.AgentId);

            SkippedCounter.Add(1);
            rootSpan?.SetTag("outcome", "skipped");

            await _welcomeService.SendAsync(request.AccountId, request.AgentId, request.AgentId, new ActivationOutputs(), ct);
            return;
        }

        // Clear stale checkpoints before a fresh run
        await ClearCheckpointsAsync(request, ct);

        try
        {
            // Phase 1: Gather
            using var phase1Span = ActivitySource.StartActivity("activation.phase1.gather");
            var (emailCorpus, driveIndex, discovery, agentName) = await RunPhase1Async(request, ct);
            await SavePhase1CheckpointAsync(request, emailCorpus, driveIndex, discovery, ct);
            phase1Span?.SetTag("outcome", "complete");

            // Phase 2: Synthesize
            using var phase2Span = ActivitySource.StartActivity("activation.phase2.synthesize");
            var outputs = await RunPhase2Async(request, agentName, emailCorpus, driveIndex, discovery, ct);
            await SavePhase2CheckpointAsync(request, outputs, ct);
            phase2Span?.SetTag("outcome", "complete");

            // Phase 3: Persist + Merge
            using var phase3Span = ActivitySource.StartActivity("activation.phase3.persist");
            await RunPhase3Async(request, outputs, discovery, ct);
            phase3Span?.SetTag("outcome", "complete");

            // Phase 4: Notify
            using var phase4Span = ActivitySource.StartActivity("activation.phase4.notify");
            await RunPhase4Async(request, outputs, ct);
            phase4Span?.SetTag("outcome", "complete");

            // Clean up transient checkpoints
            await ClearCheckpointsAsync(request, ct);

            CompletedCounter.Add(1);
            rootSpan?.SetTag("outcome", "completed");

            _logger.LogInformation(
                "[ACTV-003] Activation pipeline complete for accountId={AccountId}, agentId={AgentId}",
                request.AccountId, request.AgentId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            FailedCounter.Add(1);
            rootSpan?.SetTag("outcome", "failed");

            _logger.LogError(ex,
                "[ACTV-099] Activation pipeline failed for accountId={AccountId}, agentId={AgentId}",
                request.AccountId, request.AgentId);

            throw;
        }
    }

    // ── Phase 0: skip check ───────────────────────────────────────────────────

    internal async Task<bool> IsAlreadyCompleteAsync(ActivationRequest request, CancellationToken ct)
    {
        var agentFolder = $"real-estate-star/{request.AgentId}";
        var accountFolder = $"real-estate-star/{request.AccountId}";

        var checkTasks = new List<Task<bool>>();

        foreach (var (folderToken, file) in RequiredAgentFiles)
        {
            _ = folderToken; // token identifies that this is the agent folder
            checkTasks.Add(FileExistsAsync(agentFolder, file, ct));
        }

        foreach (var (folderToken, file) in RequiredAccountFiles)
        {
            _ = folderToken; // token identifies that this is the account folder
            checkTasks.Add(FileExistsAsync(accountFolder, file, ct));
        }

        var results = await Task.WhenAll(checkTasks);
        return results.All(exists => exists);
    }

    private async Task<bool> FileExistsAsync(string folder, string file, CancellationToken ct)
    {
        var content = await _storage.ReadDocumentAsync(folder, file, ct);
        return content is not null;
    }

    // ── Phase 1: Gather ───────────────────────────────────────────────────────

    internal async Task<(EmailCorpus EmailCorpus, DriveIndexModel DriveIndex, AgentDiscoveryModel Discovery, string AgentName)>
        RunPhase1Async(ActivationRequest request, CancellationToken ct)
    {
        _logger.LogInformation(
            "[ACTV-010] Phase 1: gathering corpus for accountId={AccountId}, agentId={AgentId}",
            request.AccountId, request.AgentId);

        var emailTask = _emailFetchWorker.RunAsync(request.AccountId, request.AgentId, ct);
        var driveTask = _driveIndexWorker.RunAsync(request.AccountId, request.AgentId, ct);

        // Load context for agent name before discovery
        var context = await _contextLoader.LoadAsync(request.AccountId, request.AgentId, ct);
        var agentName = context?.VoiceSkill ?? request.Email.Split('@')[0];

        await Task.WhenAll(emailTask, driveTask);

        var emailCorpus = emailTask.Result;
        var driveIndex = driveTask.Result;

        // Discovery needs email corpus for signature info
        var discovery = await _discoveryWorker.RunAsync(
            request.AccountId,
            request.AgentId,
            agentName,
            brokerageName: string.Empty,
            phoneNumber: null,
            emailSignature: emailCorpus.Signature,
            ct);

        return (emailCorpus, driveIndex, discovery, agentName);
    }

    // ── Phase 2: Synthesize ───────────────────────────────────────────────────

    internal async Task<ActivationOutputs> RunPhase2Async(
        ActivationRequest request,
        string agentName,
        EmailCorpus emailCorpus,
        DriveIndexModel driveIndex,
        AgentDiscoveryModel discovery,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "[ACTV-020] Phase 2: synthesizing for accountId={AccountId}, agentId={AgentId}",
            request.AccountId, request.AgentId);

        var voiceTask = RunSafeAsync(
            "[ACTV-021] voice", request,
            () => _voiceWorker.ExtractAsync(agentName, emailCorpus, driveIndex, discovery, ct));

        var personalityTask = RunSafeAsync(
            "[ACTV-022] personality", request,
            () => _personalityWorker.ExtractAsync(agentName, emailCorpus, driveIndex, discovery, ct));

        var brandingTask = RunSafeAsync(
            "[ACTV-023] branding", request,
            () => _brandingWorker.DiscoverAsync(agentName, discovery, emailCorpus, driveIndex, ct));

        var cmaTask = RunSafeAsync(
            "[ACTV-024] cma-style", request,
            () => _cmaStyleWorker.AnalyzeAsync(driveIndex, ct));

        var marketingTask = RunSafeMarketingAsync(request, emailCorpus, driveIndex, ct);

        var websiteTask = RunSafeAsync(
            "[ACTV-026] website-style", request,
            () => _websiteWorker.AnalyzeAsync(discovery, ct));

        var pipelineTask = RunSafeAsync(
            "[ACTV-027] pipeline-analysis", request,
            () => _pipelineWorker.AnalyzeAsync(emailCorpus, driveIndex, ct));

        var coachingTask = RunSafeAsync(
            "[ACTV-028] coaching", request,
            () => _coachingWorker.AnalyzeAsync(agentName, emailCorpus, driveIndex, discovery, ct));

        var brandExtractionTask = RunSafeAsync(
            "[ACTV-029] brand-extraction", request,
            () => _brandExtractionWorker.AnalyzeAsync(emailCorpus, driveIndex, discovery, ct));

        var brandVoiceTask = RunSafeAsync(
            "[ACTV-030] brand-voice", request,
            () => _brandVoiceWorker.AnalyzeAsync(emailCorpus, driveIndex, discovery, ct));

        var complianceTask = RunSafeAsync(
            "[ACTV-031] compliance", request,
            () => _complianceWorker.AnalyzeAsync(emailCorpus, driveIndex, discovery, ct));

        var feeTask = RunSafeAsync(
            "[ACTV-032] fee-structure", request,
            () => _feeWorker.AnalyzeAsync(emailCorpus, driveIndex, discovery.Websites, ct));

        await Task.WhenAll(
            voiceTask, personalityTask, brandingTask, cmaTask, marketingTask,
            websiteTask, pipelineTask, coachingTask, brandExtractionTask,
            brandVoiceTask, complianceTask, feeTask);

        var voiceResult = voiceTask.Result;
        var personalityResult = personalityTask.Result;
        var brandingResult = brandingTask.Result;
        var coachingResult = coachingTask.Result;
        var marketingResult = marketingTask.Result;

        // Derive identity from email corpus signature + discovery
        var sig = emailCorpus.Signature;
        var serviceAreas = discovery.Profiles
            .SelectMany(p => p.ServiceAreas)
            .Distinct()
            .ToList();

        return new ActivationOutputs
        {
            VoiceSkill = voiceResult?.VoiceSkillMarkdown,
            PersonalitySkill = personalityResult?.PersonalitySkillMarkdown,
            CmaStyleGuide = cmaTask.Result,
            MarketingStyle = marketingResult.StyleGuide,
            WebsiteStyleGuide = websiteTask.Result,
            SalesPipeline = pipelineTask.Result,
            CoachingReport = coachingResult?.CoachingReportMarkdown,
            BrandingKitMarkdown = brandingResult?.BrandingKitMarkdown,
            BrandingKit = brandingResult?.Kit,
            BrandExtractionSignals = brandExtractionTask.Result,
            BrandVoiceSignals = brandVoiceTask.Result,
            ComplianceAnalysis = complianceTask.Result,
            FeeStructure = feeTask.Result,
            DriveIndex = BuildDriveIndexMarkdown(driveIndex),
            AgentDiscoveryMarkdown = BuildDiscoveryMarkdown(discovery),
            EmailSignature = BuildEmailSignatureMarkdown(sig),
            HeadshotBytes = discovery.HeadshotBytes,
            BrokerageLogoBytes = discovery.LogoBytes,
            Discovery = discovery,
            AgentName = sig?.Name,
            AgentEmail = request.Email,
            AgentPhone = sig?.Phone ?? discovery.Phone,
            AgentTitle = sig?.Title,
            AgentLicenseNumber = sig?.LicenseNumber,
            ServiceAreas = serviceAreas,
        };
    }

    // ── Phase 3: Persist + Merge ──────────────────────────────────────────────

    internal async Task RunPhase3Async(
        ActivationRequest request,
        ActivationOutputs outputs,
        AgentDiscoveryModel discovery,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "[ACTV-040] Phase 3: persisting for accountId={AccountId}, agentId={AgentId}",
            request.AccountId, request.AgentId);

        try
        {
            await _persistActivity.ExecuteAsync(
                request.AccountId, request.AgentId, request.AgentId, outputs, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "[ACTV-041] Phase 3 persist failed for accountId={AccountId}, agentId={AgentId} — pipeline aborted",
                request.AccountId, request.AgentId);
            throw;
        }

        try
        {
            var brandingKit = outputs.BrandingKitMarkdown ?? string.Empty;
            var voiceSkill = outputs.VoiceSkill ?? string.Empty;

            await _brandMergeActivity.ExecuteAsync(
                request.AccountId, request.AgentId, brandingKit, voiceSkill, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "[ACTV-042] Phase 3 brand merge failed for accountId={AccountId}, agentId={AgentId} — pipeline aborted",
                request.AccountId, request.AgentId);
            throw;
        }
    }

    // ── Phase 4: Notify ───────────────────────────────────────────────────────

    internal async Task RunPhase4Async(ActivationRequest request, ActivationOutputs outputs, CancellationToken ct)
    {
        _logger.LogInformation(
            "[ACTV-050] Phase 4: sending welcome notification for accountId={AccountId}, agentId={AgentId}",
            request.AccountId, request.AgentId);

        await _welcomeService.SendAsync(request.AccountId, request.AgentId, request.AgentId, outputs, ct);
    }

    // ── Checkpoint management ─────────────────────────────────────────────────

    private string CheckpointFolderPath(ActivationRequest request) =>
        $"real-estate-star/{request.AgentId}/{CheckpointFolder}";

    internal async Task SavePhase1CheckpointAsync(
        ActivationRequest request,
        EmailCorpus emailCorpus,
        DriveIndexModel driveIndex,
        AgentDiscoveryModel discovery,
        CancellationToken ct)
    {
        var folder = CheckpointFolderPath(request);
        await _storage.EnsureFolderExistsAsync(folder, ct);

        var checkpoint = new Phase1Checkpoint(
            CorpusHash: ComputeCorpusHash(emailCorpus),
            SentEmailCount: emailCorpus.SentEmails.Count,
            InboxEmailCount: emailCorpus.InboxEmails.Count,
            DriveFileCount: driveIndex.Files.Count,
            WebsitesFound: discovery.Websites.Count,
            WhatsAppEnabled: discovery.WhatsAppEnabled,
            HeadshotFound: discovery.HeadshotBytes is not null,
            SavedAt: DateTime.UtcNow);

        var json = JsonSerializer.Serialize(checkpoint);
        await _storage.WriteDocumentAsync(folder, Phase1CheckpointFile, json, ct);
    }

    internal async Task SavePhase2CheckpointAsync(
        ActivationRequest request,
        ActivationOutputs outputs,
        CancellationToken ct)
    {
        var folder = CheckpointFolderPath(request);
        await _storage.EnsureFolderExistsAsync(folder, ct);

        var workerStatus = new Dictionary<string, string>
        {
            ["voice"] = outputs.VoiceSkill is not null ? "completed" : "skipped",
            ["personality"] = outputs.PersonalitySkill is not null ? "completed" : "skipped",
            ["cma-style"] = outputs.CmaStyleGuide is not null ? "completed" : "skipped",
            ["marketing"] = outputs.MarketingStyle is not null ? "completed" : "skipped",
            ["website-style"] = outputs.WebsiteStyleGuide is not null ? "completed" : "skipped",
            ["pipeline"] = outputs.SalesPipeline is not null ? "completed" : "skipped",
            ["coaching"] = outputs.CoachingReport is not null ? "completed" : "skipped",
            ["branding"] = outputs.BrandingKitMarkdown is not null ? "completed" : "skipped",
            ["brand-extraction"] = outputs.BrandExtractionSignals is not null ? "completed" : "skipped",
            ["brand-voice"] = outputs.BrandVoiceSignals is not null ? "completed" : "skipped",
            ["compliance"] = outputs.ComplianceAnalysis is not null ? "completed" : "skipped",
            ["fee-structure"] = outputs.FeeStructure is not null ? "completed" : "skipped",
        };

        var checkpoint = new Phase2Checkpoint(workerStatus, SavedAt: DateTime.UtcNow);
        var json = JsonSerializer.Serialize(checkpoint);
        await _storage.WriteDocumentAsync(folder, Phase2CheckpointFile, json, ct);
    }

    internal async Task ClearCheckpointsAsync(ActivationRequest request, CancellationToken ct)
    {
        var folder = CheckpointFolderPath(request);

        try
        {
            var existingP1 = await _storage.ReadDocumentAsync(folder, Phase1CheckpointFile, ct);
            if (existingP1 is not null)
                await _storage.DeleteDocumentAsync(folder, Phase1CheckpointFile, ct);

            var existingP2 = await _storage.ReadDocumentAsync(folder, Phase2CheckpointFile, ct);
            if (existingP2 is not null)
                await _storage.DeleteDocumentAsync(folder, Phase2CheckpointFile, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Non-fatal: checkpoint cleanup failure should not stop the pipeline
            _logger.LogWarning(ex,
                "[ACTV-060] Failed to clear checkpoints for agentId={AgentId}", request.AgentId);
        }
    }

    internal async Task<Phase1Checkpoint?> TryLoadPhase1CheckpointAsync(
        ActivationRequest request,
        CancellationToken ct)
    {
        var folder = CheckpointFolderPath(request);
        var json = await _storage.ReadDocumentAsync(folder, Phase1CheckpointFile, ct);
        if (json is null) return null;

        try
        {
            return JsonSerializer.Deserialize<Phase1Checkpoint>(json);
        }
        catch
        {
            return null;
        }
    }

    // ── Marketing-specific wrapper (value tuple return) ───────────────────────

    private async Task<(string? StyleGuide, string? BrandSignals)> RunSafeMarketingAsync(
        ActivationRequest request,
        EmailCorpus emailCorpus,
        DriveIndexModel driveIndex,
        CancellationToken ct)
    {
        try
        {
            return await _marketingWorker.AnalyzeAsync(emailCorpus, driveIndex, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "[ACTV-025] marketing worker failed for accountId={AccountId}, agentId={AgentId} — continuing with remaining workers",
                request.AccountId, request.AgentId);
            return (null, null);
        }
    }

    // ── Safe wrapper ──────────────────────────────────────────────────────────

    private async Task<T?> RunSafeAsync<T>(
        string logPrefix,
        ActivationRequest request,
        Func<Task<T>> work) where T : class?
    {
        try
        {
            return await work();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "{LogPrefix} worker failed for accountId={AccountId}, agentId={AgentId} — continuing with remaining workers",
                logPrefix, request.AccountId, request.AgentId);
            return null;
        }
    }


    // ── Markdown builders ─────────────────────────────────────────────────────

    private static string BuildDriveIndexMarkdown(DriveIndexModel driveIndex)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Drive Index");
        sb.AppendLine();
        sb.AppendLine($"Folder ID: `{driveIndex.FolderId}`");
        sb.AppendLine();
        sb.AppendLine($"## Files ({driveIndex.Files.Count})");
        foreach (var file in driveIndex.Files)
            sb.AppendLine($"- [{file.Name}] ({file.Category}) — {file.MimeType}");
        return sb.ToString();
    }

    private static string BuildDiscoveryMarkdown(AgentDiscoveryModel discovery)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Agent Discovery");
        sb.AppendLine();
        sb.AppendLine($"Phone: {discovery.Phone ?? "N/A"}");
        sb.AppendLine($"WhatsApp Enabled: {discovery.WhatsAppEnabled}");
        sb.AppendLine();
        sb.AppendLine($"## Websites ({discovery.Websites.Count})");
        foreach (var site in discovery.Websites)
            sb.AppendLine($"- {site.Url} ({site.Source})");
        sb.AppendLine();
        sb.AppendLine($"## Third-Party Profiles ({discovery.Profiles.Count})");
        foreach (var profile in discovery.Profiles)
            sb.AppendLine($"- {profile.Platform}: {profile.Reviews.Count} reviews");
        return sb.ToString();
    }

    private static string? BuildEmailSignatureMarkdown(EmailSignature? sig)
    {
        if (sig is null) return null;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Email Signature");
        sb.AppendLine();
        if (sig.Name is not null) sb.AppendLine($"Name: {sig.Name}");
        if (sig.Title is not null) sb.AppendLine($"Title: {sig.Title}");
        if (sig.Phone is not null) sb.AppendLine($"Phone: {sig.Phone}");
        if (sig.LicenseNumber is not null) sb.AppendLine($"License: {sig.LicenseNumber}");
        if (sig.BrokerageName is not null) sb.AppendLine($"Brokerage: {sig.BrokerageName}");
        return sb.ToString();
    }

    private static string ComputeCorpusHash(EmailCorpus corpus)
    {
        var raw = $"{corpus.SentEmails.Count}:{corpus.InboxEmails.Count}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16];
    }
}

// ── Checkpoint models ─────────────────────────────────────────────────────────

public sealed record Phase1Checkpoint(
    string CorpusHash,
    int SentEmailCount,
    int InboxEmailCount,
    int DriveFileCount,
    int WebsitesFound,
    bool WhatsAppEnabled,
    bool HeadshotFound,
    DateTime SavedAt);

public sealed record Phase2Checkpoint(
    IReadOnlyDictionary<string, string> WorkerStatus,
    DateTime SavedAt);
