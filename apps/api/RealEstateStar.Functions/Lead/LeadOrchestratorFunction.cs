using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Lead.Models;

namespace RealEstateStar.Functions.Lead;

/// <summary>
/// Durable Functions orchestrator for the lead processing pipeline.
/// Replaces <c>RealEstateStar.Workers.Lead.Orchestrator.LeadOrchestrator</c> (Phase 4 removes that BackgroundService).
/// </summary>
/// <remarks>
/// REPLAY SAFETY: This orchestrator is deterministic. It MUST NOT:
/// - Call DateTime.UtcNow  -> use ctx.CurrentUtcDateTime
/// - Call Guid.NewGuid()   -> use deterministic instance ID from input
/// - Perform any I/O       -> delegate all I/O to activity functions
/// - Log outside !ctx.IsReplaying guards (use [!ctx.IsReplaying] guards for expensive logs)
///
/// Instance ID scheme: lead-{agentId}-{leadId}
/// This makes the orchestration idempotent — re-queuing the same lead re-uses the same instance.
///
/// Parallel execution: CMA and HomeSearch run in parallel via Task.WhenAll.
/// Partial completion: each parallel task is individually try/caught so a CMA failure
/// does not prevent HomeSearch results from being used (and vice versa).
///
/// Timeout: replaced Channel{T}.WaitAsync to ctx.CreateTimer + Task.WhenAny (replay-safe).
///
/// WORKAROUND: Activities return pre-serialized JSON strings instead of typed DTOs because
/// the Durable Functions SDK (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3)
/// stores record.ToString() output instead of JSON in the task history, breaking replay deserialization.
/// The orchestrator deserializes the JSON strings back into typed DTOs.
/// </remarks>
public sealed class LeadOrchestratorFunction
{
    private static readonly TimeSpan DefaultWorkerTimeout = TimeSpan.FromMinutes(5);

    [Function("LeadOrchestrator")]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var input = ctx.GetInput<LeadOrchestratorInput>()
            ?? throw new InvalidOperationException("[ORCH-000] Orchestrator input is null.");

        var logger = ctx.CreateReplaySafeLogger<LeadOrchestratorFunction>();

        if (!ctx.IsReplaying)
        {
            logger.LogInformation("[ORCH-001] Lead orchestration started. LeadId={LeadId}, AgentId={AgentId}, CorrelationId={CorrelationId}",
                input.LeadId, input.AgentId, input.CorrelationId);

            if (string.IsNullOrWhiteSpace(input.Locale) || input.Locale == "en")
            {
                logger.LogInformation(
                    "[ORCH-002] Locale={Locale} for LeadId={LeadId}. Reason: {Reason}. All content will render in English.",
                    input.Locale ?? "null",
                    input.LeadId,
                    input.Locale is null ? "no locale submitted with lead form" : "lead specified English");
            }
            else
            {
                logger.LogInformation(
                    "[ORCH-002] Locale={Locale} for LeadId={LeadId}. Lead email, CMA PDF, and notifications will render in {Language}.",
                    input.Locale, input.LeadId, input.Locale == "es" ? "Spanish" : input.Locale);
            }
        }

        // Step 1: Load agent config
        var configJson = await ctx.CallActivityAsync<string>(
            "LoadAgentConfig",
            new LoadAgentConfigInput
            {
                AgentId = input.AgentId,
                CorrelationId = input.CorrelationId
            });
        var configOutput = JsonSerializer.Deserialize<LoadAgentConfigOutput>(configJson)!;

        if (!configOutput.Found || configOutput.AgentNotificationConfig is null)
        {
            if (!ctx.IsReplaying)
                logger.LogError("[ORCH-010] Agent config not found for {AgentId}. Lead {LeadId} cannot be processed. CorrelationId={CorrelationId}",
                    input.AgentId, input.LeadId, input.CorrelationId);
            return;
        }

        var agentConfig = configOutput.AgentNotificationConfig;

        // Step 2: Score the lead
        var scoreJson = await ctx.CallActivityAsync<string>(
            "ScoreLead",
            new ScoreLeadInput
            {
                AgentId = input.AgentId,
                LeadId = input.LeadId,
                CorrelationId = input.CorrelationId
            });
        var scoreOutput = JsonSerializer.Deserialize<ScoreLeadOutput>(scoreJson)!;

        var score = scoreOutput.Score;

        if (!ctx.IsReplaying)
            logger.LogInformation("[ORCH-020] Lead {LeadId} scored: {Score}/100. CorrelationId={CorrelationId}",
                input.LeadId, score.OverallScore, input.CorrelationId);

        // Step 3: Check content cache (cross-lead dedup — uses IDistributedContentCache)
        var cacheCheckInput = new CheckContentCacheInput
        {
            CmaInputHash = input.CmaInputHash,
            HsInputHash = input.HsInputHash,
            CorrelationId = input.CorrelationId
        };

        var cacheJson = await ctx.CallActivityAsync<string>(
            "CheckContentCache", cacheCheckInput);
        var cacheOutput = JsonSerializer.Deserialize<CheckContentCacheOutput>(cacheJson)!;

