namespace RealEstateStar.Api.Features.Leads.Services;

public interface ILeadDataDeletion
{
    Task<string> InitiateDeletionRequestAsync(string agentId, string email, CancellationToken ct);
    Task<DeleteResult> ExecuteDeletionAsync(string agentId, string email, string token, string reason, CancellationToken ct);
}
