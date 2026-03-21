using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Leads.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeadStatus
{
    Received,
    Enriching,
    Enriched,
    EnrichmentFailed,
    Notified,
    NotificationFailed,
    CmaComplete,
    SearchComplete,
    Complete,
    ActiveClient,
    UnderContract,
    Closed,
    Inactive
}
