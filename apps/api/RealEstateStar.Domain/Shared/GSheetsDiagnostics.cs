using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Domain.Shared;

public static class GSheetsDiagnostics
{
    public const string ServiceName = "RealEstateStar.GSheets";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> Operations = Meter.CreateCounter<long>(
        "gsheets.operations", description: "Sheets API operations");
    public static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "gsheets.failed", description: "Sheets API operation failures");
    public static readonly Counter<long> TokenMissing = Meter.CreateCounter<long>(
        "gsheets.token_missing", description: "Sheets calls skipped due to missing token");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "gsheets.duration_ms", unit: "ms", description: "Sheets API call duration");
}
