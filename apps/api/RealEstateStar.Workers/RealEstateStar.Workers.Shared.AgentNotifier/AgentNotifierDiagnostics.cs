using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Workers.Shared.AgentNotifier;

public static class AgentNotifierDiagnostics
{
    public const string ServiceName = "RealEstateStar.AgentNotifier";

    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    // Counters
    public static readonly Counter<long> WhatsAppSuccess = Meter.CreateCounter<long>(
        "agent_notify.whatsapp_success", description: "Agent WhatsApp notifications sent successfully");

    public static readonly Counter<long> WhatsAppFailed = Meter.CreateCounter<long>(
        "agent_notify.whatsapp_failed", description: "Agent WhatsApp notifications that failed");

    public static readonly Counter<long> EmailFallback = Meter.CreateCounter<long>(
        "agent_notify.email_fallback", description: "Times WhatsApp failed and email fallback was used");
}
