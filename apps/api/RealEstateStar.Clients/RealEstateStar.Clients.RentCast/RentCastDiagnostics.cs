using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Clients.RentCast;

public static class RentCastDiagnostics
{
    public const string ServiceName = "RealEstateStar.RentCast";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> CallsTotal = Meter.CreateCounter<long>(
        "rentcast.calls_total", description: "Total RentCast API calls");
    public static readonly Counter<long> CallsFailed = Meter.CreateCounter<long>(
        "rentcast.calls_failed", description: "Failed RentCast API calls");
    public static readonly Histogram<double> CompsReturned = Meter.CreateHistogram<double>(
        "rentcast.comps_returned", description: "Number of comparable properties returned");
    public static readonly Histogram<double> CallDuration = Meter.CreateHistogram<double>(
        "rentcast.call_duration_ms", unit: "ms", description: "RentCast API call duration");
}