        // Step 4: Dispatch CMA + HomeSearch in parallel based on lead type + cache state
        CmaFunctionOutput? cmaOutput = null;
        HomeSearchFunctionOutput? hsOutput = null;

        // Use cached results if available (skips expensive CMA/HS worker calls)
        if (cacheOutput.CmaCacheHit && cacheOutput.CachedCmaResult is not null)
        {
            cmaOutput = new CmaFunctionOutput { Result = cacheOutput.CachedCmaResult };
            if (!ctx.IsReplaying)
                logger.LogInformation("[ORCH-021b] CMA cache hit for lead {LeadId}. CorrelationId={CorrelationId}",
                    input.LeadId, input.CorrelationId);
        }

        if (cacheOutput.HsCacheHit && cacheOutput.CachedHsResult is not null)
        {
            hsOutput = new HomeSearchFunctionOutput { Result = cacheOutput.CachedHsResult };
            if (!ctx.IsReplaying)
                logger.LogInformation("[ORCH-022b] HomeSearch cache hit for lead {LeadId}. CorrelationId={CorrelationId}",
                    input.LeadId, input.CorrelationId);
        }

        // Dispatch any non-cached workers in parallel with timeout
        var deadline = ctx.CurrentUtcDateTime.Add(DefaultWorkerTimeout);
        var timedOut = false;

        var tasksToRun = new List<(string Name, Func<Task> Run)>();

        if (cmaOutput is null && input.ShouldRunCma)
        {
            tasksToRun.Add(("CMA", async () =>
            {
                try
                {
                    var cmaJson = await ctx.CallActivityAsync<string>(
                        "CmaProcessing",
                        new CmaFunctionInput
                        {
                            AgentId = input.AgentId,
                            LeadId = input.LeadId,
                            CorrelationId = input.CorrelationId,
                            AgentNotificationConfig = agentConfig
                        });
                    cmaOutput = JsonSerializer.Deserialize<CmaFunctionOutput>(cmaJson)!;
                }
                catch (Exception ex)
                {
                    if (!ctx.IsReplaying)
                        logger.LogError(ex, "[ORCH-024] CMA processing failed for lead {LeadId}. CorrelationId={CorrelationId}",
                            input.LeadId, input.CorrelationId);
                    // Partial completion: HomeSearch can still succeed
                }
            }));
        }

        if (hsOutput is null && input.ShouldRunHomeSearch)
        {
            tasksToRun.Add(("HomeSearch", async () =>
            {
                try
                {
                    var hsJson = await ctx.CallActivityAsync<string>(
                        "HomeSearch",
                        new HomeSearchFunctionInput
                        {
                            AgentId = input.AgentId,
                            LeadId = input.LeadId,
                            CorrelationId = input.CorrelationId,
                            AgentNotificationConfig = agentConfig
                        });
                    hsOutput = JsonSerializer.Deserialize<HomeSearchFunctionOutput>(hsJson)!;
                }
                catch (Exception ex)
                {
                    if (!ctx.IsReplaying)
                        logger.LogError(ex, "[ORCH-025] HomeSearch failed for lead {LeadId}. CorrelationId={CorrelationId}",
                            input.LeadId, input.CorrelationId);
                    // Partial completion: CMA can still succeed
                }
            }));
        }

        if (tasksToRun.Count > 0)
        {
            // Run all non-cached workers in parallel; apply timeout via ctx.CreateTimer (replay-safe)
            var workerTasks = tasksToRun.Select(t => t.Run()).ToList();
            var allWorkers = Task.WhenAll(workerTasks);
            var timeoutTask = ctx.CreateTimer(deadline, CancellationToken.None);

            var winner = await Task.WhenAny(allWorkers, timeoutTask);
            if (winner == timeoutTask)
            {
                timedOut = true;
                if (!ctx.IsReplaying)
                    logger.LogWarning("[ORCH-030] Worker timeout for lead {LeadId}. CorrelationId={CorrelationId}",
                        input.LeadId, input.CorrelationId);
            }
        }

        if (timedOut && !ctx.IsReplaying)
            logger.LogWarning("[ORCH-031] Proceeding with partial results after timeout. LeadId={LeadId}", input.LeadId);

        // Step 5: Generate PDF (only if CMA succeeded)
        GeneratePdfOutput? pdfOutput = null;
        if (cmaOutput?.Result.Success == true)
        {
            try
            {
                var pdfJson = await ctx.CallActivityAsync<string>(
                    "GeneratePdf",
                    new GeneratePdfInput
                    {
                        AgentId = input.AgentId,
                        LeadId = input.LeadId,
                        CorrelationId = input.CorrelationId,
                        CmaResult = cmaOutput.Result,
                        Locale = input.Locale
                    });
                pdfOutput = JsonSerializer.Deserialize<GeneratePdfOutput>(pdfJson)!;
            }
            catch (Exception ex)
            {
                if (!ctx.IsReplaying)
                    logger.LogError(ex, "[ORCH-035] PDF generation failed for lead {LeadId}. CorrelationId={CorrelationId}",
                        input.LeadId, input.CorrelationId);
                // Non-fatal: pipeline continues without PDF
            }
        }

