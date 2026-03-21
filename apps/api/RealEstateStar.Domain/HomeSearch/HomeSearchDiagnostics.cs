using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Domain.HomeSearch;

public static class HomeSearchDiagnostics
{
    public const string ServiceName = "RealEstateStar.HomeSearch";

    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    // Counters
    public static readonly Counter<long> SearchCompleted = Meter.CreateCounter<long>(
        "home_search.completed", description: "Number of home searches completed");

    public static readonly Counter<long> SearchFailed = Meter.CreateCounter<long>(
        "home_search.failed", description: "Number of home searches that failed");

    // Histograms
    public static readonly Histogram<double> FetchDuration = Meter.CreateHistogram<double>(
        "home_search.fetch_duration_ms", unit: "ms", description: "Duration of listing fetch");

    public static readonly Histogram<double> CurationDuration = Meter.CreateHistogram<double>(
        "home_search.curation_duration_ms", unit: "ms", description: "Duration of Claude listing curation");

    public static readonly Histogram<double> DriveDuration = Meter.CreateHistogram<double>(
        "home_search.drive_duration_ms", unit: "ms", description: "Duration of Google Drive save");

    public static readonly Histogram<double> EmailDuration = Meter.CreateHistogram<double>(
        "home_search.email_duration_ms", unit: "ms", description: "Duration of buyer email delivery");

    public static readonly Histogram<double> TotalDuration = Meter.CreateHistogram<double>(
        "home_search.total_duration_ms", unit: "ms", description: "Total home search pipeline duration");

    public static readonly Histogram<long> ListingsFound = Meter.CreateHistogram<long>(
        "home_search.listings_found", description: "Number of listings found per search");
}
