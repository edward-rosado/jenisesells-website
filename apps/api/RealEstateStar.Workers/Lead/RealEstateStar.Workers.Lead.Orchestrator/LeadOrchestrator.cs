using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.Activities.Lead.Persist;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Activities.Pdf;

namespace RealEstateStar.Workers.Lead.Orchestrator;

/// <summary>
/// Per-lead orchestrator. Reads from <see cref="LeadOrchestratorChannel"/>,
/// scores the lead, dispatches CMA and HomeSearch workers in parallel via channels,
/// calls <see cref="PdfActivity"/> inline, drafts and sends the lead email via
/// <see cref="ILeadCommunicatorService"/>, and notifies the agent via
/// <see cref="IAgentNotifier"/>. All pipeline state is carried in
/// a <see cref="LeadPipelineContext"/> instance.
/// </summary>
/// <remarks>
/// Retry safety: before dispatching CMA/HomeSearch, checks the per-lead
/// <see cref="LeadRetryState"/> to skip activities whose inputs haven't changed.
/// Cross-lead dedup: checks <see cref="IContentCache"/> before dispatching CMA/HomeSearch
/// to avoid re-running expensive analysis for the same property across multiple leads.
/// </remarks>
public sealed class LeadOrchestrator(
    LeadOrchestratorChannel channel,
    IAccountConfigService accountConfigService,
    ILeadScorer scorer,
    CmaProcessingChannel cmaChannel,
    HomeSearchProcessingChannel homeSearchChannel,
    PdfActivity pdfActivity,
    PersistActivity persistActivity,
    ILeadCommunicatorService communicationService,
    IAgentNotifier agentNotifier,
    IContentCache contentCache,
    BackgroundServiceHealthTracker healthTracker,
    ILogger<LeadOrchestrator> logger,
    IConfiguration configuration,
    IAgentContextLoader? agentContextLoader = null)
    : BackgroundService
{
    private const string WorkerName = "LeadOrchestrator";

    // Content cache TTLs
    internal static readonly TimeSpan CmaCacheTtl = TimeSpan.FromHours(24);
    internal static readonly TimeSpan HomeSearchCacheTtl = TimeSpan.FromHours(1);

    private int WorkerTimeoutSeconds =>
        configuration.GetValue<int?>("Pipeline:Lead:WorkerTimeoutSeconds") ?? 300;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[{Worker}-001] {Worker} started.", WorkerName, WorkerName);

        await foreach (var request in channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessRequestAsync(request, stoppingToken);
        }

        logger.LogInformation("[{Worker}-003] {Worker} stopping.", WorkerName, WorkerName);
    }

    internal async Task ProcessRequestAsync(LeadOrchestrationRequest request, CancellationToken ct)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var lead = request.Lead;
        var agentId = request.AgentId;
        var correlationId = request.CorrelationId;

        OrchestratorDiagnostics.LeadsProcessed.Add(1);

        using var activity = OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.process_lead");
        activity?.SetTag("lead.id", lead.Id.ToString());
        activity?.SetTag("agent.id", agentId);
        activity?.SetTag("correlation.id", correlationId);

        // Keep existing lead-level trace span for backward compatibility
        using var leadActivity = LeadDiagnostics.ActivitySource.StartActivity("lead.orchestrate");
        leadActivity?.SetTag("lead.id", lead.Id.ToString());
        leadActivity?.SetTag("agent.id", agentId);
        leadActivity?.SetTag("correlation.id", correlationId);

        try
        {
            // Step 1: Load agent config (single read)
            var accountConfig = await accountConfigService.GetAccountAsync(agentId, ct);
            if (accountConfig is null)
            {
                logger.LogError(
                    "[{Worker}-010] Agent config not found for {AgentId}. Lead {LeadId} cannot be processed. CorrelationId: {CorrelationId}",
                    WorkerName, agentId, lead.Id, correlationId);
                OrchestratorDiagnostics.LeadsFailed.Add(1);
                return;
            }

            var agentConfig = BuildAgentNotificationConfig(agentId, accountConfig);

            // Step 1b: Load agent activation context (skills, brand voice, coaching)
            Domain.Activation.Models.AgentContext? agentContext = null;
            if (agentContextLoader is not null)
            {
                try
                {
                    agentContext = await agentContextLoader.LoadAsync(accountConfig.Handle ?? agentId, agentId, ct);
                    if (agentContext is null)
                    {
                        logger.LogInformation(
                            "[CTX-003] Agent context not available for {AgentId}. Using generic prompts. CorrelationId: {CorrelationId}",
                            agentId, correlationId);
                    }
                    else if (agentContext.IsLowConfidence)
                    {
                        logger.LogInformation(
                            "[CTX-002] Agent context loaded (partial/low-confidence) for {AgentId}. CorrelationId: {CorrelationId}",
                            agentId, correlationId);
                    }
                    else
                    {
                        logger.LogInformation(
                            "[CTX-001] Agent context fully loaded for {AgentId}. CorrelationId: {CorrelationId}",
                            agentId, correlationId);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "[CTX-004] Agent context load failed for {AgentId}; continuing with generic prompts. CorrelationId: {CorrelationId}",
                        agentId, correlationId);
                }
            }

            // Build the shared pipeline context — load existing RetryState from lead if available
            var ctx = new LeadPipelineContext
            {
                Lead = lead,
                AgentConfig = agentConfig,
                CorrelationId = correlationId,
                RetryState = lead.RetryState ?? new LeadRetryState(),
                AgentContext = agentContext
            };

            // Step 2: Score the lead
            var scoreStarted = Stopwatch.GetTimestamp();
            using var scoreSpan = OrchestratorDiagnostics.ActivitySource.StartActivity("activity.score");
            ctx.Score = scorer.Score(lead);
            lead.Score = ctx.Score;
            OrchestratorDiagnostics.ScoreDurationMs.Record(
                Stopwatch.GetElapsedTime(scoreStarted).TotalMilliseconds);
            await persistActivity.PersistStatusAsync(lead, LeadStatus.Scored, ct);

            logger.LogInformation(
                "[{Worker}-020] Lead {LeadId} scored: {Score}/100 ({Bucket}). CorrelationId: {CorrelationId}",
                WorkerName, lead.Id, ctx.Score.OverallScore, ctx.Score.Bucket, correlationId);

            // Step 3: Dispatch CMA + HomeSearch in parallel via channels, collect via TCS
            // Content-aware skip: checks RetryState (per-lead) + IContentCache (cross-lead)
            await persistActivity.PersistStatusAsync(lead, LeadStatus.Analyzing, ct);

            var cmaInputHash = ComputeCmaInputHash(lead);
            var hsInputHash = ComputeHsInputHash(lead);

            var (cmaTcs, hsTcs) = await DispatchWorkersAsync(
                lead, agentId, agentConfig, correlationId,
                ctx.RetryState, cmaInputHash, hsInputHash, ct, ctx.AgentContext);

            // Step 4: Wait for workers with configurable timeout
            var pendingTasks = BuildPendingTasks(cmaTcs, hsTcs);
            var collectStarted = Stopwatch.GetTimestamp();
            var timedOut = false;

            using (OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.collect_workers"))
            {
                try
                {
                    await Task.WhenAll(pendingTasks).WaitAsync(
                        TimeSpan.FromSeconds(WorkerTimeoutSeconds), ct);
                }
                catch (TimeoutException)
                {
                    timedOut = true;
                    logger.LogWarning(
                        "[{Worker}-030] Worker timeout after {TimeoutSeconds}s. Lead {LeadId}. CorrelationId: {CorrelationId}",
                        WorkerName, WorkerTimeoutSeconds, lead.Id, correlationId);
                }
            }

            OrchestratorDiagnostics.CollectDurationMs.Record(
                Stopwatch.GetElapsedTime(collectStarted).TotalMilliseconds);

            if (timedOut)
                OrchestratorDiagnostics.WorkerTimeouts.Add(1);

            // Collect results from completed TCS tasks into context
            if (cmaTcs is not null && cmaTcs.Task.IsCompleted)
            {
                ctx.CmaResult = cmaTcs.Task.Result;
                OrchestratorDiagnostics.WorkerCompletions.Add(1);

                // Update cross-lead content cache and per-lead retry state
                if (ctx.CmaResult.Success)
                {
                    await contentCache.SetAsync(cmaInputHash, ctx.CmaResult, CmaCacheTtl, ct);
                    ctx.RetryState.CompletedActivityKeys["cma"] = cmaInputHash;
                    ctx.RetryState.CompletedResultPaths["cma"] = $"cma:{lead.Id}:{cmaInputHash}";
                }
            }
            else if (cmaTcs is not null)
            {
                OrchestratorDiagnostics.WorkerTimeouts.Add(1);
            }

            if (hsTcs is not null && hsTcs.Task.IsCompleted)
            {
                ctx.HsResult = hsTcs.Task.Result;
                OrchestratorDiagnostics.WorkerCompletions.Add(1);

                // Update cross-lead content cache and per-lead retry state
                if (ctx.HsResult.Success)
                {
                    await contentCache.SetAsync(hsInputHash, ctx.HsResult, HomeSearchCacheTtl, ct);
                    ctx.RetryState.CompletedActivityKeys["homeSearch"] = hsInputHash;
                    ctx.RetryState.CompletedResultPaths["homeSearch"] = $"hs:{lead.Id}:{hsInputHash}";
                }
            }
            else if (hsTcs is not null)
            {
                OrchestratorDiagnostics.WorkerTimeouts.Add(1);
            }

            LogWorkerResults(ctx.CmaResult, ctx.HsResult, lead.Id, correlationId);

            // Step 5: Generate PDF inline via PdfActivity (CMA succeeded required)
            var pdfInputHash = ComputePdfInputHash(ctx.CmaResult);
            if (ctx.CmaResult?.Success == true)
            {
                if (ctx.RetryState.IsCompleted("pdf", pdfInputHash))
                {
                    logger.LogInformation(
                        "[{Worker}-035a] PDF skipped — same CMA content already generated. Lead {LeadId}. CorrelationId: {CorrelationId}",
                        WorkerName, lead.Id, correlationId);
                }
                else
                {
                    var pdfStarted = Stopwatch.GetTimestamp();
                    using (OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.generate_pdf"))
                    {
                        ctx.PdfStoragePath = await GeneratePdfAsync(ctx, accountConfig, correlationId, ct);
                    }
                    OrchestratorDiagnostics.PdfDurationMs.Record(
                        Stopwatch.GetElapsedTime(pdfStarted).TotalMilliseconds);

                    if (ctx.PdfStoragePath is not null)
                    {
                        ctx.RetryState.CompletedActivityKeys["pdf"] = pdfInputHash;
                        ctx.RetryState.CompletedResultPaths["pdf"] = ctx.PdfStoragePath;
                    }
                }
            }

            // Step 6: Draft lead email via LeadCommunicatorService
            var draftEmailHash = ComputeDraftEmailHash(ctx);
            if (ctx.RetryState.IsCompleted("draftLeadEmail", draftEmailHash))
            {
                logger.LogInformation(
                    "[{Worker}-040a] Email draft skipped — same inputs already drafted. Lead {LeadId}. CorrelationId: {CorrelationId}",
                    WorkerName, lead.Id, correlationId);
            }
            else
            {
                var emailDraftStarted = Stopwatch.GetTimestamp();
                using (OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.draft_email"))
                {
                    try
                    {
                        ctx.LeadEmail = await communicationService.DraftAsync(ctx, ct);
                        if (ctx.LeadEmail is not null)
                        {
                            ctx.RetryState.CompletedActivityKeys["draftLeadEmail"] = draftEmailHash;
                            ctx.RetryState.CompletedResultPaths["draftLeadEmail"] = $"email:{lead.Id}";
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "[{Worker}-040] Email draft failed for lead {LeadId}. CorrelationId: {CorrelationId}",
                            WorkerName, lead.Id, correlationId);
                    }
                }
                OrchestratorDiagnostics.EmailDraftDurationMs.Record(
                    Stopwatch.GetElapsedTime(emailDraftStarted).TotalMilliseconds);
            }

            // Step 7: Send lead email
            if (ctx.LeadEmail is not null)
            {
                await SendLeadEmailAsync(ctx, correlationId, ct);
            }

            // Step 8: Notify agent via AgentNotifierService
            await NotifyAgentAsync(ctx, correlationId, ct);

            // Step 9: Set final status + persist all artifacts
            // PersistActivity handles: status → Complete, score, CMA/HS summaries,
            // email/notification drafts, retry state. Only Scored and Analyzing are
            // written inline as concurrency gates (above). All result data persists here.
            lead.Status = LeadStatus.Complete;
            try
            {
                await persistActivity.ExecuteAsync(ctx, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[{Worker}-085] PersistActivity failed for lead {LeadId}. CorrelationId: {CorrelationId}",
                    WorkerName, lead.Id, correlationId);
            }
            healthTracker.RecordActivity(WorkerName);

            var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            LeadDiagnostics.TotalPipelineDuration.Record((long)elapsedMs);
            OrchestratorDiagnostics.TotalDurationMs.Record(elapsedMs);

            var anyTimeout = (cmaTcs is not null && !cmaTcs.Task.IsCompleted) ||
                             (hsTcs is not null && !hsTcs.Task.IsCompleted);
            if (anyTimeout)
                OrchestratorDiagnostics.LeadsPartial.Add(1);
            else
                OrchestratorDiagnostics.LeadsCompleted.Add(1);

            logger.LogInformation(
                "[{Worker}-090] Lead {LeadId} pipeline complete. CorrelationId: {CorrelationId}",
                WorkerName, lead.Id, correlationId);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "[{Worker}-095] Lead {LeadId} orchestration cancelled. CorrelationId: {CorrelationId}",
                WorkerName, lead.Id, correlationId);
            throw;
        }
        catch (Exception ex)
        {
            OrchestratorDiagnostics.LeadsFailed.Add(1);
            logger.LogError(ex,
                "[{Worker}-099] Lead {LeadId} orchestration failed. CorrelationId: {CorrelationId}",
                WorkerName, lead.Id, correlationId);
        }
    }

    // ── Content hash helpers ─────────────────────────────────────────────────

    internal static string ComputeCmaInputHash(Domain.Leads.Models.Lead lead) =>
        ContentHash.Compute(
            lead.SellerDetails?.Address,
            lead.SellerDetails?.City,
            lead.SellerDetails?.State,
            lead.SellerDetails?.Zip);

    internal static string ComputeHsInputHash(Domain.Leads.Models.Lead lead) =>
        ContentHash.Compute(
            lead.BuyerDetails?.City,
            lead.BuyerDetails?.State,
            lead.BuyerDetails?.MinBudget?.ToString(),
            lead.BuyerDetails?.MaxBudget?.ToString(),
            lead.BuyerDetails?.Bedrooms?.ToString(),
            lead.BuyerDetails?.Bathrooms?.ToString());

    internal static string ComputePdfInputHash(CmaWorkerResult? cmaResult) =>
        cmaResult is null
            ? ContentHash.Compute()
            : ContentHash.Compute(
                cmaResult.EstimatedValue?.ToString(),
                cmaResult.PriceRangeLow?.ToString(),
                cmaResult.PriceRangeHigh?.ToString(),
                cmaResult.MarketAnalysis,
                cmaResult.Comps?.Count.ToString());

    internal static string ComputeDraftEmailHash(LeadPipelineContext ctx) =>
        ContentHash.Compute(
            ctx.Score?.OverallScore.ToString(),
            ctx.RetryState.GetHash("cma"),
            ctx.RetryState.GetHash("homeSearch"));

    // ── Worker dispatch (content-aware) ─────────────────────────────────────

    internal async Task<(TaskCompletionSource<CmaWorkerResult>? CmaTcs, TaskCompletionSource<HomeSearchWorkerResult>? HsTcs)>
        DispatchWorkersAsync(
            Domain.Leads.Models.Lead lead,
            string agentId,
            AgentNotificationConfig agentConfig,
            string correlationId,
            LeadRetryState retryState,
            string cmaInputHash,
            string hsInputHash,
            CancellationToken ct,
            Domain.Activation.Models.AgentContext? agentContext = null)
    {
        TaskCompletionSource<CmaWorkerResult>? cmaTcs = null;
        TaskCompletionSource<HomeSearchWorkerResult>? hsTcs = null;

        if (lead.LeadType is LeadType.Seller or LeadType.Both && lead.SellerDetails is not null)
        {
            cmaTcs = new TaskCompletionSource<CmaWorkerResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Check per-lead retry state first
            if (retryState.IsCompleted("cma", cmaInputHash))
            {
                logger.LogInformation(
                    "[{Worker}-021a] CMA skipped — same inputs already completed. Lead {LeadId}. CorrelationId: {CorrelationId}",
                    WorkerName, lead.Id, correlationId);
                // We don't have the cached result stored locally, so re-run to get the data
                // (RetryState only stores the hash, not the full result)
                cmaTcs = null;
            }
            else
            {
                // Check cross-lead content cache
                var cached = await contentCache.GetAsync<CmaWorkerResult>(cmaInputHash, ct);
                if (cached is not null)
                {
                    logger.LogInformation(
                        "[{Worker}-021b] CMA cache hit for lead {LeadId}. CorrelationId: {CorrelationId}",
                        WorkerName, lead.Id, correlationId);
                    cmaTcs.TrySetResult(cached);
                    OrchestratorDiagnostics.WorkerCompletions.Add(1);
                }
                else if (!cmaChannel.Writer.TryWrite(new CmaProcessingRequest(agentId, lead, agentConfig, correlationId, cmaTcs, agentContext)))
                {
                    logger.LogError(
                        "[{Worker}-024] CMA channel full — request dropped for lead {LeadId}. CorrelationId: {CorrelationId}",
                        WorkerName, lead.Id, correlationId);
                    cmaTcs.TrySetResult(new CmaWorkerResult(lead.Id.ToString(), false, "Channel full", null, null, null, null, null));
                }
                else
                {
                    OrchestratorDiagnostics.WorkerDispatches.Add(1);
                    logger.LogInformation(
                        "[{Worker}-022] CMA dispatched for lead {LeadId}. CorrelationId: {CorrelationId}",
                        WorkerName, lead.Id, correlationId);
                }
            }
        }

        if (lead.LeadType is LeadType.Buyer or LeadType.Both && lead.BuyerDetails is not null)
        {
            hsTcs = new TaskCompletionSource<HomeSearchWorkerResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Check per-lead retry state first
            if (retryState.IsCompleted("homeSearch", hsInputHash))
            {
                logger.LogInformation(
                    "[{Worker}-023a] HomeSearch skipped — same inputs already completed. Lead {LeadId}. CorrelationId: {CorrelationId}",
                    WorkerName, lead.Id, correlationId);
                hsTcs = null;
            }
            else
            {
                // Check cross-lead content cache
                var cached = await contentCache.GetAsync<HomeSearchWorkerResult>(hsInputHash, ct);
                if (cached is not null)
                {
                    logger.LogInformation(
                        "[{Worker}-023b] HomeSearch cache hit for lead {LeadId}. CorrelationId: {CorrelationId}",
                        WorkerName, lead.Id, correlationId);
                    hsTcs.TrySetResult(cached);
                    OrchestratorDiagnostics.WorkerCompletions.Add(1);
                }
                else if (!homeSearchChannel.Writer.TryWrite(new HomeSearchProcessingRequest(agentId, lead, agentConfig, correlationId, hsTcs)))
                {
                    logger.LogError(
                        "[{Worker}-025] HomeSearch channel full — request dropped for lead {LeadId}. CorrelationId: {CorrelationId}",
                        WorkerName, lead.Id, correlationId);
                    hsTcs.TrySetResult(new HomeSearchWorkerResult(lead.Id.ToString(), false, "Channel full", null, null));
                }
                else
                {
                    OrchestratorDiagnostics.WorkerDispatches.Add(1);
                    logger.LogInformation(
                        "[{Worker}-023] HomeSearch dispatched for lead {LeadId}. CorrelationId: {CorrelationId}",
                        WorkerName, lead.Id, correlationId);
                }
            }
        }

        return (cmaTcs, hsTcs);
    }

    // ── Backward-compatible non-async DispatchWorkers (kept for tests) ────────

    internal (TaskCompletionSource<CmaWorkerResult>? CmaTcs, TaskCompletionSource<HomeSearchWorkerResult>? HsTcs)
        DispatchWorkers(Domain.Leads.Models.Lead lead, string agentId, AgentNotificationConfig agentConfig, string correlationId)
    {
        TaskCompletionSource<CmaWorkerResult>? cmaTcs = null;
        TaskCompletionSource<HomeSearchWorkerResult>? hsTcs = null;

        if (lead.LeadType is LeadType.Seller or LeadType.Both && lead.SellerDetails is not null)
        {
            cmaTcs = new TaskCompletionSource<CmaWorkerResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!cmaChannel.Writer.TryWrite(new CmaProcessingRequest(agentId, lead, agentConfig, correlationId, cmaTcs)))
            {
                logger.LogError(
                    "[{Worker}-024] CMA channel full — request dropped for lead {LeadId}. CorrelationId: {CorrelationId}",
                    WorkerName, lead.Id, correlationId);
                cmaTcs.TrySetResult(new CmaWorkerResult(lead.Id.ToString(), false, "Channel full", null, null, null, null, null));
            }
            else
            {
                OrchestratorDiagnostics.WorkerDispatches.Add(1);
                logger.LogInformation(
                    "[{Worker}-022] CMA dispatched for lead {LeadId}. CorrelationId: {CorrelationId}",
                    WorkerName, lead.Id, correlationId);
            }
        }

        if (lead.LeadType is LeadType.Buyer or LeadType.Both && lead.BuyerDetails is not null)
        {
            hsTcs = new TaskCompletionSource<HomeSearchWorkerResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!homeSearchChannel.Writer.TryWrite(new HomeSearchProcessingRequest(agentId, lead, agentConfig, correlationId, hsTcs)))
            {
                logger.LogError(
                    "[{Worker}-025] HomeSearch channel full — request dropped for lead {LeadId}. CorrelationId: {CorrelationId}",
                    WorkerName, lead.Id, correlationId);
                hsTcs.TrySetResult(new HomeSearchWorkerResult(lead.Id.ToString(), false, "Channel full", null, null));
            }
            else
            {
                OrchestratorDiagnostics.WorkerDispatches.Add(1);
                logger.LogInformation(
                    "[{Worker}-023] HomeSearch dispatched for lead {LeadId}. CorrelationId: {CorrelationId}",
                    WorkerName, lead.Id, correlationId);
            }
        }

        return (cmaTcs, hsTcs);
    }

    private static List<Task> BuildPendingTasks(
        TaskCompletionSource<CmaWorkerResult>? cmaTcs,
        TaskCompletionSource<HomeSearchWorkerResult>? hsTcs)
    {
        var tasks = new List<Task>();
        if (cmaTcs is not null) tasks.Add(cmaTcs.Task);
        if (hsTcs is not null) tasks.Add(hsTcs.Task);
        return tasks;
    }

    private void LogWorkerResults(
        CmaWorkerResult? cmaResult,
        HomeSearchWorkerResult? hsResult,
        Guid leadId,
        string correlationId)
    {
        if (cmaResult is not null)
            logger.LogInformation(
                "[{Worker}-031] CMA result for lead {LeadId}: Success={Success}, Error={Error}. CorrelationId: {CorrelationId}",
                WorkerName, leadId, cmaResult.Success, cmaResult.Error, correlationId);
        else
            logger.LogWarning(
                "[{Worker}-032] No CMA result for lead {LeadId} (timeout or not dispatched). CorrelationId: {CorrelationId}",
                WorkerName, leadId, correlationId);

        if (hsResult is not null)
            logger.LogInformation(
                "[{Worker}-033] HomeSearch result for lead {LeadId}: Success={Success}, ListingCount={Count}. CorrelationId: {CorrelationId}",
                WorkerName, leadId, hsResult.Success, hsResult.Listings?.Count ?? 0, correlationId);
        else
            logger.LogInformation(
                "[{Worker}-034] No HomeSearch result for lead {LeadId} (timeout or not dispatched). CorrelationId: {CorrelationId}",
                WorkerName, leadId, correlationId);
    }

    private async Task<string?> GeneratePdfAsync(
        LeadPipelineContext ctx,
        Domain.Shared.Models.AccountConfig accountConfig,
        string correlationId,
        CancellationToken ct)
    {
        var cmaResult = ctx.CmaResult!;

        logger.LogInformation(
            "[{Worker}-035] Generating PDF for lead {LeadId}. CorrelationId: {CorrelationId}",
            WorkerName, ctx.Lead.Id, correlationId);

        try
        {
            // Reconstruct CmaAnalysis from the flat fields in CmaWorkerResult
            var analysis = new Domain.Cma.Models.CmaAnalysis
            {
                ValueLow = cmaResult.PriceRangeLow ?? cmaResult.EstimatedValue ?? 0m,
                ValueMid = cmaResult.EstimatedValue ?? 0m,
                ValueHigh = cmaResult.PriceRangeHigh ?? cmaResult.EstimatedValue ?? 0m,
                MarketNarrative = cmaResult.MarketAnalysis ?? string.Empty,
                PricingStrategy = cmaResult.PricingStrategy,
                MarketTrend = "Stable",
                MedianDaysOnMarket = 0
            };

            logger.LogDebug(
                "[{Worker}-035b] PDF input for lead {LeadId}: PricingStrategy={HasStrategy} ({StrategyLength} chars), " +
                "MarketNarrative={NarrativeLength} chars, ValueRange={Low}-{Mid}-{High}, Comps={CompCount}",
                WorkerName, ctx.Lead.Id,
                analysis.PricingStrategy is not null,
                analysis.PricingStrategy?.Length ?? 0,
                analysis.MarketNarrative.Length,
                analysis.ValueLow, analysis.ValueMid, analysis.ValueHigh,
                cmaResult.Comps?.Count ?? 0);

            // Convert CompSummary → Comp for PDF generation (use defaults for required fields missing in summary)
            var comps = (cmaResult.Comps ?? [])
                .Select(c => new Domain.Cma.Models.Comp
                {
                    Address = c.Address,
                    SalePrice = c.Price,
                    SaleDate = c.SaleDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    Beds = c.Beds ?? 0,
                    Baths = (int)(c.Baths ?? 0),
                    Sqft = c.Sqft ?? 0,
                    DaysOnMarket = c.DaysOnMarket,
                    DistanceMiles = c.Distance ?? 0.0,
                    Source = Domain.Cma.Models.CompSource.RentCast
                })
                .ToList();

            var storagePath = await pdfActivity.ExecuteAsync(
                ctx.Lead,
                analysis,
                comps,
                accountConfig,
                Domain.Cma.Models.ReportType.Standard,
                null,
                null,
                correlationId,
                ct);

            logger.LogInformation(
                "[{Worker}-036] PDF generated for lead {LeadId}. Path: {Path}. CorrelationId: {CorrelationId}",
                WorkerName, ctx.Lead.Id, storagePath, correlationId);

            return storagePath;
        }
        catch (TimeoutException)
        {
            logger.LogWarning(
                "[{Worker}-037] PDF generation timed out for lead {LeadId}. CorrelationId: {CorrelationId}",
                WorkerName, ctx.Lead.Id, correlationId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[{Worker}-038] PDF generation failed for lead {LeadId}. CorrelationId: {CorrelationId}",
                WorkerName, ctx.Lead.Id, correlationId);
            return null;
        }
    }

    private async Task SendLeadEmailAsync(
        LeadPipelineContext ctx,
        string correlationId,
        CancellationToken ct)
    {
        var sendStarted = Stopwatch.GetTimestamp();
        using var span = OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.send_email");
        try
        {
            ctx.LeadEmail = await communicationService.SendAsync(ctx.LeadEmail!, ctx, ct);

            if (ctx.LeadEmail.Sent)
            {
                LeadDiagnostics.LeadsNotificationSent.Add(1);
                OrchestratorDiagnostics.EmailSent.Add(1);
                logger.LogInformation(
                    "[{Worker}-050] Email sent for lead {LeadId}. CorrelationId: {CorrelationId}",
                    WorkerName, ctx.Lead.Id, correlationId);
            }
            else
            {
                LeadDiagnostics.LeadsNotificationFailed.Add(1);
                OrchestratorDiagnostics.EmailFailed.Add(1);
                logger.LogWarning(
                    "[{Worker}-051] Email send returned without success for lead {LeadId}. Error: {Error}. CorrelationId: {CorrelationId}",
                    WorkerName, ctx.Lead.Id, ctx.LeadEmail.Error, correlationId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[{Worker}-052] Email send threw for lead {LeadId}. CorrelationId: {CorrelationId}",
                WorkerName, ctx.Lead.Id, correlationId);
            LeadDiagnostics.LeadsNotificationFailed.Add(1);
            OrchestratorDiagnostics.EmailFailed.Add(1);
        }
        finally
        {
            OrchestratorDiagnostics.EmailSendDurationMs.Record(
                Stopwatch.GetElapsedTime(sendStarted).TotalMilliseconds);
        }
    }

    private async Task NotifyAgentAsync(
        LeadPipelineContext ctx,
        string correlationId,
        CancellationToken ct)
    {
        var notifyStarted = Stopwatch.GetTimestamp();
        using var span = OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.notify_agent");
        try
        {
            await agentNotifier.NotifyAsync(
                ctx.Lead,
                ctx.Score ?? new LeadScore { OverallScore = 0, Factors = [], Explanation = string.Empty },
                ctx.CmaResult,
                ctx.HsResult,
                ctx.AgentConfig,
                ct);

            ctx.AgentNotification = new CommunicationRecord
            {
                Subject = "New Lead Notification",
                HtmlBody = string.Empty,
                Channel = ctx.AgentConfig.WhatsAppPhoneNumberId != null ? "whatsapp" : "email",
                DraftedAt = DateTimeOffset.UtcNow,
                SentAt = DateTimeOffset.UtcNow,
                Sent = true,
                ContentHash = ContentHash.Compute(
                    ctx.Lead.Id.ToString(),
                    ctx.Score?.OverallScore.ToString(),
                    ctx.CmaResult?.Success.ToString(),
                    ctx.HsResult?.Success.ToString())
            };

            OrchestratorDiagnostics.WhatsAppSent.Add(1);
            logger.LogInformation(
                "[{Worker}-060] Agent notified for lead {LeadId}. CorrelationId: {CorrelationId}",
                WorkerName, ctx.Lead.Id, correlationId);
        }
        catch (Exception ex)
        {
            OrchestratorDiagnostics.WhatsAppFailed.Add(1);
            logger.LogError(ex,
                "[{Worker}-061] Agent notification failed for lead {LeadId}. CorrelationId: {CorrelationId}",
                WorkerName, ctx.Lead.Id, correlationId);
        }
        finally
        {
            OrchestratorDiagnostics.WhatsAppSendDurationMs.Record(
                Stopwatch.GetElapsedTime(notifyStarted).TotalMilliseconds);
        }
    }

    internal static AgentNotificationConfig BuildAgentNotificationConfig(
        string agentId,
        Domain.Shared.Models.AccountConfig accountConfig)
    {
        return new AgentNotificationConfig
        {
            AgentId = agentId,
            Handle = accountConfig.Handle,
            Name = accountConfig.Agent?.Name ?? "",
            FirstName = accountConfig.Agent?.Name?.Split(' ').FirstOrDefault() ?? "",
            Email = accountConfig.Agent?.Email ?? "",
            Phone = accountConfig.Agent?.Phone ?? "",
            LicenseNumber = accountConfig.Agent?.LicenseNumber ?? "",
            BrokerageName = accountConfig.Brokerage?.Name ?? "",
            BrokerageLogo = accountConfig.Branding?.LogoUrl,
            PrimaryColor = accountConfig.Branding?.PrimaryColor ?? "#000000",
            AccentColor = accountConfig.Branding?.AccentColor ?? "#000000",
            State = accountConfig.Location?.State ?? "",
            ServiceAreas = accountConfig.Location?.ServiceAreas ?? [],
            WhatsAppPhoneNumberId = accountConfig.Integrations?.WhatsApp?.PhoneNumber,
        };
    }
}
