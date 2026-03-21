using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Api.Features.WhatsApp;

public static class WhatsAppDiagnostics
{
    public const string ServiceName = "RealEstateStar.WhatsApp";

    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    // Counters
    public static readonly Counter<long> MessagesReceived = Meter.CreateCounter<long>("whatsapp.messages.received");
    public static readonly Counter<long> MessagesSentTemplate = Meter.CreateCounter<long>("whatsapp.messages.sent.template");
    public static readonly Counter<long> MessagesSentFreeform = Meter.CreateCounter<long>("whatsapp.messages.sent.freeform");
    public static readonly Counter<long> MessagesDuplicate = Meter.CreateCounter<long>("whatsapp.messages.duplicate");
    public static readonly Counter<long> WebhookSignatureFail = Meter.CreateCounter<long>("whatsapp.webhook.signature_fail");
    public static readonly Counter<long> AgentNotFound = Meter.CreateCounter<long>("whatsapp.agent.not_found");
    public static readonly Counter<long> IntentClassified = Meter.CreateCounter<long>("whatsapp.intent.classified");
    public static readonly Counter<long> QueueEnqueued = Meter.CreateCounter<long>("whatsapp.queue.enqueued");
    public static readonly Counter<long> QueueProcessed = Meter.CreateCounter<long>("whatsapp.queue.processed");
    public static readonly Counter<long> QueueFailed = Meter.CreateCounter<long>("whatsapp.queue.failed");
    public static readonly Counter<long> QueuePoison = Meter.CreateCounter<long>("whatsapp.queue.poison");
    public static readonly Counter<long> AuditWritten = Meter.CreateCounter<long>("whatsapp.audit.written");
    public static readonly Counter<long> AuditFailed = Meter.CreateCounter<long>("whatsapp.audit.failed");

    // Histograms
    public static readonly Histogram<double> WebhookProcessingMs = Meter.CreateHistogram<double>("whatsapp.webhook.processing_ms");
    public static readonly Histogram<double> QueueProcessingMs = Meter.CreateHistogram<double>("whatsapp.queue.processing_ms");
    public static readonly Histogram<double> SendLatencyMs = Meter.CreateHistogram<double>("whatsapp.send.latency_ms");
    public static readonly Histogram<double> QueueWaitMs = Meter.CreateHistogram<double>("whatsapp.queue.wait_ms");
}
