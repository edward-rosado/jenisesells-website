using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Categories assigned to emails during the Phase 1.5 pre-classification step.
/// Each email can have multiple categories.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EmailCategory
{
    Transaction,
    Marketing,
    FeeRelated,
    Compliance,
    LeadNurture,
    Negotiation,
    Personal,
    Administrative
}

/// <summary>
/// An email with its assigned categories from the classification step.
/// </summary>
public sealed record ClassifiedEmail(
    string EmailId,
    IReadOnlyList<EmailCategory> Categories);

/// <summary>
/// Summary of the entire email corpus produced during classification.
/// </summary>
public sealed record CorpusSummary(
    int TotalEmails,
    int TransactionCount,
    int MarketingCount,
    int FeeRelatedCount,
    int ComplianceCount,
    int LeadNurtureCount,
    Dictionary<string, int> LanguageDistribution,
    string? DominantTone,
    string? AverageEmailLength);

/// <summary>
/// Result of the email classification worker — classifications per email plus a corpus summary.
/// </summary>
public sealed record EmailClassificationResult(
    IReadOnlyList<ClassifiedEmail> Classifications,
    CorpusSummary Summary);
