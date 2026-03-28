using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Activities.Pdf;

public static class PdfDiagnostics
{
    public const string ServiceName = "RealEstateStar.Pdf";

    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    // Counters
    public static readonly Counter<long> PdfSuccess = Meter.CreateCounter<long>(
        "pdf.success", description: "Number of PDFs generated and stored successfully");

    public static readonly Counter<long> PdfFailed = Meter.CreateCounter<long>(
        "pdf.failed", description: "Number of PDF generation or storage failures");

    // Histograms
    public static readonly Histogram<double> GenerationDurationMs = Meter.CreateHistogram<double>(
        "pdf.generation_duration_ms", unit: "ms", description: "Duration of PDF generation via QuestPDF");

    public static readonly Histogram<long> PdfSizeBytes = Meter.CreateHistogram<long>(
        "pdf.size_bytes", unit: "bytes", description: "Size of generated PDF in bytes");

    public static readonly Histogram<double> StorageDurationMs = Meter.CreateHistogram<double>(
        "pdf.storage_duration_ms", unit: "ms", description: "Duration of writing PDF to document storage");
}
