using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Domain.Shared.Diagnostics;

public static class LanguageDiagnostics
{
    public static readonly ActivitySource ActivitySource = new("RealEstateStar.Language");
    public static readonly Meter Meter = new("RealEstateStar.Language");

    // Counters
    public static readonly Counter<long> EmailsDetected = Meter.CreateCounter<long>("language.emails.detected");
    public static readonly Counter<long> DocsDetected = Meter.CreateCounter<long>("language.docs.detected");
    public static readonly Counter<long> DetectionFallback = Meter.CreateCounter<long>("language.detection.fallback");
    public static readonly Counter<long> SkillsExtracted = Meter.CreateCounter<long>("language.skills.extracted");
    public static readonly Counter<long> SkillsLowConfidence = Meter.CreateCounter<long>("language.skills.low_confidence");
    public static readonly Counter<long> LeadLocale = Meter.CreateCounter<long>("language.lead.locale");
    public static readonly Counter<long> EmailDrafted = Meter.CreateCounter<long>("language.email.drafted");
    public static readonly Counter<long> CmaGenerated = Meter.CreateCounter<long>("language.cma.generated");
    public static readonly Counter<long> WelcomeSent = Meter.CreateCounter<long>("language.welcome.sent");

    // Histograms
    public static readonly Histogram<double> DetectionDuration = Meter.CreateHistogram<double>("language.detection_duration_ms");
    public static readonly Histogram<double> ExtractionDuration = Meter.CreateHistogram<double>("language.extraction_duration_ms");
}
