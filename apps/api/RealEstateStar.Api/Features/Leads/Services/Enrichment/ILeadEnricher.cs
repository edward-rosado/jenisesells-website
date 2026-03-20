namespace RealEstateStar.Api.Features.Leads.Services.Enrichment;

public interface ILeadEnricher
{
    Task<(LeadEnrichment Enrichment, LeadScore Score)> EnrichAsync(Lead lead, CancellationToken ct);
}
