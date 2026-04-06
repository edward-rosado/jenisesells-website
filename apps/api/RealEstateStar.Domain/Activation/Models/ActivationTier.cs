using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Controls which Phase 2 synthesis workers run during activation.
/// MVP: 8 core workers (Voice, Personality, Coaching, Compliance, CmaStyle, Pipeline, Branding, WebsiteStyle).
/// Future: All 12 workers including BrandExtraction, BrandVoice, MarketingStyle, FeeStructure.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActivationTier
{
    Mvp,
    Future
}
