using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.Leads;

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
