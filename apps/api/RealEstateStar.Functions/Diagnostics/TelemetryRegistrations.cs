namespace RealEstateStar.Functions.Diagnostics;

/// <summary>
/// Single source of truth for every ActivitySource and Meter name registered with OpenTelemetry
/// in the Functions host. AddSource / AddMeter calls in Program.cs MUST use these lists.
///
/// Ordering: clients → data services → workers → services → activities → pipeline-specific → planned.
/// </summary>
public static class TelemetryRegistrations
{
    public static IReadOnlyList<string> ActivitySourceNames =>
    [
        // ── Client libraries ───────────────────────────────────────────────────
        "RealEstateStar.Claude",
        "RealEstateStar.Gmail",
        "RealEstateStar.GDrive",
        "RealEstateStar.GDocs",
        "RealEstateStar.GSheets",
        "RealEstateStar.Scraper",
        "RealEstateStar.RentCast",
        "RealEstateStar.TokenStore",
        "RealEstateStar.Queue",
        "RealEstateStar.Zillow",
        "RealEstateStar.GooglePlaces",

        // ── Data services ──────────────────────────────────────────────────────
        "RealEstateStar.FanOut",
        "RealEstateStar.Activation",

        // ── Lead pipeline ──────────────────────────────────────────────────────
        "RealEstateStar.Leads",
        "RealEstateStar.Orchestrator",
        "RealEstateStar.Cma",
        "RealEstateStar.HomeSearch",

        // ── Services ───────────────────────────────────────────────────────────
        "RealEstateStar.LeadCommunicator",
        "RealEstateStar.AgentNotifier",

        // ── Activities ─────────────────────────────────────────────────────────
        "RealEstateStar.Pdf",
        "RealEstateStar.ContactDetection",

        // ── WhatsApp pipeline ──────────────────────────────────────────────────
        "RealEstateStar.WhatsApp",

        // ── Cross-cutting ──────────────────────────────────────────────────────
        "RealEstateStar.Language",
        "RealEstateStar.Onboarding",

        // ── Durable Functions tracing middleware ───────────────────────────────
        DurableOrchestratorTracingMiddleware.SourceName,

        // ── Planned / not yet backed by a Diagnostics class ───────────────────
        // Registered early so in-flight spans are not dropped when the class is added.
        "RealEstateStar.AgentContext",
    ];

    public static IReadOnlyList<string> MeterNames =>
    [
        // ── Client libraries ───────────────────────────────────────────────────
        "RealEstateStar.Claude",
        "RealEstateStar.Gmail",
        "RealEstateStar.GDrive",
        "RealEstateStar.GDocs",
        "RealEstateStar.GSheets",
        "RealEstateStar.Scraper",
        "RealEstateStar.RentCast",
        "RealEstateStar.TokenStore",
        "RealEstateStar.Queue",
        "RealEstateStar.Zillow",
        "RealEstateStar.GooglePlaces",

        // ── Data services ──────────────────────────────────────────────────────
        "RealEstateStar.FanOut",
        "RealEstateStar.Activation",

        // ── Lead pipeline ──────────────────────────────────────────────────────
        "RealEstateStar.Leads",
        "RealEstateStar.Orchestrator",
        "RealEstateStar.Cma",
        "RealEstateStar.HomeSearch",

        // ── Services ───────────────────────────────────────────────────────────
        "RealEstateStar.LeadCommunicator",
        "RealEstateStar.AgentNotifier",

        // ── Activities ─────────────────────────────────────────────────────────
        "RealEstateStar.Pdf",
        "RealEstateStar.ContactDetection",

        // ── WhatsApp pipeline ──────────────────────────────────────────────────
        "RealEstateStar.WhatsApp",

        // ── Cross-cutting ──────────────────────────────────────────────────────
        "RealEstateStar.Language",
        "RealEstateStar.Onboarding",

        // ── Planned / not yet backed by a Diagnostics class ───────────────────
        "RealEstateStar.AgentContext",
    ];
}
