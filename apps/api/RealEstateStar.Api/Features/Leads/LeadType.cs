using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.Leads;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeadType
{
    Buyer,
    Seller,
    Both
}
