using Azure;
using Azure.Data.Tables;

namespace RealEstateStar.DataServices.WhatsApp;

public class WhatsAppAuditEntry : ITableEntity
{
    public string PartitionKey { get; set; } = "unknown"; // agentId or "unknown"
    public string RowKey { get; set; } = "";               // wamid (message ID)
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string FromPhone { get; set; } = "";
    public string ToPhoneNumberId { get; set; } = "";
    public string MessageBody { get; set; } = "";
    public string MessageType { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public string? AgentId { get; set; }
    public string? LeadName { get; set; }
    public string? IntentClassification { get; set; }
    public string? ResponseSent { get; set; }
    public string ProcessingStatus { get; set; } = "received";
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorDetails { get; set; }
}
