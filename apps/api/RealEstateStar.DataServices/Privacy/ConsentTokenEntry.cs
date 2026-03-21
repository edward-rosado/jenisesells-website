using Azure;
using Azure.Data.Tables;

namespace RealEstateStar.DataServices.Privacy;

public class ConsentTokenEntry : ITableEntity
{
    public string PartitionKey { get; set; } = "";  // agentId
    public string RowKey { get; set; } = "";         // SHA-256 hash of consent token
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public Guid LeadId { get; set; }
    public string EmailHash { get; set; } = "";
}
