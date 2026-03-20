namespace RealEstateStar.Api.Features.Leads.Submit;

public interface ILeadNotifier
{
    Task NotifyAgentAsync(string agentId, Lead lead, LeadEnrichment enrichment, LeadScore score, CancellationToken ct);
}
