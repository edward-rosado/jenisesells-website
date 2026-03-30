using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Workers.Lead.Orchestrator;

public static class OrchestratorDiagnostics
{
    public const string ServiceName = "RealEstateStar.Orchestrator";

    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    // Counters — lead lifecycle
    public static readonly Counter<long> LeadsProcessed = Meter.CreateCounter<long>(
        "orchestrator.leads_processed", description: "Total leads entered the orchestration pipeline");

    public static readonly Counter<long> LeadsCompleted = Meter.CreateCounter<long>(
        "orchestrator.leads_completed", description: "Leads that completed all pipeline steps successfully");

    public static readonly Counter<long> LeadsPartial = Meter.CreateCounter<long>(
        "orchestrator.leads_partial", description: "Leads that completed with one or more worker timeouts");

    public static readonly Counter<long> LeadsFailed = Meter.CreateCounter<long>(
        "orchestrator.leads_failed", description: "Leads that failed with an unhandled exception");

    // Counters — worker dispatch / completion
    public static readonly Counter<long> WorkerDispatches = Meter.CreateCounter<long>(
        "orchestrator.worker_dispatches", description: "Total worker dispatch attempts (CMA + HomeSearch + PDF)");

    public static readonly Counter<long> WorkerCompletions = Meter.CreateCounter<long>(
        "orchestrator.worker_completions", description: "Workers that returned a result before timeout");

    public static readonly Counter<long> WorkerTimeouts = Meter.CreateCounter<long>(
        "orchestrator.worker_timeouts", description: "Workers that did not complete within the timeout window");

    // Counters — email delivery
    public static readonly Counter<long> EmailSent = Meter.CreateCounter<long>(
        "orchestrator.email_sent", description: "Lead notification emails sent successfully");

    public static readonly Counter<long> EmailFailed = Meter.CreateCounter<long>(
        "orchestrator.email_failed", description: "Lead notification emails that failed to send");

    // Counters — WhatsApp delivery
    public static readonly Counter<long> WhatsAppSent = Meter.CreateCounter<long>(
        "orchestrator.whatsapp_sent", description: "Agent WhatsApp notifications sent successfully");

    public static readonly Counter<long> WhatsAppFailed = Meter.CreateCounter<long>(
        "orchestrator.whatsapp_failed", description: "Agent WhatsApp notifications that failed");

    // Counters — checkpoint resume
    public static readonly Counter<long> CheckpointsWritten = Meter.CreateCounter<long>(
        "orchestrator.checkpoints_written", description: "Pipeline checkpoints written to storage");

    public static readonly Counter<long> CheckpointsResumed = Meter.CreateCounter<long>(
        "orchestrator.checkpoints_resumed", description: "Pipeline steps skipped because a checkpoint already exists");

    // Counters — Claude token usage (orchestrator-level)
    public static readonly Counter<long> ClaudeTokensInput = Meter.CreateCounter<long>(
        "orchestrator.claude_tokens.input", description: "Claude input tokens consumed by orchestrator steps");

    public static readonly Counter<long> ClaudeTokensOutput = Meter.CreateCounter<long>(
        "orchestrator.claude_tokens.output", description: "Claude output tokens consumed by orchestrator steps");

    // Histograms — per-phase durations
    public static readonly Histogram<double> TotalDurationMs = Meter.CreateHistogram<double>(
        "orchestrator.total_duration_ms", unit: "ms", description: "End-to-end orchestration duration per lead");

    public static readonly Histogram<double> ScoreDurationMs = Meter.CreateHistogram<double>(
        "orchestrator.score_duration_ms", unit: "ms", description: "Duration of lead scoring step");

    public static readonly Histogram<double> CollectDurationMs = Meter.CreateHistogram<double>(
        "orchestrator.collect_duration_ms", unit: "ms", description: "Duration waiting for CMA + HomeSearch workers to complete");

    public static readonly Histogram<double> PdfDurationMs = Meter.CreateHistogram<double>(
        "orchestrator.pdf_duration_ms", unit: "ms", description: "Duration waiting for PDF worker to complete");

    public static readonly Histogram<double> EmailDraftDurationMs = Meter.CreateHistogram<double>(
        "orchestrator.email_draft_duration_ms", unit: "ms", description: "Duration of email drafting step");

    public static readonly Histogram<double> EmailSendDurationMs = Meter.CreateHistogram<double>(
        "orchestrator.email_send_duration_ms", unit: "ms", description: "Duration of email send step");

    public static readonly Histogram<double> WhatsAppSendDurationMs = Meter.CreateHistogram<double>(
        "orchestrator.whatsapp_send_duration_ms", unit: "ms", description: "Duration of agent WhatsApp notification step");
}
