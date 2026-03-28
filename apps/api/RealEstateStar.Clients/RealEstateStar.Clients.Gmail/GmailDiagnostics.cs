using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Clients.Gmail;

public static class GmailDiagnostics
{
    public const string ServiceName = "RealEstateStar.Gmail";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> Sent = Meter.CreateCounter<long>(
        "gmail.sent", description: "Emails sent via Gmail API");
    public static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "gmail.failed", description: "Gmail API send failures");
    public static readonly Counter<long> TokenMissing = Meter.CreateCounter<long>(
        "gmail.token_missing", description: "Gmail calls skipped due to missing token");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "gmail.duration_ms", unit: "ms", description: "Gmail API call duration");
}
