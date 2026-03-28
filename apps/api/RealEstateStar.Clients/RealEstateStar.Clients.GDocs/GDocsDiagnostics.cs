using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Clients.GDocs;

public static class GDocsDiagnostics
{
    public const string ServiceName = "RealEstateStar.GDocs";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> Operations = Meter.CreateCounter<long>(
        "gdocs.operations", description: "Docs API operations");
    public static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "gdocs.failed", description: "Docs API operation failures");
    public static readonly Counter<long> TokenMissing = Meter.CreateCounter<long>(
        "gdocs.token_missing", description: "Docs calls skipped due to missing token");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "gdocs.duration_ms", unit: "ms", description: "Docs API call duration");
}
