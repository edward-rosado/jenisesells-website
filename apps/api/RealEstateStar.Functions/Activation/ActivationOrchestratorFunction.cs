using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Activation;

/// <summary>
/// Durable Functions orchestrator for the activation pipeline.
///
/// Maps directly from <c>ActivationOrchestrator.ProcessActivationAsync()</c> — same 4 phases.
///
/// Replay-safety rules (enforced):
/// - No DateTime.UtcNow — use ctx.CurrentUtcDateTime
/// - No Guid.NewGuid() — not needed here
/// - No I/O calls — all I/O is in activity functions
/// - Logging is guarded by !ctx.IsReplaying
/// </summary>
public sealed class ActivationOrchestratorFunction
{
    /// <summary>
    /// Deterministic instance ID for dedup: at most one activation per agent at a time.
    /// </summary>
    public static string InstanceId(string accountId, string agentId) =>
        $"activation-{accountId}-{agentId}";

    [Function("ActivationOrchestrator")]
    public static async Task RunAsync(
        [OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var request = ctx.GetInput<ActivationRequest>()
            ?? throw new InvalidOperationException("[ACTV-FN-001] Orchestrator input is null.");

        var logger = ctx.CreateReplaySafeLogger<ActivationOrchestratorFunction>();

        if (!ctx.IsReplaying)
        {
            logger.LogInformation(
                "[ACTV-FN-002] Starting activation orchestration for accountId={AccountId}, agentId={AgentId}",
                request.AccountId, request.AgentId);
        }

        // ── Phase 0: skip-if-complete ────────────────────────────────────────

        var completeCheck = await ctx.CallActivityAsync<CheckActivationCompleteOutput>(
            ActivityNames.CheckActivationComplete,
            new CheckActivationCompleteInput(request.AccountId, request.AgentId));

        if (completeCheck.IsComplete)
        {
            if (!ctx.IsReplaying)
            {
                logger.LogInformation(
                    "[ACTV-FN-003] Activation already complete for accountId={AccountId}, agentId={AgentId} — sending welcome (idempotent)",
                    request.AccountId, request.AgentId);
            }

            await ctx.CallActivityAsync(
                ActivityNames.WelcomeNotification,
                new WelcomeNotificationInput(
                    AccountId: request.AccountId,
                    AgentId: request.AgentId,
                    Handle: request.AgentId,
                    AgentName: null,
                    AgentPhone: null,
                    WhatsAppEnabled: false));

            return;
        }

        // ── Phase 1: Gather ──────────────────────────────────────────────────

        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-010] Phase 1: gather for accountId={AccountId}", request.AccountId);

        // Email and Drive run in parallel
        var emailTask = ctx.CallActivityAsync<EmailFetchOutput>(
            ActivityNames.EmailFetch,
            new EmailFetchInput(request.AccountId, request.AgentId));

        var driveTask = ctx.CallActivityAsync<DriveIndexOutput>(
            ActivityNames.DriveIndex,
            new DriveIndexInput(request.AccountId, request.AgentId));

        await Task.WhenAll(emailTask, driveTask);

        var emailCorpus = emailTask.Result;
        var driveIndex = driveTask.Result;

        // Discovery requires email corpus to use signature info.
        // Validate email before splitting to avoid IndexOutOfRangeException on malformed input.
        var agentName = !string.IsNullOrWhiteSpace(request.Email) && request.Email.Contains('@')
            ? request.Email.Split('@')[0].Trim()
            : request.AccountId; // fallback to account ID when email is absent or malformed

        var discovery = await ctx.CallActivityAsync<AgentDiscoveryOutput>(
            ActivityNames.AgentDiscovery,
            new AgentDiscoveryInput(
                AccountId: request.AccountId,
                AgentId: request.AgentId,
                AgentName: agentName,
                EmailSignature: emailCorpus.Signature));

        // ── Phase 2: Synthesize (12 workers in parallel) ──────────────────────

        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-020] Phase 2: synthesize for agentId={AgentId}", request.AgentId);

        var synthesisInput = new SynthesisInput(
            AccountId: request.AccountId,
            AgentId: request.AgentId,
            AgentName: agentName,
            EmailCorpus: emailCorpus,
            DriveIndex: driveIndex,
            Discovery: discovery);

        // Each worker wrapped in try/catch to preserve RunSafeAsync semantics:
        // one worker failure does NOT abort the pipeline — it contributes null output.

