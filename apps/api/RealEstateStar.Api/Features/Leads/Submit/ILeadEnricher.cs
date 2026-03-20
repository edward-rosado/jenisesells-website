namespace RealEstateStar.Api.Features.Leads.Submit;

public interface ILeadEnricher
{
    Task<(LeadEnrichment Enrichment, LeadScore Score)> EnrichAsync(Lead lead, CancellationToken ct);
}
