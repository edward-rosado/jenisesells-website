using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Clients.Zillow;

public static class ZillowDiagnostics
{
    public const string ServiceName = "RealEstateStar.Zillow";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> CallsTotal = Meter.CreateCounter<long>("zillow.calls_total");
    public static readonly Counter<long> CallsSucceeded = Meter.CreateCounter<long>("zillow.calls_succeeded");
    public static readonly Counter<long> CallsFailed = Meter.CreateCounter<long>("zillow.calls_failed");
    public static readonly Counter<long> ReviewsFetched = Meter.CreateCounter<long>("zillow.reviews_fetched");

    public static readonly Histogram<double> CallDuration = Meter.CreateHistogram<double>("zillow.call_duration_ms");
}
