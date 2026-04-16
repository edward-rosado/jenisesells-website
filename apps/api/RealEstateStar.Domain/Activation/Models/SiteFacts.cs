using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Locale-neutral fact substrate extracted once per activation.
/// Every field on the generated site traces back to exactly one path in SiteFacts.
/// Consumed by VoicedContentGenerator for per-locale resynthesis.
/// </summary>
public sealed record SiteFacts(
    AgentIdentity Agent,
    BrokerageIdentity Brokerage,
    LocationFacts Location,
    SpecialtiesFacts Specialties,
    TrustSignals Trust,
    IReadOnlyList<RecentSale> RecentSales,
    IReadOnlyList<SiteTestimonial> Testimonials,
    IReadOnlyList<AgentCredential> Credentials,
    PipelineStages Stages,
    IReadOnlyDictionary<string, LocaleVoice> VoicesByLocale)
{
    /// <summary>
    /// SHA-256 hash of all fact values, used as cache key for voiced content.
    /// </summary>
    [JsonPropertyName("facts_hash")]
    public string FactsHash { get; init; } = "";
}

public sealed record AgentIdentity(
    string Name,
    string LegalName,
    string Title,
    string Email,
    string Phone,
    string? LicenseNumber,
    int YearsExperience,
    IReadOnlyList<string> Languages);

public sealed record BrokerageIdentity(
    string Name,
    string? LegalName,
    string? LicenseNumber,
    string? OfficeAddress,
    string? OfficePhone,
    string? DomainHint);

public sealed record LocationFacts(
    string State,
    IReadOnlyList<string> ServiceAreas,
    IReadOnlyDictionary<string, int> ListingFrequencyByCity);

public sealed record SpecialtiesFacts(
    IReadOnlyList<string> Specialties,
    IReadOnlyList<string> VibeHints,
    IReadOnlyDictionary<string, int> EvidenceCount);

public sealed record TrustSignals(
    int ReviewCount,
    decimal AverageRating,
    int TransactionCount,
    TimeSpan AverageResponseTime,
    decimal AverageSalePrice);

public sealed record RecentSale(
    string Address,
    string City,
    string State,
    decimal Price,
    DateTime SoldDate,
    bool SoldByAgent,
    string? ListingCourtesyOf,
    string? ImageUrl);

public sealed record SiteTestimonial(
    string Text,
    int Rating,
    string Reviewer,
    string Source,
    DateTime Date);

public sealed record AgentCredential(
    string Name,
    string? Issuer,
    DateTime? IssuedAt);

public sealed record PipelineStages(
    IReadOnlyList<string> StageNames,
    IReadOnlyDictionary<string, int> LeadCountByStage);

public sealed record LocaleVoice(
    string Locale,
    string VoiceSkillMarkdown,
    string PersonalitySkillMarkdown,
    string VoiceHash);
