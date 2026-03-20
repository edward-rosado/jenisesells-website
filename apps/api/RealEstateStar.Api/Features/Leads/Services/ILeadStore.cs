namespace RealEstateStar.Api.Features.Leads.Services;

public interface ILeadStore
{
    Task SaveAsync(Lead lead, CancellationToken ct);
    Task UpdateEnrichmentAsync(string agentId, Guid leadId, LeadEnrichment enrichment, LeadScore score, CancellationToken ct);
    Task UpdateCmaJobIdAsync(string agentId, Guid leadId, string cmaJobId, CancellationToken ct);
    Task UpdateHomeSearchIdAsync(string agentId, Guid leadId, string homeSearchId, CancellationToken ct);
    Task UpdateStatusAsync(string agentId, Guid leadId, LeadStatus status, CancellationToken ct);
    Task<Lead?> GetAsync(string agentId, Guid leadId, CancellationToken ct);
    Task<Lead?> GetByNameAsync(string agentId, string leadName, CancellationToken ct);
    Task<List<Lead>> ListByStatusAsync(string agentId, LeadStatus status, CancellationToken ct);
    Task DeleteAsync(string agentId, Guid leadId, CancellationToken ct);
}
