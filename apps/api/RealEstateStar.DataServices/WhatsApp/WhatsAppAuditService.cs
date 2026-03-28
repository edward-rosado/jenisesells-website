using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.WhatsApp.Interfaces;

namespace RealEstateStar.DataServices.WhatsApp;

public class AzureWhatsAppAuditService(
    TableClient tableClient,
    ILogger<AzureWhatsAppAuditService> logger) : IWhatsAppAuditService
{
    public async Task RecordReceivedAsync(string messageId, string fromPhone,
        string toPhoneNumberId, string body, string messageType, CancellationToken ct)
    {
        await SafeUpsertAsync(new WhatsAppAuditEntry
        {
            PartitionKey = "unknown",
            RowKey = messageId,
            FromPhone = fromPhone,
            ToPhoneNumberId = toPhoneNumberId,
            MessageBody = body,
            MessageType = messageType,
            ReceivedAt = DateTime.UtcNow,
            ProcessingStatus = "received"
        }, ct);
        logger.LogInformation("[WA-023] Audit: received {MessageId}", messageId);
    }

    public async Task UpdateProcessingAsync(string messageId, string agentId,
        CancellationToken ct) =>
        await SafeUpsertAsync(new WhatsAppAuditEntry
        {
            PartitionKey = agentId,
            RowKey = messageId,
            AgentId = agentId,
            ProcessingStatus = "processing"
        }, ct);

    public async Task UpdateCompletedAsync(string messageId, string agentId,
        string intent, string response, CancellationToken ct) =>
        await SafeUpsertAsync(new WhatsAppAuditEntry
        {
            PartitionKey = agentId,
            RowKey = messageId,
            AgentId = agentId,
            IntentClassification = intent,
            ResponseSent = response,
            ProcessingStatus = "completed",
            ProcessedAt = DateTime.UtcNow
        }, ct);

    public async Task UpdateFailedAsync(string messageId, string? agentId,
        string error, CancellationToken ct) =>
        await SafeUpsertAsync(new WhatsAppAuditEntry
        {
            PartitionKey = agentId ?? "unknown",
            RowKey = messageId,
            ProcessingStatus = "failed",
            ErrorDetails = error
        }, ct);

    public async Task UpdatePoisonAsync(string messageId, string error,
        CancellationToken ct) =>
        await SafeUpsertAsync(new WhatsAppAuditEntry
        {
            PartitionKey = "unknown",
            RowKey = messageId,
            ProcessingStatus = "poison",
            ErrorDetails = error
        }, ct);

    private async Task SafeUpsertAsync(WhatsAppAuditEntry entry, CancellationToken ct)
    {
        try
        {
            await tableClient.UpsertEntityAsync(entry, TableUpdateMode.Merge, ct);
        }
        catch (Exception ex)
        {
            // Audit writes are non-blocking — log and continue
            logger.LogWarning(ex, "[WA-024] Audit write failed for {MessageId}", entry.RowKey);
        }
    }
}
