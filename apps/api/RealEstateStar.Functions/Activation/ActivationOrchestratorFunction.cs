using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Diagnostics;
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
///
/// WORKAROUND: Activities return pre-serialized JSON strings instead of typed DTOs because
/// the Durable Functions SDK (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3)
/// stores record.ToString() output instead of JSON in the task history, breaking replay deserialization.
/// The orchestrator deserializes the JSON strings back into typed DTOs.
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

        // ── Phase 1: Gather ──────────────────────────────────────────────────
        // Runs before the skip-if-complete check so detected languages are available.

        var phase1Start = ctx.CurrentUtcDateTime;
        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-010] Phase 1: gather for accountId={AccountId}", request.AccountId);

        // Email then Drive — sequential to stay under Consumption plan 1.5 GB memory limit.
        // Running both in parallel doubles peak memory (both hold Google API responses).
        var emailStart = ctx.CurrentUtcDateTime;
        var emailJson = await ctx.CallActivityAsync<string>(
            ActivityNames.EmailFetch,
            new EmailFetchInput { AccountId = request.AccountId, AgentId = request.AgentId });
        var emailCorpus = JsonSerializer.Deserialize<EmailFetchOutput>(emailJson)!;
        if (!ctx.IsReplaying)
        {
            var emailDuration = (ctx.CurrentUtcDateTime - emailStart).TotalMilliseconds;
            logger.LogInformation("[ACTV-FN-011] EmailFetch completed in {Duration}ms", emailDuration);
        }

        var driveStart = ctx.CurrentUtcDateTime;
        var driveJson = await ctx.CallActivityAsync<string>(
            ActivityNames.DriveIndex,
            new DriveIndexInput { AccountId = request.AccountId, AgentId = request.AgentId });
        var driveIndex = JsonSerializer.Deserialize<DriveIndexOutput>(driveJson)!;
        if (!ctx.IsReplaying)
        {
            var driveDuration = (ctx.CurrentUtcDateTime - driveStart).TotalMilliseconds;
            logger.LogInformation("[ACTV-FN-012] DriveIndex completed in {Duration}ms", driveDuration);
        }

        // Derive distinct detected locales from Phase 1 outputs for language-aware completion check.
        var detectedLanguages = emailCorpus.SentEmails
            .Concat(emailCorpus.InboxEmails)
            .Select(e => e.DetectedLocale)
            .Concat(driveIndex.Files.Select(f => f.DetectedLocale))
            .Where(l => l is not null && l != "en")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(l => l!)
            .ToList();

        if (!ctx.IsReplaying && detectedLanguages.Count > 0)
        {
            logger.LogInformation(
                "[ACTV-FN-015] Detected non-English languages for agentId={AgentId}: [{Languages}]. " +
                "Bilingual extraction will run for {Count} additional language(s).",
                request.AgentId, string.Join(", ", detectedLanguages), detectedLanguages.Count);
            LanguageDiagnostics.BilingualActivations.Add(1,
                new KeyValuePair<string, object?>("agentId", request.AgentId),
                new KeyValuePair<string, object?>("languages", string.Join(",", detectedLanguages)));
        }

        // ── Phase 0: skip-if-complete ────────────────────────────────────────
        // Runs after Phase 1 so language-aware file checks (e.g., Voice Skill.es.md) are included.

        var completeCheckJson = await ctx.CallActivityAsync<string>(
            ActivityNames.CheckActivationComplete,
            new CheckActivationCompleteInput
            {
                AccountId = request.AccountId,
                AgentId = request.AgentId,
                Languages = detectedLanguages.Count > 0 ? detectedLanguages : null,
            });
        var completeCheck = JsonSerializer.Deserialize<CheckActivationCompleteOutput>(completeCheckJson)!;

        if (completeCheck.IsComplete)
        {
            if (!ctx.IsReplaying)
            {
                logger.LogInformation(
                    "[ACTV-FN-003] SKIP: Activation already complete for accountId={AccountId}, agentId={AgentId}. " +
                    "Reason: all required files exist (including language-specific files for [{Languages}]). Sending welcome (idempotent).",
                    request.AccountId, request.AgentId,
                    detectedLanguages.Count > 0 ? string.Join(",", detectedLanguages) : "en-only");
            }

            await ctx.CallActivityAsync(
                ActivityNames.WelcomeNotification,
                new WelcomeNotificationInput
                {
                    AccountId = request.AccountId,
                    AgentId = request.AgentId,
                    Handle = request.AgentId,
                });

            return;
        }

        // Discovery requires email corpus to use signature info.
        // Validate email before splitting to avoid IndexOutOfRangeException on malformed input.
        var emailHandle = !string.IsNullOrWhiteSpace(request.Email) && request.Email.Contains('@')
            ? request.Email.Split('@')[0].Trim()
            : null;

        // Agent name: prefer email signature (real name), fall back to email handle, then account ID.
        var agentName = emailCorpus.Signature?.Name
            ?? emailHandle
            ?? request.AccountId;

        // Combine profile URLs discovered from emails and Drive documents.
        // These are the agent's REAL profile URLs (e.g., zillow.com/profile/jenisebuck)
        // — more reliable than guessing from their name.
        var discoveredUrls = (emailCorpus.DiscoveredProfileUrls ?? [])
            .Concat(driveIndex.DiscoveredUrls ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var discoveryStart = ctx.CurrentUtcDateTime;
        var discoveryJson = await ctx.CallActivityAsync<string>(
            ActivityNames.AgentDiscovery,
            new AgentDiscoveryInput
            {
                AccountId = request.AccountId,
                AgentId = request.AgentId,
                AgentName = agentName,
                BrokerageName = emailCorpus.Signature?.BrokerageName ?? string.Empty,
                PhoneNumber = emailCorpus.Signature?.Phone,
                EmailHandle = emailHandle,
                AgentEmail = request.Email,
                DiscoveredUrls = discoveredUrls,
                EmailSignature = emailCorpus.Signature,
            });
        var discovery = JsonSerializer.Deserialize<AgentDiscoveryOutput>(discoveryJson)!;
        if (!ctx.IsReplaying)
        {
            var discoveryDuration = (ctx.CurrentUtcDateTime - discoveryStart).TotalMilliseconds;
            logger.LogInformation("[ACTV-FN-013] AgentDiscovery completed in {Duration}ms", discoveryDuration);
            var phase1Duration = (ctx.CurrentUtcDateTime - phase1Start).TotalMilliseconds;
            logger.LogInformation("[ACTV-FN-014] Phase 1 completed in {Duration}ms", phase1Duration);
        }

        // ── Phase 2: Synthesize (12 workers in parallel) ──────────────────────

        var phase2Start = ctx.CurrentUtcDateTime;
        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-020] Phase 2: synthesize for agentId={AgentId}", request.AgentId);

        var synthesisInput = new SynthesisInput
        {
            AccountId = request.AccountId,
            AgentId = request.AgentId,
            AgentName = agentName,
            EmailCorpus = emailCorpus,
            DriveIndex = driveIndex,
            Discovery = discovery,
        };

        // Each worker wrapped in try/catch to preserve RunSafeAsync semantics:
        // one worker failure does NOT abort the pipeline — it contributes null output.
        //
        // Batched in pairs of 2 to avoid Anthropic API rate limits.
        // Even 4 at a time triggers rate_limit_error responses.
        //
        // MVP tier dispatches 8 workers in 4 batches.
        // Future tier dispatches all 12 workers in 6 batches.

        // ── Batch 1 (MVP + Future) ──────────────────────────────────────────
        var batch1Start = ctx.CurrentUtcDateTime;
        var voiceTask = WrapAsync<VoiceExtractionOutput>(
            ctx, ActivityNames.VoiceExtraction, synthesisInput, "[ACTV-FN-021] voice", logger);
        var personalityTask = WrapAsync<PersonalityOutput>(
            ctx, ActivityNames.Personality, synthesisInput, "[ACTV-FN-022] personality", logger);
        await Task.WhenAll(voiceTask, personalityTask);
        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-025] Batch 1 (voice+personality) completed in {Duration}ms",
                (ctx.CurrentUtcDateTime - batch1Start).TotalMilliseconds);

        // ── Batch 2 (MVP + Future) ──────────────────────────────────────────
        var batch2Start = ctx.CurrentUtcDateTime;
        var brandingTask = WrapAsync<BrandingDiscoveryOutput>(
            ctx, ActivityNames.BrandingDiscovery, synthesisInput, "[ACTV-FN-023] branding", logger);
        var websiteTask = WrapAsync<StringOutput>(
            ctx, ActivityNames.WebsiteStyle, synthesisInput, "[ACTV-FN-026] website-style", logger);
        await Task.WhenAll(brandingTask, websiteTask);
        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-025] Batch 2 (branding+website) completed in {Duration}ms",
                (ctx.CurrentUtcDateTime - batch2Start).TotalMilliseconds);

        // ── Batch 3 (MVP + Future) ──────────────────────────────────────────
        var batch3Start = ctx.CurrentUtcDateTime;
        var cmaTask = WrapAsync<StringOutput>(
            ctx, ActivityNames.CmaStyle, synthesisInput, "[ACTV-FN-024] cma-style", logger);
        var pipelineTask = WrapAsync<PipelineAnalysisOutput>(
            ctx, ActivityNames.PipelineAnalysis, synthesisInput, "[ACTV-FN-027] pipeline", logger);
        await Task.WhenAll(cmaTask, pipelineTask);
        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-025] Batch 3 (cma+pipeline) completed in {Duration}ms",
                (ctx.CurrentUtcDateTime - batch3Start).TotalMilliseconds);

        // ── Batch 4 (MVP + Future) ──────────────────────────────────────────
        var batch4Start = ctx.CurrentUtcDateTime;
        var coachingTask = WrapAsync<CoachingOutput>(
            ctx, ActivityNames.Coaching, synthesisInput, "[ACTV-FN-028] coaching", logger);
        var complianceTask = WrapAsync<StringOutput>(
            ctx, ActivityNames.ComplianceAnalysis, synthesisInput, "[ACTV-FN-031] compliance", logger);
        await Task.WhenAll(coachingTask, complianceTask);
        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-025] Batch 4 (coaching+compliance) completed in {Duration}ms",
                (ctx.CurrentUtcDateTime - batch4Start).TotalMilliseconds);

        // ── FUTURE-tier workers (skip for MVP) ──────────────────────────────
        Task<BrandExtractionOutput?> brandExtractionTask;
        Task<BrandVoiceOutput?> brandVoiceTask;
        Task<MarketingStyleOutput?> marketingTask;
        Task<StringOutput?> feeTask;

        if (request.Tier == ActivationTier.Future)
        {
            // ── Batch 5 (Future only) ───────────────────────────────────────
            var batch5Start = ctx.CurrentUtcDateTime;
            brandExtractionTask = WrapAsync<BrandExtractionOutput>(
                ctx, ActivityNames.BrandExtraction, synthesisInput, "[ACTV-FN-029] brand-extraction", logger);
            brandVoiceTask = WrapAsync<BrandVoiceOutput>(
                ctx, ActivityNames.BrandVoice, synthesisInput, "[ACTV-FN-030] brand-voice", logger);
            await Task.WhenAll(brandExtractionTask, brandVoiceTask);
            if (!ctx.IsReplaying)
                logger.LogInformation("[ACTV-FN-025] Batch 5 (brand-extraction+brand-voice) completed in {Duration}ms",
                    (ctx.CurrentUtcDateTime - batch5Start).TotalMilliseconds);

            // ── Batch 6 (Future only) ───────────────────────────────────────
            var batch6Start = ctx.CurrentUtcDateTime;
            marketingTask = WrapAsync<MarketingStyleOutput>(
                ctx, ActivityNames.MarketingStyle, synthesisInput, "[ACTV-FN-025] marketing", logger);
            feeTask = WrapAsync<StringOutput>(
                ctx, ActivityNames.FeeStructure, synthesisInput, "[ACTV-FN-032] fee-structure", logger);
            await Task.WhenAll(marketingTask, feeTask);
            if (!ctx.IsReplaying)
                logger.LogInformation("[ACTV-FN-025] Batch 6 (marketing+fee) completed in {Duration}ms",
                    (ctx.CurrentUtcDateTime - batch6Start).TotalMilliseconds);
        }
        else
        {
            if (!ctx.IsReplaying)
            {
                logger.LogInformation(
                    "[ACTV-FN-033] SKIP: Future-tier workers (BrandExtraction, BrandVoice, MarketingStyle, FeeStructure) " +
                    "for agentId={AgentId}. Reason: Tier={Tier}. These workers run only on Future-tier activations. " +
                    "Bilingual extraction for these skills is deferred until tier upgrade.",
                    request.AgentId, request.Tier);
            }

            brandExtractionTask = Task.FromResult<BrandExtractionOutput?>(null);
            brandVoiceTask = Task.FromResult<BrandVoiceOutput?>(null);
            marketingTask = Task.FromResult<MarketingStyleOutput?>(null);
            feeTask = Task.FromResult<StringOutput?>(null);
        }

        var voice = voiceTask.Result;
        var personality = personalityTask.Result;
        var branding = brandingTask.Result;
        var cmaStyle = cmaTask.Result?.Value;
        var marketing = marketingTask.Result;
        var websiteStyle = websiteTask.Result?.Value;
        var pipelineResult = pipelineTask.Result;
        var salesPipeline = pipelineResult?.Markdown;
        var pipelineJson = pipelineResult?.PipelineJson;
        var coaching = coachingTask.Result;
        var brandExtractionResult = brandExtractionTask.Result;
        var brandExtraction = brandExtractionResult?.Signals;
        var brandVoiceResult = brandVoiceTask.Result;
        var brandVoice = brandVoiceResult?.Signals;
        var localizedSkills = MergeLocalizedSkills(
            voice?.LocalizedSkills,
            personality?.LocalizedSkills,
            brandExtractionResult?.LocalizedSkills,
            brandVoiceResult?.LocalizedSkills,
            marketingTask.Result?.LocalizedSkills);
        var compliance = complianceTask.Result?.Value;
        var feeStructure = feeTask.Result?.Value;

        // Count succeeded/failed synthesis workers for the summary
        object?[] mvpResults = [voice, personality, branding, cmaTask.Result, websiteTask.Result, pipelineResult, coaching, complianceTask.Result];
        var succeededCount = mvpResults.Count(r => r is not null);
        var failedCount = mvpResults.Length - succeededCount;
        if (request.Tier == ActivationTier.Future)
        {
            object?[] futureResults = [brandExtractionTask.Result, brandVoiceTask.Result, marketing, feeTask.Result];
            succeededCount += futureResults.Count(r => r is not null);
            failedCount += futureResults.Count(r => r is null);
        }

        if (!ctx.IsReplaying)
        {
            var phase2Duration = (ctx.CurrentUtcDateTime - phase2Start).TotalMilliseconds;
            logger.LogInformation(
                "[ACTV-FN-034] Phase 2 completed in {Duration}ms — {Succeeded} succeeded, {Failed} failed",
                phase2Duration, succeededCount, failedCount);
        }

        // ── Phase 2.5: Contact Detection ──────────────────────────────────────

        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-035] Phase 2.5: contact detection for agentId={AgentId}", request.AgentId);

        ContactDetectionOutput contactDetectionResult;
        try
        {
            var contactJson = await ctx.CallActivityAsync<string>(
                ActivityNames.ContactDetection,
                new ContactDetectionInput
                {
                    AccountId = request.AccountId,
                    AgentId = request.AgentId,
                    DriveExtractions = driveIndex.Extractions,
                    EmailCorpus = emailCorpus,
                });
            contactDetectionResult = JsonSerializer.Deserialize<ContactDetectionOutput>(contactJson)!;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!ctx.IsReplaying)
            {
                logger.LogWarning(ex,
                    "[ACTV-FN-036] Phase 2.5 contact detection failed for agentId={AgentId} — continuing without contacts",
                    request.AgentId);
            }
            contactDetectionResult = new ContactDetectionOutput();
        }

        // ── Phase 3: Persist + Merge ──────────────────────────────────────────

        var phase3Start = ctx.CurrentUtcDateTime;
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

        var persistInput = new PersistProfileInput
        {
            AccountId = request.AccountId,
            AgentId = request.AgentId,
            Handle = request.AgentId,
            Voice = voice,
            Personality = personality,
            CmaStyle = cmaStyle,
            WebsiteStyle = websiteStyle,
            SalesPipeline = salesPipeline,
            Coaching = coaching,
            Branding = branding,
            Compliance = compliance,
            PipelineJson = pipelineJson,
            DriveIndexMarkdown = driveIndexMarkdown,
            DiscoveryMarkdown = discoveryMarkdown,
            EmailSignatureMarkdown = emailSigMarkdown,
            HeadshotBytes = discovery.HeadshotBytes,
            BrokerageLogoBytes = discovery.LogoBytes,
            AgentName = emailCorpus.Signature?.Name,
            AgentEmail = request.Email,
            AgentPhone = emailCorpus.Signature?.Phone ?? discovery.Phone,
            AgentTitle = emailCorpus.Signature?.Title,
            AgentLicenseNumber = emailCorpus.Signature?.LicenseNumber,
            ServiceAreas = serviceAreas,
            Discovery = discovery,
            LocalizedSkills = localizedSkills?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };

        // PersistProfile is fatal if it fails — let it propagate
        await ctx.CallActivityAsync(ActivityNames.PersistProfile, persistInput);

        // BrandMerge only for multi-agent accounts (brokerage)
        if (request.AccountId != request.AgentId)
        {
            await ctx.CallActivityAsync(
                ActivityNames.BrandMerge,
                new BrandMergeInput
                {
                    AccountId = request.AccountId,
                    AgentId = request.AgentId,
                    BrandingKit = branding?.BrandingKitMarkdown ?? string.Empty,
                    VoiceSkill = voice?.VoiceSkillMarkdown ?? string.Empty,
                });
        }
        else if (!ctx.IsReplaying)
        {
            logger.LogInformation(
                "[ACTV-FN-042] SKIP: BrandMerge for accountId={AccountId}. " +
                "Reason: single-agent account (accountId == agentId). Brand merge only applies to multi-agent brokerages.",
                request.AccountId);
        }

        // ContactImport is non-fatal (warning on failure, pipeline continues)
        if (contactDetectionResult.Contacts.Count > 0)
        {
            try
            {
                await ctx.CallActivityAsync(
                    ActivityNames.ContactImport,
                    new ContactImportInput
                    {
                        AccountId = request.AccountId,
                        AgentId = request.AgentId,
                        Contacts = contactDetectionResult.Contacts,
                    });
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

        // Cleanup staged Drive content blobs (non-fatal — best effort)
        try
        {
            await ctx.CallActivityAsync(
                ActivityNames.CleanupStagedContent,
                new CleanupStagedContentInput
                {
                    AccountId = request.AccountId,
                    AgentId = request.AgentId,
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!ctx.IsReplaying)
                logger.LogWarning(ex, "[ACTV-FN-046] Staged content cleanup failed — non-fatal");
        }

        if (!ctx.IsReplaying)
        {
            var phase3Duration = (ctx.CurrentUtcDateTime - phase3Start).TotalMilliseconds;
            logger.LogInformation("[ACTV-FN-049] Phase 3 completed in {Duration}ms", phase3Duration);
        }

        // ── Phase 4: Notify ───────────────────────────────────────────────────

        var phase4Start = ctx.CurrentUtcDateTime;
        if (!ctx.IsReplaying)
            logger.LogInformation("[ACTV-FN-050] Phase 4: welcome notification for agentId={AgentId}", request.AgentId);

        await ctx.CallActivityAsync(
            ActivityNames.WelcomeNotification,
            new WelcomeNotificationInput
            {
                AccountId = request.AccountId,
                AgentId = request.AgentId,
                Handle = request.AgentId,
                AgentName = emailCorpus.Signature?.Name,
                AgentPhone = emailCorpus.Signature?.Phone ?? discovery.Phone,
                WhatsAppEnabled = discovery.WhatsAppEnabled,
                AgentEmail = request.Email,
                // Synthesis data for personalized welcome email
                VoiceSkill = voice?.VoiceSkillMarkdown,
                PersonalitySkill = personality?.PersonalitySkillMarkdown,
                CoachingReport = coaching?.CoachingReportMarkdown,
                PipelineJson = pipelineJson,
                ContactCount = contactDetectionResult.Contacts.Count,
                LocalizedSkills = localizedSkills?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            });

        if (!ctx.IsReplaying)
        {
            var phase4Duration = (ctx.CurrentUtcDateTime - phase4Start).TotalMilliseconds;
            logger.LogInformation("[ACTV-FN-059] Phase 4 completed in {Duration}ms", phase4Duration);

            var totalDuration = (ctx.CurrentUtcDateTime - request.Timestamp).TotalMilliseconds;
            var localizedSkillCount = localizedSkills?.Count ?? 0;
            logger.LogInformation(
                "[ACTV-FN-099] Activation orchestration complete for accountId={AccountId}, agentId={AgentId}, " +
                "Tier={Tier}, TotalDuration={TotalDuration}ms, Succeeded={Succeeded}, Failed={Failed}, " +
                "DetectedLanguages=[{Languages}], LocalizedSkills={LocalizedSkillCount}",
                request.AccountId, request.AgentId, request.Tier, totalDuration, succeededCount, failedCount,
                detectedLanguages.Count > 0 ? string.Join(",", detectedLanguages) : "en-only",
                localizedSkillCount);

            if (localizedSkillCount > 0)
            {
                LanguageDiagnostics.SkillsExtracted.Add(localizedSkillCount,
                    new KeyValuePair<string, object?>("agentId", request.AgentId));
            }
        }
    }

    // ── Safe wrapper for Phase 2 parallel workers ─────────────────────────────

    /// <summary>
    /// Calls an activity (which returns pre-serialized JSON string), deserializes
    /// the result to the typed DTO, and returns it.
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
            var json = await ctx.CallActivityAsync<string>(activityName, input);
            return json is null ? null : JsonSerializer.Deserialize<T>(json);
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

    // ── Deterministic merge of localized skills from all Phase 2 outputs ──────

    /// <summary>
    /// Merges LocalizedSkills dictionaries from all Phase 2 outputs into a single dictionary.
    /// Later entries overwrite earlier ones if keys conflict (last-writer-wins).
    /// Returns null if no localized skills were produced.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? MergeLocalizedSkills(
        params IReadOnlyDictionary<string, string>?[] sources)
    {
        var merged = new Dictionary<string, string>();
        foreach (var source in sources)
        {
            if (source is null) continue;
            foreach (var kvp in source)
                merged[kvp.Key] = kvp.Value;
        }
        return merged.Count > 0 ? merged : null;
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

        if (driveIndex.Extractions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"## PDF Extractions ({driveIndex.Extractions.Count})");
            foreach (var ext in driveIndex.Extractions)
            {
                sb.AppendLine($"### {ext.FileName}");
                sb.AppendLine($"- **Type**: {ext.Type}");
                if (ext.Date is not null)
                    sb.AppendLine($"- **Date**: {ext.Date:yyyy-MM-dd}");
                if (ext.Property is not null)
                    sb.AppendLine($"- **Property**: {ext.Property.Address}" +
                        (ext.Property.City is not null ? $", {ext.Property.City}" : "") +
                        (ext.Property.State is not null ? $", {ext.Property.State}" : "") +
                        (ext.Property.Zip is not null ? $" {ext.Property.Zip}" : ""));
                if (ext.KeyTerms?.Price is not null)
                    sb.AppendLine($"- **Price**: {ext.KeyTerms.Price}");
                if (ext.KeyTerms?.Commission is not null)
                    sb.AppendLine($"- **Commission**: {ext.KeyTerms.Commission}");
                if (ext.Clients.Count > 0)
                {
                    sb.AppendLine("- **Clients**:");
                    foreach (var client in ext.Clients)
                        sb.AppendLine($"  - {client.Name} ({client.Role})" +
                            (client.Phone is not null ? $" — {client.Phone}" : "") +
                            (client.Email is not null ? $" — {client.Email}" : ""));
                }
            }
        }

        if (driveIndex.DiscoveredUrls.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"## Discovered URLs ({driveIndex.DiscoveredUrls.Count})");
            foreach (var url in driveIndex.DiscoveredUrls)
                sb.AppendLine($"- {url}");
        }

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
