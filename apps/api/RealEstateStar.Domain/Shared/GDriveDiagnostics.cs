using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Domain.Shared;

public static class GDriveDiagnostics
{
    public const string ServiceName = "RealEstateStar.GDrive";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> Operations = Meter.CreateCounter<long>(
        "gdrive.operations", description: "Drive API operations");
    public static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "gdrive.failed", description: "Drive API operation failures");
    public static readonly Counter<long> TokenMissing = Meter.CreateCounter<long>(
        "gdrive.token_missing", description: "Drive calls skipped due to missing token");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "gdrive.duration_ms", unit: "ms", description: "Drive API call duration");
}
