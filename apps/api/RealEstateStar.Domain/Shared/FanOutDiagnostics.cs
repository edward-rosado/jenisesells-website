using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Domain.Shared;

public static class FanOutDiagnostics
{
    public const string ServiceName = "RealEstateStar.FanOut";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> Writes = Meter.CreateCounter<long>(
        "fanout.writes", description: "Fan-out document writes");
    public static readonly Counter<long> TierFailures = Meter.CreateCounter<long>(
        "fanout.tier_failures", description: "Fan-out tier write failures");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "fanout.duration_ms", unit: "ms", description: "Fan-out write duration");
}
