namespace RealEstateStar.Api.Features.Leads.Services;

using RealEstateStar.Api.Services.Storage;

public sealed class DeletionAuditLog(IFileStorageProvider storage) : IDeletionAuditLog
{
    public Task RecordInitiationAsync(string agentId, Guid leadId, string email, CancellationToken ct) =>
        storage.AppendRowAsync(
            LeadPaths.DeletionAuditLogSheet(agentId),
            [DateTime.UtcNow.ToString("o"), agentId, leadId.ToString(), HashEmail(email), "initiated"],
            ct);

    public Task RecordCompletionAsync(string agentId, Guid leadId, CancellationToken ct) =>
        storage.AppendRowAsync(
            LeadPaths.DeletionAuditLogSheet(agentId),
            [DateTime.UtcNow.ToString("o"), agentId, leadId.ToString(), "[REDACTED]", "completed"],
            ct);

    private static string HashEmail(string email)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}
