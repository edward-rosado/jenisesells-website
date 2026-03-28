namespace RealEstateStar.Domain.Privacy.Interfaces;

public interface IDeletionAuditDataService
{
    Task RecordInitiationAsync(string agentId, Guid leadId, string email, CancellationToken ct);
    Task RecordCompletionAsync(string agentId, Guid leadId, CancellationToken ct);
}
