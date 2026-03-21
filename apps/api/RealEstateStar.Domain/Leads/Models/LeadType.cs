using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Leads.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeadType
{
    Buyer,
    Seller,
    Both
}
