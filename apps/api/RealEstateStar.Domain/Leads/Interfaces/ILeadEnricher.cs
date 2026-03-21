using RealEstateStar.Domain.Leads.Models;
namespace RealEstateStar.Domain.Leads.Interfaces;

public interface ILeadEnricher
{
    Task<(LeadEnrichment Enrichment, LeadScore Score)> EnrichAsync(Lead lead, CancellationToken ct);
}
