using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.DataServices.Diagnostics;

/// <summary>
/// Consolidated diagnostics for the activation pipeline.
///
/// Covers all activation data-service components:
///   account_config   — reading / validating account.json
///   routing          — IRoutingPolicyStore reads and writes
///   voiced_content   — IVoicedContentStore reads, writes, CAS conflicts
///   persist          — profile persistence (PersistAgentProfileActivity)
///   site_facts       — ISiteFactsStore reads and writes
///
/// The <c>component</c> tag is ALWAYS one of the five values above (bounded cardinality).
/// Never pass free-form strings as the component tag.
/// </summary>
public static class ActivationDiagnostics
{
    public const string ServiceName = "RealEstateStar.Activation";

    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    // ── Counters ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Total activation data-service operations, tagged by component.
    /// component ∈ { account_config | routing | voiced_content | persist | site_facts }
    /// </summary>
    public static readonly Counter<long> Operations = Meter.CreateCounter<long>(
        "activation.operations",
        description: "Total activation data-service operations. Tag: component.");

    /// <summary>
    /// CAS (compare-and-swap / optimistic concurrency) conflicts encountered.
    /// A conflict means another writer updated the record between our read and write.
    /// Callers should retry on conflict.
    /// component ∈ { voiced_content | routing | site_facts }
    /// </summary>
    public static readonly Counter<long> Conflicts = Meter.CreateCounter<long>(
        "activation.conflicts",
        description: "CAS / optimistic-concurrency conflicts in activation stores. Tag: component.");

    /// <summary>
    /// Operations that exhausted all retry attempts after repeated CAS conflicts.
    /// component ∈ { voiced_content | routing | site_facts }
    /// </summary>
    public static readonly Counter<long> RetriesExhausted = Meter.CreateCounter<long>(
        "activation.retries_exhausted",
        description: "Activation store operations that failed after all CAS retries. Tag: component.");

    /// <summary>
    /// Times a voiced-content field fell back to the default value because the
    /// agent's custom voiced content was absent or unparseable.
    /// component = voiced_content; field_name identifies the specific field.
    /// </summary>
    public static readonly Counter<long> VoicedFallbacks = Meter.CreateCounter<long>(
        "activation.voiced_fallbacks",
        description: "Times voiced content fell back to default. Tags: component, field_name.");

    // ── Histograms ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Duration of an activation data-service operation, in milliseconds.
    /// component ∈ { account_config | routing | voiced_content | persist | site_facts }
    /// </summary>
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "activation.duration_ms",
        unit: "ms",
        description: "Duration of activation data-service operations. Tag: component.");

    // ── Helper methods ─────────────────────────────────────────────────────────

    /// <summary>
    /// Records a completed data-service operation for the given component.
    /// </summary>
    public static void RecordOperation(string component, double durationMs)
    {
        var tags = new TagList { { "component", component } };
        Operations.Add(1, tags);
        Duration.Record(durationMs, tags);
    }

    /// <summary>
    /// Records a CAS conflict for the given component.
    /// </summary>
    public static void RecordConflict(string component)
    {
        Conflicts.Add(1, new TagList { { "component", component } });
    }

    /// <summary>
    /// Records that all retry attempts were exhausted for the given component.
    /// </summary>
    public static void RecordRetriesExhausted(string component)
    {
        RetriesExhausted.Add(1, new TagList { { "component", component } });
    }

    /// <summary>
    /// Records a voiced-content fallback.
    /// </summary>
    /// <param name="component">Always "voiced_content".</param>
    /// <param name="fieldName">The specific content field that fell back (e.g. "bio", "tagline").</param>
    public static void RecordFallback(string component, string fieldName)
    {
        VoicedFallbacks.Add(1, new TagList { { "component", component }, { "field_name", fieldName } });
    }

    // ── Bounded component tag values ───────────────────────────────────────────
    // Use these constants when recording metrics to prevent typos and cardinality drift.

    public static class Components
    {
        public const string AccountConfig  = "account_config";
        public const string Routing        = "routing";
        public const string VoicedContent  = "voiced_content";
        public const string Persist        = "persist";
        public const string SiteFacts      = "site_facts";
    }
}