        var voiceTask = WrapAsync<VoiceExtractionOutput>(
            ctx, ActivityNames.VoiceExtraction, synthesisInput, "[ACTV-FN-021] voice", logger);
        var personalityTask = WrapAsync<PersonalityOutput>(
            ctx, ActivityNames.Personality, synthesisInput, "[ACTV-FN-022] personality", logger);
        var brandingTask = WrapAsync<BrandingDiscoveryOutput>(
            ctx, ActivityNames.BrandingDiscovery, synthesisInput, "[ACTV-FN-023] branding", logger);
        var cmaTask = WrapAsync<StringOutput>(
            ctx, ActivityNames.CmaStyle, synthesisInput, "[ACTV-FN-024] cma-style", logger);
        var marketingTask = WrapAsync<MarketingStyleOutput>(
            ctx, ActivityNames.MarketingStyle, synthesisInput, "[ACTV-FN-025] marketing", logger);
        var websiteTask = WrapAsync<StringOutput>(
            ctx, ActivityNames.WebsiteStyle, synthesisInput, "[ACTV-FN-026] website-style", logger);
        var pipelineTask = WrapAsync<StringOutput>(
            ctx, ActivityNames.PipelineAnalysis, synthesisInput, "[ACTV-FN-027] pipeline", logger);
        var coachingTask = WrapAsync<CoachingOutput>(
            ctx, ActivityNames.Coaching, synthesisInput, "[ACTV-FN-028] coaching", logger);
        var brandExtractionTask = WrapAsync<StringOutput>(
            ctx, ActivityNames.BrandExtraction, synthesisInput, "[ACTV-FN-029] brand-extraction", logger);
        var brandVoiceTask = WrapAsync<StringOutput>(
            ctx, ActivityNames.BrandVoice, synthesisInput, "[ACTV-FN-030] brand-voice", logger);
        var complianceTask = WrapAsync<StringOutput>(
            ctx, ActivityNames.ComplianceAnalysis, synthesisInput, "[ACTV-FN-031] compliance", logger);
        var feeTask = WrapAsync<StringOutput>(
            ctx, ActivityNames.FeeStructure, synthesisInput, "[ACTV-FN-032] fee-structure", logger);

        await Task.WhenAll(
            voiceTask, personalityTask, brandingTask, cmaTask, marketingTask,
            websiteTask, pipelineTask, coachingTask, brandExtractionTask,
            brandVoiceTask, complianceTask, feeTask);

        var voice = voiceTask.Result;
        var personality = personalityTask.Result;
        var branding = brandingTask.Result;
        var cmaStyle = cmaTask.Result?.Value;
        var marketing = marketingTask.Result;
        var websiteStyle = websiteTask.Result?.Value;
        var salesPipeline = pipelineTask.Result?.Value;
        var coaching = coachingTask.Result;
        var brandExtraction = brandExtractionTask.Result?.Value;
        var brandVoice = brandVoiceTask.Result?.Value;
        var compliance = complianceTask.Result?.Value;
        var feeStructure = feeTask.Result?.Value;

