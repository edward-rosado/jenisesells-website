using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Domain.Cma;

public static class CmaDiagnostics
{
    public const string ServiceName = "RealEstateStar.Cma";

    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    // Counters
    public static readonly Counter<long> CmaGenerated = Meter.CreateCounter<long>(
        "cma.generated", description: "Number of CMA reports generated");

    public static readonly Counter<long> CmaFailed = Meter.CreateCounter<long>(
        "cma.failed", description: "Number of CMA reports that failed");

    // Histograms
    public static readonly Histogram<double> CompsDuration = Meter.CreateHistogram<double>(
        "cma.comps_duration_ms", unit: "ms", description: "Duration of comparable sales fetch");

    public static readonly Histogram<double> AnalysisDuration = Meter.CreateHistogram<double>(
        "cma.analysis_duration_ms", unit: "ms", description: "Duration of Claude CMA analysis");

    public static readonly Histogram<double> PdfDuration = Meter.CreateHistogram<double>(
        "cma.pdf_duration_ms", unit: "ms", description: "Duration of QuestPDF generation");

    public static readonly Histogram<double> DriveDuration = Meter.CreateHistogram<double>(
        "cma.drive_duration_ms", unit: "ms", description: "Duration of Google Drive upload");

    public static readonly Histogram<double> EmailDuration = Meter.CreateHistogram<double>(
        "cma.email_duration_ms", unit: "ms", description: "Duration of CMA email delivery");

    public static readonly Histogram<double> TotalDuration = Meter.CreateHistogram<double>(
        "cma.total_duration_ms", unit: "ms", description: "Total CMA pipeline duration");

    public static readonly Histogram<long> CompsFound = Meter.CreateHistogram<long>(
        "cma.comps_found", description: "Number of comparable sales found per CMA");
}
