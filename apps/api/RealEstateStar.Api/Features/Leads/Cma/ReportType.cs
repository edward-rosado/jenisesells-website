using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.Leads.Cma;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReportType
{
    Lean,
    Standard,
    Comprehensive
}