        // ── Phase 2.5: Contact Detection ──────────────────────────────────────

        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-035] Phase 2.5: contact detection for agentId={AgentId}", request.AgentId);

        ContactDetectionOutput contactDetectionResult;
        try
        {
            contactDetectionResult = await ctx.CallActivityAsync<ContactDetectionOutput>(
                ActivityNames.ContactDetection,
                new ContactDetectionInput(
                    AccountId: request.AccountId,
                    AgentId: request.AgentId,
                    DriveExtractions: driveIndex.Extractions,
                    EmailCorpus: emailCorpus));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!ctx.IsReplaying)
            {
                logger.LogWarning(ex,
                    "[ACTV-FN-036] Phase 2.5 contact detection failed for agentId={AgentId} — continuing without contacts",
                    request.AgentId);
            }
            contactDetectionResult = new ContactDetectionOutput(Array.Empty<ImportedContactDto>());
        }

        // ── Phase 3: Persist + Merge ──────────────────────────────────────────

        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-040] Phase 3: persist for agentId={AgentId}", request.AgentId);

        // Build drive index and discovery markdown (deterministic — no I/O)
        var driveIndexMarkdown = BuildDriveIndexMarkdown(driveIndex);
        var discoveryMarkdown = BuildDiscoveryMarkdown(discovery);
        var emailSigMarkdown = BuildEmailSignatureMarkdown(emailCorpus.Signature);
        var serviceAreas = discovery.Profiles
            .SelectMany(p => p.ServiceAreas)
            .Distinct()
            .ToList();

        var persistInput = new PersistProfileInput(
            AccountId: request.AccountId,
            AgentId: request.AgentId,
            Handle: request.AgentId,
            Voice: voice,
            Personality: personality,
            CmaStyle: cmaStyle,
            Marketing: marketing,
            WebsiteStyle: websiteStyle,
            SalesPipeline: salesPipeline,
            Coaching: coaching,
            Branding: branding,
            BrandExtraction: brandExtraction,
            BrandVoice: brandVoice,
            Compliance: compliance,
            FeeStructure: feeStructure,
            DriveIndexMarkdown: driveIndexMarkdown,
            DiscoveryMarkdown: discoveryMarkdown,
            EmailSignatureMarkdown: emailSigMarkdown,
            HeadshotBytes: discovery.HeadshotBytes,
            BrokerageLogoBytes: discovery.LogoBytes,
            AgentName: emailCorpus.Signature?.Name,
            AgentEmail: request.Email,
            AgentPhone: emailCorpus.Signature?.Phone ?? discovery.Phone,
            AgentTitle: emailCorpus.Signature?.Title,
            AgentLicenseNumber: emailCorpus.Signature?.LicenseNumber,
            ServiceAreas: serviceAreas,
            Discovery: discovery);

        // PersistProfile is fatal if it fails — let it propagate
        await ctx.CallActivityAsync(ActivityNames.PersistProfile, persistInput);

        // BrandMerge is fatal
        await ctx.CallActivityAsync(
            ActivityNames.BrandMerge,
            new BrandMergeInput(
                AccountId: request.AccountId,
                AgentId: request.AgentId,
                BrandingKit: branding?.BrandingKitMarkdown ?? string.Empty,
                VoiceSkill: voice?.VoiceSkillMarkdown ?? string.Empty));

        // ContactImport is non-fatal (warning on failure, pipeline continues)
        if (contactDetectionResult.Contacts.Count > 0)
        {
            try
            {
                await ctx.CallActivityAsync(
                    ActivityNames.ContactImport,
                    new ContactImportInput(
                        AccountId: request.AccountId,
                        AgentId: request.AgentId,
                        Contacts: contactDetectionResult.Contacts));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (!ctx.IsReplaying)
                {
                    logger.LogWarning(ex,
                        "[ACTV-FN-043] Phase 3 contact import failed for agentId={AgentId} — non-fatal, continuing",
                        request.AgentId);
                }
            }
        }

        // ── Phase 4: Notify ───────────────────────────────────────────────────

        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-050] Phase 4: welcome notification for agentId={AgentId}", request.AgentId);

        await ctx.CallActivityAsync(
            ActivityNames.WelcomeNotification,
            new WelcomeNotificationInput(
                AccountId: request.AccountId,
                AgentId: request.AgentId,
                Handle: request.AgentId,
                AgentName: emailCorpus.Signature?.Name,
                AgentPhone: emailCorpus.Signature?.Phone ?? discovery.Phone,
                WhatsAppEnabled: discovery.WhatsAppEnabled));

        if (!ctx.IsReplaying)
        {
            logger.LogInformation(
                "[ACTV-FN-099] Activation orchestration complete for accountId={AccountId}, agentId={AgentId}",
                request.AccountId, request.AgentId);
        }
    }

    // ── Safe wrapper for Phase 2 parallel workers ─────────────────────────────

    /// <summary>
    /// Calls an activity and returns the typed result.
    /// Returns null on failure so one worker failure does not abort the pipeline.
    /// </summary>
    private static async Task<T?> WrapAsync<T>(
        TaskOrchestrationContext ctx,
        string activityName,
        SynthesisInput input,
        string logPrefix,
        ILogger logger) where T : class
    {
        try
        {
            return await ctx.CallActivityAsync<T>(activityName, input);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!ctx.IsReplaying)
            {
                logger.LogWarning(ex,
                    "{LogPrefix} activity failed for agentId={AgentId} — continuing",
                    logPrefix, input.AgentId);
            }
            return null;
        }
    }

    // ── Deterministic markdown builders (no I/O, replay-safe) ────────────────

    private static string BuildDriveIndexMarkdown(DriveIndexOutput driveIndex)
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

    private static string BuildDiscoveryMarkdown(AgentDiscoveryOutput discovery)
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

    private static string? BuildEmailSignatureMarkdown(EmailSignatureDto? sig)
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
}
