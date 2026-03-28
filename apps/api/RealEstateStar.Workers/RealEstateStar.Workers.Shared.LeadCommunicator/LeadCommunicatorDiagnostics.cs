using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Workers.Shared.LeadCommunicator;

public static class LeadCommunicatorDiagnostics
{
    public const string ServiceName = "RealEstateStar.LeadCommunicator";

    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    // Counters
    public static readonly Counter<long> DraftFallback = Meter.CreateCounter<long>(
        "email.draft_fallback", description: "Times Claude failed and the template-only fallback was used");

    public static readonly Counter<long> SendSuccess = Meter.CreateCounter<long>(
        "email.send_success", description: "Lead emails sent successfully");

    public static readonly Counter<long> SendFailed = Meter.CreateCounter<long>(
        "email.send_failed", description: "Lead emails that failed to send");

    // Histograms
    public static readonly Histogram<double> DraftDurationMs = Meter.CreateHistogram<double>(
        "email.draft_duration_ms", unit: "ms", description: "Duration of email draft step (including Claude call)");

    public static readonly Histogram<double> SendDurationMs = Meter.CreateHistogram<double>(
        "email.send_duration_ms", unit: "ms", description: "Duration of Gmail send operation");
}
