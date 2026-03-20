namespace RealEstateStar.Api.Features.Leads.Services;

using RealEstateStar.Api.Services.Storage;

public sealed class DeletionAuditLog(IFileStorageProvider storage) : IDeletionAuditLog
{
    public Task RecordInitiationAsync(string agentId, Guid leadId, string email, CancellationToken ct) =>
        storage.AppendRowAsync(
            LeadPaths.DeletionAuditLogSheet(agentId),
            [DateTime.UtcNow.ToString("o"), agentId, leadId.ToString(), email, "initiated"],
            ct);

    public Task RecordCompletionAsync(string agentId, Guid leadId, CancellationToken ct) =>
        storage.AppendRowAsync(
            LeadPaths.DeletionAuditLogSheet(agentId),
            [DateTime.UtcNow.ToString("o"), agentId, leadId.ToString(), "[REDACTED]", "completed"],
            ct);
}
