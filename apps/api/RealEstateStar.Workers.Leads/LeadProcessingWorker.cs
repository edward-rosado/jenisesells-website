using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Workers.Cma;
using RealEstateStar.Workers.HomeSearch;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Leads;

/// <summary>
/// Background service that processes leads from the <see cref="LeadProcessingChannel"/>.
/// Uses checkpoint/resume: each step checks if its output already exists before re-running.
/// This saves Claude tokens and ScraperAPI credits on retries.
/// </summary>
public sealed class LeadProcessingWorker(
    LeadProcessingChannel channel,
    ILeadStore leadStore,
    ILeadEnricher enricher,
    ILeadNotifier notifier,
    IFailedNotificationStore failedNotificationStore,
    IFileStorageProvider storage,
    CmaProcessingChannel cmaChannel,
    HomeSearchProcessingChannel homeSearchChannel,
    BackgroundServiceHealthTracker healthTracker,
    ILogger<LeadProcessingWorker> logger) : BackgroundService
{
    // Exposed internal for test injection — production values are 30s/60s/90s
    internal TimeSpan[] RetryDelays { get; init; } =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(90),
    ];
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[WORKER-001] Lead processing worker started.");

        await foreach (var request in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessLeadAsync(request, stoppingToken);
                healthTracker.RecordActivity(nameof(LeadProcessingWorker));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "[WORKER-002] Unhandled error processing lead {LeadId} for agent {AgentId}. CorrelationId: {CorrelationId}",
                    request.Lead.Id, request.AgentId, request.CorrelationId);
            }
        }

        logger.LogInformation("[WORKER-003] Lead processing worker stopping.");
    }

    private async Task ProcessLeadAsync(LeadProcessingRequest request, CancellationToken ct)
    {
        var (agentId, lead, correlationId) = request;
        var pipelineStart = Stopwatch.GetTimestamp();

        using var activity = LeadDiagnostics.ActivitySource.StartActivity("lead.process");
        activity?.SetTag("lead.id", lead.Id.ToString());
        activity?.SetTag("lead.agent_id", agentId);
        activity?.SetTag("correlation.id", correlationId);

        logger.LogInformation(
            "[WORKER-010] Processing lead {LeadId} for agent {AgentId}. Type: {LeadType}. Status: {Status}. CorrelationId: {CorrelationId}",
            lead.Id, agentId, lead.LeadType, lead.Status, correlationId);

        // Step 1: Enrich (checkpoint: Research & Insights.md exists)
        var (enrichment, score) = await EnrichLeadAsync(agentId, lead, ct);

        // Step 2: Draft + send notification (checkpoint: Notification Draft.md exists)
        await NotifyAgentAsync(agentId, lead, enrichment, score, ct);

        // Step 3: Dispatch to CMA pipeline for sellers
        if (lead.LeadType is LeadType.Seller or LeadType.Both && lead.SellerDetails is not null)
            await DispatchCmaAsync(agentId, lead, enrichment, score, correlationId, ct);

        // Step 4: Dispatch to home search pipeline for buyers
        if (lead.LeadType is LeadType.Buyer or LeadType.Both && lead.BuyerDetails is not null)
            await DispatchHomeSearchAsync(agentId, lead, correlationId, ct);

        // Mark complete
        lead.Status = LeadStatus.Complete;
        try { await leadStore.UpdateStatusAsync(lead, LeadStatus.Complete, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "[WORKER-012] Failed to update status to Complete for lead {LeadId}", lead.Id); }

        var totalMs = Stopwatch.GetElapsedTime(pipelineStart).TotalMilliseconds;
        LeadDiagnostics.TotalPipelineDuration.Record(totalMs);

        logger.LogInformation(
            "[WORKER-011] Lead {LeadId} processing complete in {DurationMs}ms. CorrelationId: {CorrelationId}",
            lead.Id, totalMs, correlationId);
    }

    private async Task<(LeadEnrichment, LeadScore)> EnrichLeadAsync(string agentId, Lead lead, CancellationToken ct)
    {
        // Checkpoint: if enrichment file exists, skip Claude + scraping
        var enrichmentFolder = LeadPaths.LeadFolder(lead.FullName);
        var existingEnrichment = await storage.ReadDocumentAsync(enrichmentFolder, "Research & Insights.md", ct);
        if (existingEnrichment is not null && lead.Enrichment is not null)
        {
            logger.LogInformation(
                "[WORKER-020-SKIP] Enrichment already exists for lead {LeadId}. Skipping Claude + scraping.",
                lead.Id);
            return (lead.Enrichment, lead.Score ?? LeadScore.Default("from checkpoint"));
        }

        var enrichment = LeadEnrichment.Empty();
        var score = LeadScore.Default("enrichment not yet run");
        var sw = Stopwatch.GetTimestamp();

        try
        {
            (enrichment, score) = await enricher.EnrichAsync(lead, ct);
            await leadStore.UpdateEnrichmentAsync(lead, enrichment, score, ct);

            lead.Status = LeadStatus.Enriched;
            await leadStore.UpdateStatusAsync(lead, LeadStatus.Enriched, ct);

            LeadDiagnostics.LeadsEnriched.Add(1);
            logger.LogInformation(
                "[WORKER-020] Enrichment complete for lead {LeadId}. Score: {Score}. Duration: {DurationMs}ms",
                lead.Id, score.OverallScore, Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            LeadDiagnostics.LeadsEnrichmentFailed.Add(1);
            logger.LogError(ex,
                "[WORKER-021] Enrichment failed for lead {LeadId}. Continuing with empty profile. Duration: {DurationMs}ms",
                lead.Id, Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }

        LeadDiagnostics.EnrichmentDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        return (enrichment, score);
    }

    private async Task NotifyAgentAsync(string agentId, Lead lead, LeadEnrichment enrichment, LeadScore score, CancellationToken ct)
    {
        // Step 2a: Draft email (checkpoint: Notification Draft.md exists)
        var draftFolder = LeadPaths.LeadFolder(lead.FullName);
        var existingDraft = await storage.ReadDocumentAsync(draftFolder, "Notification Draft.md", ct);

        if (existingDraft is null)
        {
            // Build and save the draft — this is the expensive part (building the email body)
            var subject = notifier.BuildSubject(lead, enrichment, score);
            var body = notifier.BuildBody(lead, enrichment, score);
            var draft = $"---\nsubject: {EscapeYaml(subject)}\n---\n\n{body}";

            await storage.WriteDocumentAsync(draftFolder, "Notification Draft.md", draft, ct);

            lead.Status = LeadStatus.EmailDrafted;
            try { await leadStore.UpdateStatusAsync(lead, LeadStatus.EmailDrafted, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "[WORKER-025] Failed to update status to EmailDrafted for lead {LeadId}", lead.Id); }

            logger.LogInformation("[WORKER-024] Email draft saved for lead {LeadId}.", lead.Id);
        }
        else
        {
            logger.LogInformation("[WORKER-024-SKIP] Email draft already exists for lead {LeadId}. Skipping draft creation.", lead.Id);
        }

        // Step 2b: Send notification (retry with dead letter)
        var sw = Stopwatch.GetTimestamp();
        Exception? lastException = null;

        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    logger.LogInformation(
                        "[WORKER-032] Retry {Attempt}/{MaxRetries} for lead {LeadId}",
                        attempt, RetryDelays.Length, lead.Id);
                    await Task.Delay(RetryDelays[attempt - 1], ct);
                }

                await notifier.NotifyAgentAsync(agentId, lead, enrichment, score, ct);

                lead.Status = LeadStatus.Notified;
                try { await leadStore.UpdateStatusAsync(lead, LeadStatus.Notified, ct); }
                catch (Exception ex) { logger.LogWarning(ex, "[WORKER-031] Failed to update status to Notified for lead {LeadId}", lead.Id); }

                LeadDiagnostics.LeadsNotificationSent.Add(1);
                logger.LogInformation(
                    "[WORKER-030] Agent notification sent for lead {LeadId}. Duration: {DurationMs}ms",
                    lead.Id, Stopwatch.GetElapsedTime(sw).TotalMilliseconds);

                LeadDiagnostics.NotificationDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                LeadDiagnostics.LeadsNotificationFailed.Add(1);
                logger.LogWarning(ex,
                    "[WORKER-033] Notification attempt {Attempt} failed for lead {LeadId}",
                    attempt + 1, lead.Id);
            }
        }

        // All retries exhausted — write to dead letter
        LeadDiagnostics.NotificationPermanentlyFailed.Add(1);
        logger.LogError(lastException,
            "[WORKER-034] Notification permanently failed for lead {LeadId} after {MaxRetries} retries",
            lead.Id, RetryDelays.Length + 1);
        await failedNotificationStore.RecordAsync(agentId, lead.Id, lastException?.Message ?? "Unknown error", RetryDelays.Length + 1, ct);

        LeadDiagnostics.NotificationDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
    }

    private async Task DispatchCmaAsync(
        string agentId, Lead lead, LeadEnrichment enrichment,
        LeadScore score, string correlationId, CancellationToken ct)
    {
        // Checkpoint: skip if CMA already completed
        if (lead.Status >= LeadStatus.CmaComplete)
        {
            logger.LogInformation("[WORKER-040-SKIP] CMA already completed for lead {LeadId}.", lead.Id);
            return;
        }

        try
        {
            var cmaRequest = new CmaProcessingRequest(agentId, lead, enrichment, score, correlationId);
            await cmaChannel.Writer.WriteAsync(cmaRequest, ct);

            logger.LogInformation(
                "[WORKER-040] Dispatched CMA request for lead {LeadId}. CorrelationId: {CorrelationId}",
                lead.Id, correlationId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "[WORKER-041] Failed to dispatch CMA request for lead {LeadId}. CorrelationId: {CorrelationId}",
                lead.Id, correlationId);
        }
    }

    private async Task DispatchHomeSearchAsync(
        string agentId, Lead lead, string correlationId, CancellationToken ct)
    {
        // Checkpoint: skip if home search already completed
        if (lead.Status >= LeadStatus.SearchComplete)
        {
            logger.LogInformation("[WORKER-050-SKIP] Home search already completed for lead {LeadId}.", lead.Id);
            return;
        }

        try
        {
            var hsRequest = new HomeSearchProcessingRequest(agentId, lead, correlationId);
            await homeSearchChannel.Writer.WriteAsync(hsRequest, ct);

            logger.LogInformation(
                "[WORKER-050] Dispatched home search request for lead {LeadId}. CorrelationId: {CorrelationId}",
                lead.Id, correlationId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "[WORKER-051] Failed to dispatch home search request for lead {LeadId}. CorrelationId: {CorrelationId}",
                lead.Id, correlationId);
        }
    }

    private static string EscapeYaml(string value)
        => value.Replace("\"", "\\\"").Replace("\n", " ");
}
