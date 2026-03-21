using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Cma;
using RealEstateStar.Workers.HomeSearch;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Leads;

/// <summary>
/// Background service that processes leads from the <see cref="LeadProcessingChannel"/>.
/// Runs enrichment and agent notification, then fans out to dedicated pipelines:
/// <see cref="CmaProcessingWorker"/> for seller CMA and <see cref="HomeSearchProcessingWorker"/> for buyer home search.
/// </summary>
public sealed class LeadProcessingWorker(
    LeadProcessingChannel channel,
    ILeadStore leadStore,
    ILeadEnricher enricher,
    ILeadNotifier notifier,
    CmaProcessingChannel cmaChannel,
    HomeSearchProcessingChannel homeSearchChannel,
    BackgroundServiceHealthTracker healthTracker,
    ILogger<LeadProcessingWorker> logger) : BackgroundService
{
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
            "[WORKER-010] Processing lead {LeadId} for agent {AgentId}. Type: {LeadType}. CorrelationId: {CorrelationId}",
            lead.Id, agentId, lead.LeadType, correlationId);

        // Step 1: Enrich
        var (enrichment, score) = await EnrichLeadAsync(agentId, lead, ct);

        // Step 2: Notify agent
        await NotifyAgentAsync(agentId, lead, enrichment, score, ct);

        // Step 3: Dispatch to CMA pipeline for sellers
        if (lead.LeadType is LeadType.Seller or LeadType.Both && lead.SellerDetails is not null)
            await DispatchCmaAsync(agentId, lead, enrichment, score, correlationId, ct);

        // Step 4: Dispatch to home search pipeline for buyers
        if (lead.LeadType is LeadType.Buyer or LeadType.Both && lead.BuyerDetails is not null)
            await DispatchHomeSearchAsync(agentId, lead, correlationId, ct);

        var totalMs = Stopwatch.GetElapsedTime(pipelineStart).TotalMilliseconds;
        LeadDiagnostics.TotalPipelineDuration.Record(totalMs);

        logger.LogInformation(
            "[WORKER-011] Lead {LeadId} processing complete in {DurationMs}ms. CorrelationId: {CorrelationId}",
            lead.Id, totalMs, correlationId);
    }

    private async Task<(LeadEnrichment, LeadScore)> EnrichLeadAsync(string agentId, Lead lead, CancellationToken ct)
    {
        var enrichment = LeadEnrichment.Empty();
        var score = LeadScore.Default("enrichment not yet run");
        var sw = Stopwatch.GetTimestamp();

        try
        {
            (enrichment, score) = await enricher.EnrichAsync(lead, ct);
            await leadStore.UpdateEnrichmentAsync(agentId, lead.Id, enrichment, score, ct);

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
        var sw = Stopwatch.GetTimestamp();

        try
        {
            await notifier.NotifyAgentAsync(agentId, lead, enrichment, score, ct);

            LeadDiagnostics.LeadsNotificationSent.Add(1);
            logger.LogInformation(
                "[WORKER-030] Agent notification sent for lead {LeadId}. Duration: {DurationMs}ms",
                lead.Id, Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            LeadDiagnostics.LeadsNotificationFailed.Add(1);
            logger.LogError(ex,
                "[WORKER-031] Agent notification failed for lead {LeadId}. Duration: {DurationMs}ms",
                lead.Id, Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }

        LeadDiagnostics.NotificationDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
    }

    private async Task DispatchCmaAsync(
        string agentId, Lead lead, LeadEnrichment enrichment,
        LeadScore score, string correlationId, CancellationToken ct)
    {
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
}
