using Azure;
using Azure.Data.Tables;

namespace RealEstateStar.DataServices.Leads;

public class FailedNotificationEntry : ITableEntity
{
    public string PartitionKey { get; set; } = "";  // agentId
    public string RowKey { get; set; } = "";         // Guid.NewGuid()
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public Guid LeadId { get; set; }
    public string LastError { get; set; } = "";
    public int RetryCount { get; set; }
    public DateTime FailedAt { get; set; }
}
