using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Shared.Context;

namespace RealEstateStar.Workers.Leads;

// TODO: Pipeline redesign — ILeadEnricher, ILeadNotifier, IFailedNotificationStore removed in Phase 1.5.
// LeadProcessingWorker is a stub pending full pipeline redesign in Phase 2/3/4.
/// <summary>
/// Background pipeline worker that processes leads from the <see cref="LeadProcessingChannel"/>.
/// Inherits checkpoint/resume, exponential backoff retry, and dead-letter handling from
/// <see cref="PipelineWorker{TRequest,TContext}"/>. Each step is idempotent via RunStepAsync
/// and sub-step tracking — no file-based checkpoints needed.
/// </summary>
public sealed class LeadProcessingWorker(
    LeadProcessingChannel channel,
    ILeadStore leadStore,
    BackgroundServiceHealthTracker healthTracker,
    ILogger<LeadProcessingWorker> logger,
    IConfiguration configuration)
    : PipelineWorker<LeadProcessingRequest, LeadPipelineContext>(
        channel, healthTracker, logger,
        configuration.GetSection("Pipeline:Lead:Retry").Get<PipelineRetryOptions>())
{
    protected override string WorkerName => "LeadWorker";

    protected override LeadPipelineContext CreateContext(LeadProcessingRequest request) => new()
    {
        Request = request.Lead,
        AgentId = request.AgentId,
        CorrelationId = request.CorrelationId,
    };

    protected override async Task ProcessAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        using var activity = LeadDiagnostics.ActivitySource.StartActivity("lead.process");
        activity?.SetTag("lead.id", ctx.Request.Id.ToString());
        activity?.SetTag("lead.agent_id", ctx.AgentId);
        activity?.SetTag("correlation.id", ctx.CorrelationId);

        // TODO: Pipeline redesign — steps will be re-implemented in Phase 2/3/4
        await RunStepAsync(ctx, LeadPipelineContext.StepNotify, () => NotifyAsync(ctx, ct), ct);

        // Mark lead complete
        ctx.Request.Status = LeadStatus.Complete;
        try { await leadStore.UpdateStatusAsync(ctx.Request, LeadStatus.Complete, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "[LeadWorker] Failed to update status to Complete for lead {LeadId}", ctx.Request.Id); }

        LeadDiagnostics.TotalPipelineDuration.Record(ctx.PipelineDurationMs ?? 0);
    }

    private async Task NotifyAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        // TODO: Pipeline redesign — notification re-implemented in Phase 2/3/4
        ctx.Request.Status = LeadStatus.Notified;
        try { await leadStore.UpdateStatusAsync(ctx.Request, LeadStatus.Notified, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "[LeadWorker] Failed to update status to Notified for lead {LeadId}", ctx.Request.Id); }

        LeadDiagnostics.LeadsNotificationSent.Add(1);
    }

    protected override Task OnPermanentFailureAsync(LeadPipelineContext context, Exception lastException, CancellationToken ct)
    {
        LeadDiagnostics.NotificationPermanentlyFailed.Add(1);
        // TODO: Pipeline redesign — IFailedNotificationStore removed in Phase 1.5; dead-letter handling replaced in Phase 2/3/4
        return Task.CompletedTask;
    }
}
