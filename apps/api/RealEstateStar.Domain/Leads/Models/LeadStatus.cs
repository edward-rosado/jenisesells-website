using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Leads.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeadStatus
{
    Received,
    Scored,
    Analyzing,
    Notified,
    Complete,
    ActiveClient,
    UnderContract,
    Closed,
    Inactive
}
