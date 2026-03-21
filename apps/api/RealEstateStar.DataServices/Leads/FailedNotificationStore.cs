using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads.Interfaces;

namespace RealEstateStar.DataServices.Leads;

public class FailedNotificationStore(
    TableClient tableClient,
    ILogger<FailedNotificationStore> logger) : IFailedNotificationStore
{
    public async Task RecordAsync(string agentId, Guid leadId, string lastError, int retryCount, CancellationToken ct)
    {
        try
        {
            var entry = new FailedNotificationEntry
            {
                PartitionKey = agentId,
                RowKey = Guid.NewGuid().ToString(),
                LeadId = leadId,
                LastError = lastError,
                RetryCount = retryCount,
                FailedAt = DateTime.UtcNow,
            };

            await tableClient.UpsertEntityAsync(entry, TableUpdateMode.Merge, ct);
            logger.LogInformation("[NOTIFY-010] Failed notification recorded for lead {LeadId}. RetryCount: {RetryCount}", leadId, retryCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[NOTIFY-011] Dead letter write failed for lead {LeadId}", leadId);
        }
    }
}
