using RealEstateStar.Domain.Leads.Models;
namespace RealEstateStar.Domain.Leads.Interfaces;

public interface ILeadNotifier
{
    Task NotifyAgentAsync(string agentId, Lead lead, LeadEnrichment enrichment, LeadScore score, CancellationToken ct);
}
