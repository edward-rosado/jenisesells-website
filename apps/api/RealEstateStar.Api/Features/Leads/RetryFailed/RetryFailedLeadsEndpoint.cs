using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Features.Leads.Services.Enrichment;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Leads.RetryFailed;

public class RetryFailedLeadsEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/agents/{agentId}/leads/retry-failed", Handle);

    internal static async Task<IResult> Handle(
        string agentId,
        ILeadStore leadStore,
        ILeadEnricher enricher,
        ILeadNotifier notifier,
        ILogger<RetryFailedLeadsEndpoint> logger,
        CancellationToken ct)
    {
        logger.LogInformation("[LEAD-060] Retry-failed started for agent {AgentId}", agentId);

        var enrichmentFailed = await leadStore.ListByStatusAsync(agentId, LeadStatus.EnrichmentFailed, ct);
        var notificationFailed = await leadStore.ListByStatusAsync(agentId, LeadStatus.NotificationFailed, ct);

        var candidates = enrichmentFailed.Concat(notificationFailed).ToList();

        int retried = 0;
        int stillFailing = 0;

        foreach (var lead in candidates)
        {
            try
            {
                if (lead.Status == LeadStatus.EnrichmentFailed)
                {
                    var (enrichment, score) = await enricher.EnrichAsync(lead, ct);
                    await leadStore.UpdateEnrichmentAsync(agentId, lead.Id, enrichment, score, ct);
                }
                else if (lead.Status == LeadStatus.NotificationFailed)
                {
                    var enrichment = lead.Enrichment ?? LeadEnrichment.Empty();
                    var score = lead.Score ?? LeadScore.Default("retry");
                    await notifier.NotifyAgentAsync(agentId, lead, enrichment, score, ct);
                }

                logger.LogInformation("[LEAD-061] Retry succeeded for lead {LeadId}", lead.Id);
                retried++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[LEAD-062] Retry failed for lead {LeadId}", lead.Id);
                stillFailing++;
            }
        }

        return Results.Ok(new RetryFailedLeadsResponse(retried, stillFailing));
    }
}
