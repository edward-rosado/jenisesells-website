namespace RealEstateStar.DataServices.Privacy;

using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

public sealed class DeletionAuditLog(ISheetStorageProvider storage, ILogger<DeletionAuditLog> logger) : IDeletionAuditLog
{
    public Task RecordInitiationAsync(string agentId, Guid leadId, string email, CancellationToken ct)
    {
        logger.LogInformation("[DELETE-001] Deletion initiated for lead {LeadId}, agent {AgentId}", leadId, agentId);
        return storage.AppendRowAsync(
            LeadPaths.DeletionAuditLogSheet(agentId),
            [DateTime.UtcNow.ToString("o"), agentId, leadId.ToString(), HashEmail(email), "initiated"],
            ct);
    }

    public Task RecordCompletionAsync(string agentId, Guid leadId, CancellationToken ct)
    {
        logger.LogInformation("[DELETE-002] Deletion completed for lead {LeadId}, agent {AgentId}", leadId, agentId);
        return storage.AppendRowAsync(
            LeadPaths.DeletionAuditLogSheet(agentId),
            [DateTime.UtcNow.ToString("o"), agentId, leadId.ToString(), "[REDACTED]", "completed"],
            ct);
    }

    private static string HashEmail(string email)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}
