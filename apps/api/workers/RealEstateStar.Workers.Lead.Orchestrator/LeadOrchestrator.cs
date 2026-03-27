using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Orchestration;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Workers.Cma;
using RealEstateStar.Workers.HomeSearch;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Shared.AgentNotifier;
using RealEstateStar.Workers.Shared.LeadCommunicator;
using RealEstateStar.Workers.Shared.Pdf;

namespace RealEstateStar.Workers.Lead.Orchestrator;

/// <summary>
/// Per-lead orchestrator. Reads from <see cref="LeadOrchestratorChannel"/>,
/// scores the lead, dispatches CMA and HomeSearch workers in parallel via channels,
/// calls <see cref="PdfActivity"/> inline, drafts and sends the lead email via
/// <see cref="LeadCommunicationService"/>, and notifies the agent via
/// <see cref="AgentNotificationService"/>. All pipeline state is carried in
/// a <see cref="LeadPipelineContext"/> instance.
/// </summary>
public sealed class LeadOrchestrator(
    LeadOrchestratorChannel channel,
    ILeadStore leadStore,
    IAccountConfigService accountConfigService,
    ILeadScorer scorer,
    CmaProcessingChannel cmaChannel,
    HomeSearchProcessingChannel homeSearchChannel,
    PdfActivity pdfActivity,
    LeadCommunicationService communicationService,
    AgentNotificationService agentNotificationService,
    BackgroundServiceHealthTracker healthTracker,
    ILogger<LeadOrchestrator> logger,
    IConfiguration configuration)
    : BackgroundService
{
    private const string WorkerName = "LeadOrchestrator";

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

            // Build the shared pipeline context
            var ctx = new LeadPipelineContext
            {
                Lead = lead,
                AgentConfig = agentConfig,
                CorrelationId = correlationId
            };

            // Step 2: Score the lead
            var scoreStarted = Stopwatch.GetTimestamp();
            using var scoreSpan = OrchestratorDiagnostics.ActivitySource.StartActivity("activity.score");
            ctx.Score = scorer.Score(lead);
            lead.Score = ctx.Score;
            OrchestratorDiagnostics.ScoreDurationMs.Record(
                Stopwatch.GetElapsedTime(scoreStarted).TotalMilliseconds);
            await UpdateStatusAsync(lead, LeadStatus.Scored, ct);

            logger.LogInformation(
                "[{Worker}-020] Lead {LeadId} scored: {Score}/100 ({Bucket}). CorrelationId: {CorrelationId}",
                WorkerName, lead.Id, ctx.Score.OverallScore, ctx.Score.Bucket, correlationId);

            // Step 3: Dispatch CMA + HomeSearch in parallel via channels, collect via TCS
            await UpdateStatusAsync(lead, LeadStatus.Analyzing, ct);

            var (cmaTcs, hsTcs) = DispatchWorkers(lead, agentId, agentConfig, correlationId);

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
            if (cmaTcs is not null)
            {
                if (cmaTcs.Task.IsCompleted)
                {
                    ctx.CmaResult = cmaTcs.Task.Result;
                    OrchestratorDiagnostics.WorkerCompletions.Add(1);
                }
                else
                {
                    OrchestratorDiagnostics.WorkerTimeouts.Add(1);
                }
            }

            if (hsTcs is not null)
            {
                if (hsTcs.Task.IsCompleted)
                {
                    ctx.HsResult = hsTcs.Task.Result;
                    OrchestratorDiagnostics.WorkerCompletions.Add(1);
                }
                else
                {
                    OrchestratorDiagnostics.WorkerTimeouts.Add(1);
                }
            }

            LogWorkerResults(ctx.CmaResult, ctx.HsResult, lead.Id, correlationId);

            // Step 5: Generate PDF inline via PdfActivity (CMA succeeded required)
            if (ctx.CmaResult?.Success == true)
            {
                var pdfStarted = Stopwatch.GetTimestamp();
                using (OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.generate_pdf"))
                {
                    ctx.PdfStoragePath = await GeneratePdfAsync(ctx, accountConfig, correlationId, ct);
                }
                OrchestratorDiagnostics.PdfDurationMs.Record(
                    Stopwatch.GetElapsedTime(pdfStarted).TotalMilliseconds);
            }

            // Step 6: Draft lead email via LeadCommunicationService
            var emailDraftStarted = Stopwatch.GetTimestamp();
            using (OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.draft_email"))
            {
                try
                {
                    ctx.LeadEmail = await communicationService.DraftAsync(ctx, ct);
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

            // Always set Notified — notification was attempted regardless of draft success
            await UpdateStatusAsync(lead, LeadStatus.Notified, ct);

            // Step 7: Send lead email
            if (ctx.LeadEmail is not null)
            {
                await SendLeadEmailAsync(ctx, correlationId, ct);
            }

            // Step 8: Notify agent via AgentNotificationService
            await NotifyAgentAsync(ctx, correlationId, ct);

            // Step 9: Update status to Complete
            await UpdateStatusAsync(lead, LeadStatus.Complete, ct);
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

    internal (TaskCompletionSource<CmaWorkerResult>? CmaTcs, TaskCompletionSource<HomeSearchWorkerResult>? HsTcs)
        DispatchWorkers(global::RealEstateStar.Domain.Leads.Models.Lead lead, string agentId, AgentNotificationConfig agentConfig, string correlationId)
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
                MarketTrend = "Stable",
                MedianDaysOnMarket = 0
            };

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
            await agentNotificationService.NotifyAsync(
                ctx.Lead,
                ctx.Score ?? new LeadScore { OverallScore = 0, Factors = [], Explanation = string.Empty },
                ctx.CmaResult,
                ctx.HsResult,
                ctx.AgentConfig,
                ct);

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

    private async Task UpdateStatusAsync(global::RealEstateStar.Domain.Leads.Models.Lead lead, LeadStatus status, CancellationToken ct)
    {
        try
        {
            await leadStore.UpdateStatusAsync(lead, status, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[{Worker}-070] Failed to update status to {Status} for lead {LeadId}.",
                WorkerName, status, lead.Id);
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