        // Step 6: Draft lead email
        DraftLeadEmailOutput? emailDraft = null;
        try
        {
            var draftJson = await ctx.CallActivityAsync<string>(
                "DraftLeadEmail",
                new DraftLeadEmailInput
                {
                    AgentId = input.AgentId,
                    LeadId = input.LeadId,
                    CorrelationId = input.CorrelationId,
                    AgentNotificationConfig = agentConfig,
                    Score = score,
                    CmaResult = cmaOutput?.Result,
                    HsResult = hsOutput?.Result,
                    Locale = input.Locale
                });
            emailDraft = JsonSerializer.Deserialize<DraftLeadEmailOutput>(draftJson)!;
        }
        catch (Exception ex)
        {
            if (!ctx.IsReplaying)
                logger.LogError(ex, "[ORCH-040] Email draft failed for lead {LeadId}. CorrelationId={CorrelationId}",
                    input.LeadId, input.CorrelationId);
        }

        // Step 7: Send lead email (idempotency guarded inside LeadCommunicatorService)
        var emailSent = false;
        if (emailDraft is not null)
        {
            try
            {
                await ctx.CallActivityAsync(
                    "SendLeadEmail",
                    new SendLeadEmailInput
                    {
                        AgentId = input.AgentId,
                        LeadId = input.LeadId,
                        CorrelationId = input.CorrelationId,
                        InstanceId = ctx.InstanceId,
                        EmailDraft = emailDraft,
                        AgentNotificationConfig = agentConfig,
                        Score = score,
                        CmaResult = cmaOutput?.Result,
                        HsResult = hsOutput?.Result
                    });
                emailSent = true;
            }
            catch (Exception ex)
            {
                if (!ctx.IsReplaying)
                    logger.LogError(ex, "[ORCH-045] Email send failed for lead {LeadId}. CorrelationId={CorrelationId}",
                        input.LeadId, input.CorrelationId);
            }
        }

        // Step 8: Notify agent (idempotency guarded inside AgentNotifierService)
        var agentNotified = false;
        try
        {
            await ctx.CallActivityAsync(
                "NotifyAgent",
                new NotifyAgentInput
                {
                    AgentId = input.AgentId,
                    LeadId = input.LeadId,
                    CorrelationId = input.CorrelationId,
                    InstanceId = ctx.InstanceId,
                    AgentNotificationConfig = agentConfig,
                    Score = score,
                    CmaResult = cmaOutput?.Result,
                    HsResult = hsOutput?.Result,
                    Locale = input.Locale
                });
            agentNotified = true;
        }
        catch (Exception ex)
        {
            if (!ctx.IsReplaying)
                logger.LogError(ex, "[ORCH-050] Agent notification failed for lead {LeadId}. CorrelationId={CorrelationId}",
                    input.LeadId, input.CorrelationId);
        }

        // Step 9: Persist all results
        try
        {
            await ctx.CallActivityAsync(
                "PersistLeadResults",
                new PersistLeadResultsInput
                {
                    AgentId = input.AgentId,
                    LeadId = input.LeadId,
                    CorrelationId = input.CorrelationId,
                    Score = score,
                    CmaResult = cmaOutput?.Result,
                    HsResult = hsOutput?.Result,
                    PdfStoragePath = pdfOutput?.PdfStoragePath,
                    EmailDraft = emailDraft,
                    EmailSent = emailSent,
                    AgentNotified = agentNotified,
                    CmaInputHash = input.CmaInputHash,
                    HsInputHash = input.HsInputHash,
                    Locale = input.Locale
                });
        }
        catch (Exception ex)
        {
            if (!ctx.IsReplaying)
                logger.LogError(ex, "[ORCH-085] PersistLeadResults failed for lead {LeadId}. CorrelationId={CorrelationId}",
                    input.LeadId, input.CorrelationId);
        }

        // Step 10: Update content cache with fresh results (for future cross-lead dedup)
        try
        {
            await ctx.CallActivityAsync(
                "UpdateContentCache",
                new UpdateContentCacheInput
                {
                    CmaInputHash = input.CmaInputHash,
                    HsInputHash = input.HsInputHash,
                    CmaResult = cmaOutput?.Result,
                    HsResult = hsOutput?.Result,
                    CorrelationId = input.CorrelationId
                });
        }
        catch (Exception ex)
        {
            if (!ctx.IsReplaying)
                logger.LogWarning(ex, "[ORCH-086] UpdateContentCache failed for lead {LeadId}. CorrelationId={CorrelationId}",
                    input.LeadId, input.CorrelationId);
        }

        if (!ctx.IsReplaying)
            logger.LogInformation("[ORCH-090] Lead {LeadId} pipeline complete. CorrelationId={CorrelationId}",
                input.LeadId, input.CorrelationId);
    }
}
