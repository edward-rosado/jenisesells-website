using RealEstateStar.Domain.Leads.Interfaces;

namespace RealEstateStar.DataServices.Leads;

/// <summary>
/// Null-object implementation used when Azure Storage is not configured (e.g., local development).
/// Silently discards failed notification records rather than crashing the pipeline.
/// </summary>
public sealed class NullFailedNotificationStore : IFailedNotificationStore
{
    public Task RecordAsync(string agentId, Guid leadId, string lastError, int retryCount, CancellationToken ct) =>
        Task.CompletedTask;
}
