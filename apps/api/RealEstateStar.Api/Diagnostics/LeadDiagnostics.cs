using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Api.Diagnostics;

public static class LeadDiagnostics
{
    public const string ServiceName = "RealEstateStar.Leads";

    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    // Counters
    public static readonly Counter<long> LeadsReceived = Meter.CreateCounter<long>(
        "leads.received", description: "Number of leads received");

    public static readonly Counter<long> LeadsEnriched = Meter.CreateCounter<long>(
        "leads.enriched", description: "Number of leads successfully enriched");

    public static readonly Counter<long> LeadsEnrichmentFailed = Meter.CreateCounter<long>(
        "leads.enrichment_failed", description: "Number of leads that failed enrichment");

    public static readonly Counter<long> LeadsNotificationSent = Meter.CreateCounter<long>(
        "leads.notification_sent", description: "Number of lead notifications sent");

    public static readonly Counter<long> LeadsNotificationFailed = Meter.CreateCounter<long>(
        "leads.notification_failed", description: "Number of lead notifications that failed");

    public static readonly Counter<long> LeadsDeleted = Meter.CreateCounter<long>(
        "leads.deleted", description: "Number of leads deleted");

    // Histograms
    public static readonly Histogram<double> EnrichmentDuration = Meter.CreateHistogram<double>(
        "leads.enrichment_duration_ms", unit: "ms", description: "Duration of lead enrichment");

    public static readonly Histogram<double> NotificationDuration = Meter.CreateHistogram<double>(
        "leads.notification_duration_ms", unit: "ms", description: "Duration of notification delivery");

    public static readonly Histogram<double> HomeSearchDuration = Meter.CreateHistogram<double>(
        "leads.home_search_duration_ms", unit: "ms", description: "Duration of home search for lead");

    public static readonly Histogram<double> TotalPipelineDuration = Meter.CreateHistogram<double>(
        "leads.total_pipeline_duration_ms", unit: "ms", description: "Total duration of the lead pipeline");

    // Token counters
    public static readonly Counter<long> LlmTokensInput = Meter.CreateCounter<long>(
        "leads.llm_tokens.input", description: "LLM input tokens consumed for lead processing");

    public static readonly Counter<long> LlmTokensOutput = Meter.CreateCounter<long>(
        "leads.llm_tokens.output", description: "LLM output tokens consumed for lead processing");

    public static readonly Counter<double> LlmCostUsd = Meter.CreateCounter<double>(
        "leads.llm_cost_usd", unit: "USD", description: "Estimated LLM cost in USD for lead processing");
}
