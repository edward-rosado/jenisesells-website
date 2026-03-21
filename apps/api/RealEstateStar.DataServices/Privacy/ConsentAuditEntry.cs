using Azure;
using Azure.Data.Tables;

namespace RealEstateStar.DataServices.Privacy;

public class ConsentAuditEntry : ITableEntity
{
    public string PartitionKey { get; set; } = "";  // agentId
    public string RowKey { get; set; } = "";         // Guid.NewGuid()
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public DateTime EventTimestamp { get; set; }
    public Guid LeadId { get; set; }
    public string EmailHash { get; set; } = "";      // SHA-256 full hash
    public bool OptedIn { get; set; }
    public string ConsentText { get; set; } = "";
    public string Channels { get; set; } = "";
    public string Action { get; set; } = "";
    public string Source { get; set; } = "";
    public string HmacSignature { get; set; } = "";
}
