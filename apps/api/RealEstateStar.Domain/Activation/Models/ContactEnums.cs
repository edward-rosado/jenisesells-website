using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Activation.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentType
{
    ListingAgreement,
    BuyerAgreement,
    PurchaseContract,
    Disclosure,
    ClosingStatement,
    Cma,
    Inspection,
    Appraisal,
    Other
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContactRole
{
    Buyer,
    Seller,
    Both,
    Unknown
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PipelineStage
{
    Lead,
    ActiveClient,
    UnderContract,
    Closed
}
