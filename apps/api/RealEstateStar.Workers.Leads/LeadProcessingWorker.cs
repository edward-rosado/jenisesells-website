using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Shared.Context;

namespace RealEstateStar.Workers.Leads;

/// <summary>
/// Background pipeline worker that processes leads from the <see cref="LeadProcessingChannel"/>.
/// Inherits checkpoint/resume, exponential backoff retry, and dead-letter handling from
/// <see cref="PipelineWorker{TRequest,TContext}"/>. Each step is idempotent via RunStepAsync
/// and sub-step tracking — no file-based checkpoints needed.
/// </summary>
public sealed class LeadProcessingWorker(
    LeadProcessingChannel channel,
    ILeadStore leadStore,
    ILeadEnricher enricher,
    ILeadNotifier notifier,
    IFailedNotificationStore failedNotificationStore,
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

        await RunStepAsync(ctx, LeadPipelineContext.StepEnrich, () => EnrichAsync(ctx, ct), ct);
        await RunStepAsync(ctx, LeadPipelineContext.StepDraftEmail, () => DraftEmailAsync(ctx, ct), ct);
        await RunStepAsync(ctx, LeadPipelineContext.StepNotify, () => NotifyAsync(ctx, ct), ct);

        // Mark lead complete
        ctx.Request.Status = LeadStatus.Complete;
        try { await leadStore.UpdateStatusAsync(ctx.Request, LeadStatus.Complete, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "[LeadWorker] Failed to update status to Complete for lead {LeadId}", ctx.Request.Id); }

        LeadDiagnostics.TotalPipelineDuration.Record(ctx.PipelineDurationMs ?? 0);
    }

    private async Task EnrichAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        if (!ctx.HasCompletedSubStep(LeadPipelineContext.StepEnrich, "call-enricher"))
        {
            var (enrichment, score) = await enricher.EnrichAsync(ctx.Request, ct);
            ctx.Enrichment = enrichment;
            ctx.Score = score;
            ctx.MarkSubStepCompleted(LeadPipelineContext.StepEnrich, "call-enricher");
        }

        if (!ctx.HasCompletedSubStep(LeadPipelineContext.StepEnrich, "save"))
        {
            await leadStore.UpdateEnrichmentAsync(ctx.Request, ctx.Enrichment!, ctx.Score!, ct);
            ctx.Request.Status = LeadStatus.Enriched;
            await leadStore.UpdateStatusAsync(ctx.Request, LeadStatus.Enriched, ct);
            LeadDiagnostics.LeadsEnriched.Add(1);
            ctx.MarkSubStepCompleted(LeadPipelineContext.StepEnrich, "save");
        }
    }

    private async Task DraftEmailAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        var subject = notifier.BuildSubject(ctx.Request, ctx.Enrichment!, ctx.Score!);
        var body = notifier.BuildBody(ctx.Request, ctx.Enrichment!, ctx.Score!);
        ctx.EmailDraftSubject = subject;
        ctx.EmailDraftBody = body;

        ctx.Request.Status = LeadStatus.EmailDrafted;
        try { await leadStore.UpdateStatusAsync(ctx.Request, LeadStatus.EmailDrafted, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "[LeadWorker] Failed to update status to EmailDrafted for lead {LeadId}", ctx.Request.Id); }
    }

    private async Task NotifyAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        await notifier.NotifyAgentAsync(ctx.AgentId, ctx.Request, ctx.Enrichment!, ctx.Score!, ct);

        ctx.Request.Status = LeadStatus.Notified;
        try { await leadStore.UpdateStatusAsync(ctx.Request, LeadStatus.Notified, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "[LeadWorker] Failed to update status to Notified for lead {LeadId}", ctx.Request.Id); }

        LeadDiagnostics.LeadsNotificationSent.Add(1);
    }

    protected override async Task OnPermanentFailureAsync(LeadPipelineContext context, Exception lastException, CancellationToken ct)
    {
        LeadDiagnostics.NotificationPermanentlyFailed.Add(1);
        await failedNotificationStore.RecordAsync(
            context.AgentId, context.Request.Id,
            lastException.Message, context.TotalFailures, ct);
    }
}
