using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Orchestration;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Workers.Cma;
using RealEstateStar.Workers.HomeSearch;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Leads;

/// <summary>
/// Central lead pipeline coordinator. Reads from <see cref="LeadOrchestratorChannel"/>,
/// scores the lead, dispatches CMA and HomeSearch workers in parallel, collects results,
/// generates the PDF, drafts and sends the email, and notifies the agent via WhatsApp.
/// </summary>
public sealed class LeadOrchestrator(
    LeadOrchestratorChannel channel,
    ILeadStore leadStore,
    IAccountConfigService accountConfigService,
    ILeadScorer scorer,
    CmaProcessingChannel cmaChannel,
    HomeSearchProcessingChannel homeSearchChannel,
    PdfProcessingChannel pdfChannel,
    ILeadEmailDrafter emailDrafter,
    IGmailSender gmailSender,
    IAgentNotifier agentNotifier,
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
        activity?.SetTag("lead.agent_id", agentId);
        activity?.SetTag("correlation.id", correlationId);

        // Keep existing lead-level trace span for backward compatibility
        using var leadActivity = LeadDiagnostics.ActivitySource.StartActivity("lead.orchestrate");
        leadActivity?.SetTag("lead.id", lead.Id.ToString());
        leadActivity?.SetTag("lead.agent_id", agentId);
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

            // Step 2: Score the lead
            var scoreStarted = Stopwatch.GetTimestamp();
            using var scoreSpan = OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.score_lead");
            var score = scorer.Score(lead);
            lead.Score = score;
            OrchestratorDiagnostics.ScoreDurationMs.Record(
                Stopwatch.GetElapsedTime(scoreStarted).TotalMilliseconds);
            await UpdateStatusAsync(lead, LeadStatus.Scored, ct);

            logger.LogInformation(
                "[{Worker}-020] Lead {LeadId} scored: {Score}/100 ({Bucket}). CorrelationId: {CorrelationId}",
                WorkerName, lead.Id, score.OverallScore, score.Bucket, correlationId);

            // Step 3: Dispatch CMA + HomeSearch in parallel, collect via TCS
            await UpdateStatusAsync(lead, LeadStatus.Analyzing, ct);

            var (cmaTcs, hsTcs) = DispatchWorkers(lead, agentId, agentConfig, correlationId);

            // Step 4: Wait for workers with configurable timeout
            CmaWorkerResult? cmaResult = null;
            HomeSearchWorkerResult? hsResult = null;

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

            // Collect results from completed TCS tasks
            if (cmaTcs is not null)
            {
                if (cmaTcs.Task.IsCompleted)
                {
                    cmaResult = cmaTcs.Task.Result;
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
                    hsResult = hsTcs.Task.Result;
                    OrchestratorDiagnostics.WorkerCompletions.Add(1);
                }
                else
                {
                    OrchestratorDiagnostics.WorkerTimeouts.Add(1);
                }
            }

            LogWorkerResults(cmaResult, hsResult, lead.Id, correlationId);

            // Step 5: Dispatch PDF if CMA succeeded
            PdfWorkerResult? pdfResult = null;
            if (cmaResult?.Success == true)
            {
                var pdfStarted = Stopwatch.GetTimestamp();
                using (OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.dispatch_pdf"))
                {
                    pdfResult = await DispatchPdfAsync(lead.Id.ToString(), cmaResult, agentConfig, correlationId, ct);
                }
                OrchestratorDiagnostics.PdfDurationMs.Record(
                    Stopwatch.GetElapsedTime(pdfStarted).TotalMilliseconds);
            }

            // Step 6: Draft email
            LeadEmail? emailDraft = null;
            var emailDraftStarted = Stopwatch.GetTimestamp();
            using (OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.draft_email"))
            {
                try
                {
                    emailDraft = await emailDrafter.DraftAsync(lead, score, cmaResult, hsResult, agentConfig, ct);
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

            // Step 7: Send email
            if (emailDraft is not null)
            {
                await SendEmailAsync(lead, agentId, agentConfig, emailDraft, pdfResult, correlationId, ct);
            }

            // Step 8: Notify agent via WhatsApp/notifier
            await NotifyAgentAsync(lead, score, cmaResult, hsResult, agentConfig, correlationId, ct);

            // Step 9: Save results + update status
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
        DispatchWorkers(Lead lead, string agentId, AgentNotificationConfig agentConfig, string correlationId)
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

    private async Task<PdfWorkerResult?> DispatchPdfAsync(
        string leadId,
        CmaWorkerResult cmaResult,
        AgentNotificationConfig agentConfig,
        string correlationId,
        CancellationToken ct)
    {
        var pdfTcs = new TaskCompletionSource<PdfWorkerResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        pdfChannel.Writer.TryWrite(
            new PdfProcessingRequest(leadId, cmaResult, agentConfig, correlationId, pdfTcs));

        logger.LogInformation(
            "[{Worker}-035] PDF dispatched for lead {LeadId}. CorrelationId: {CorrelationId}",
            WorkerName, leadId, correlationId);

        try
        {
            return await pdfTcs.Task.WaitAsync(TimeSpan.FromSeconds(WorkerTimeoutSeconds), ct);
        }
        catch (TimeoutException)
        {
            logger.LogWarning(
                "[{Worker}-036] PDF generation timed out for lead {LeadId}. CorrelationId: {CorrelationId}",
                WorkerName, leadId, correlationId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[{Worker}-037] PDF generation failed for lead {LeadId}. CorrelationId: {CorrelationId}",
                WorkerName, leadId, correlationId);
            return null;
        }
    }

    private async Task SendEmailAsync(
        Lead lead,
        string agentId,
        AgentNotificationConfig agentConfig,
        LeadEmail emailDraft,
        PdfWorkerResult? pdfResult,
        string correlationId,
        CancellationToken ct)
    {
        var sendStarted = Stopwatch.GetTimestamp();
        using var span = OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.send_email");
        try
        {
            await gmailSender.SendAsync(agentId, agentId, agentConfig.Email, emailDraft.Subject, emailDraft.HtmlBody, ct);

            LeadDiagnostics.LeadsNotificationSent.Add(1);
            OrchestratorDiagnostics.EmailSent.Add(1);

            logger.LogInformation(
                "[{Worker}-050] Email sent for lead {LeadId}. CorrelationId: {CorrelationId}",
                WorkerName, lead.Id, correlationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[{Worker}-051] Email send failed for lead {LeadId}. CorrelationId: {CorrelationId}",
                WorkerName, lead.Id, correlationId);
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
        Lead lead,
        LeadScore score,
        CmaWorkerResult? cmaResult,
        HomeSearchWorkerResult? hsResult,
        AgentNotificationConfig agentConfig,
        string correlationId,
        CancellationToken ct)
    {
        var notifyStarted = Stopwatch.GetTimestamp();
        using var span = OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.notify_agent");
        try
        {
            await agentNotifier.NotifyAsync(lead, score, cmaResult, hsResult, agentConfig, ct);

            OrchestratorDiagnostics.WhatsAppSent.Add(1);
            logger.LogInformation(
                "[{Worker}-060] Agent notified for lead {LeadId}. CorrelationId: {CorrelationId}",
                WorkerName, lead.Id, correlationId);
        }
        catch (Exception ex)
        {
            OrchestratorDiagnostics.WhatsAppFailed.Add(1);
            logger.LogError(ex,
                "[{Worker}-061] Agent notification failed for lead {LeadId}. CorrelationId: {CorrelationId}",
                WorkerName, lead.Id, correlationId);
        }
        finally
        {
            OrchestratorDiagnostics.WhatsAppSendDurationMs.Record(
                Stopwatch.GetElapsedTime(notifyStarted).TotalMilliseconds);
        }
    }

    private async Task UpdateStatusAsync(Lead lead, LeadStatus status, CancellationToken ct)
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
