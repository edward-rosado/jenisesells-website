using RealEstateStar.Domain.Leads.Models;
namespace RealEstateStar.Domain.Leads.Interfaces;

public interface ILeadDeletionDataService
{
    Task<string> InitiateDeletionRequestAsync(string agentId, string email, CancellationToken ct);
    Task<DeleteResult> ExecuteDeletionAsync(string agentId, string email, string token, string reason, CancellationToken ct);
}
