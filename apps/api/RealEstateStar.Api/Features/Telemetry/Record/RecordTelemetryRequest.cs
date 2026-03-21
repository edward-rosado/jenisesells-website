using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.Telemetry.Record;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormEvent { Viewed, Started, Submitted, Succeeded, Failed }

public class RecordTelemetryRequest
{
    [Required] public FormEvent? Event { get; init; }
    [Required, MinLength(1)] public string? AgentId { get; init; }
    public string? ErrorType { get; init; }
}
