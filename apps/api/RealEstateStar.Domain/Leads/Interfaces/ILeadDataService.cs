using RealEstateStar.Domain.Leads.Models;
namespace RealEstateStar.Domain.Leads.Interfaces;

public interface ILeadDataService
{
    Task SaveAsync(Lead lead, CancellationToken ct);
    Task UpdateScoreAsync(Lead lead, LeadScore score, CancellationToken ct);
    Task UpdateHomeSearchIdAsync(string agentId, Guid leadId, string homeSearchId, CancellationToken ct);
    Task UpdateStatusAsync(Lead lead, LeadStatus status, CancellationToken ct);
    Task UpdateMarketingOptInAsync(string agentId, Guid leadId, bool optedIn, CancellationToken ct);
    Task<Lead?> GetAsync(string agentId, Guid leadId, CancellationToken ct);
    Task<Lead?> GetByNameAsync(string agentId, string leadName, CancellationToken ct);
    Task<Lead?> GetByEmailAsync(string agentId, string email, CancellationToken ct);
    Task<List<Lead>> ListByStatusAsync(string agentId, LeadStatus status, CancellationToken ct);
    Task DeleteAsync(string agentId, Guid leadId, CancellationToken ct);
}
