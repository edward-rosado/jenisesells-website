using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Clients.GooglePlaces;

public static class GooglePlacesDiagnostics
{
    public const string ServiceName = "RealEstateStar.GooglePlaces";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> CallsTotal = Meter.CreateCounter<long>("google_places.calls_total");
    public static readonly Counter<long> CallsSucceeded = Meter.CreateCounter<long>("google_places.calls_succeeded");
    public static readonly Counter<long> CallsFailed = Meter.CreateCounter<long>("google_places.calls_failed");
    public static readonly Counter<long> ReviewsFetched = Meter.CreateCounter<long>("google_places.reviews_fetched");

    public static readonly Histogram<double> CallDuration = Meter.CreateHistogram<double>("google_places.call_duration_ms");
}
