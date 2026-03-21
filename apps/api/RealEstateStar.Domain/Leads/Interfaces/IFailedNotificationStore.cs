namespace RealEstateStar.Domain.Leads.Interfaces;

public interface IFailedNotificationStore
{
    Task RecordAsync(string agentId, Guid leadId, string lastError, int retryCount, CancellationToken ct);
}
