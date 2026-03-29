using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Cma.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReportType
{
    Lean,
    Standard
}
