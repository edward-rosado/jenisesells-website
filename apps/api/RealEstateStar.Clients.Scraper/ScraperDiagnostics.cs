using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Clients.Scraper;

public static class ScraperDiagnostics
{
    public const string ServiceName = "RealEstateStar.Scraper";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    // Counters
    public static readonly Counter<long> CallsTotal = Meter.CreateCounter<long>("scraper.calls_total");
    public static readonly Counter<long> CallsSucceeded = Meter.CreateCounter<long>("scraper.calls_succeeded");
    public static readonly Counter<long> CallsFailed = Meter.CreateCounter<long>("scraper.calls_failed");
    public static readonly Counter<long> CallsRateLimited = Meter.CreateCounter<long>("scraper.calls_rate_limited");
    public static readonly Counter<long> CreditsUsed = Meter.CreateCounter<long>("scraper.credits_used");

    // Histograms
    public static readonly Histogram<double> CallDuration = Meter.CreateHistogram<double>("scraper.call_duration_ms");
}
