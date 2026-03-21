using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Privacy;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConsentAction { OptIn, OptOut, Resubscribe, DataAccessRequest, DataExportRequest }
