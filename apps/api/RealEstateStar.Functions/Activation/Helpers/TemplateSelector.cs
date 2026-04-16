using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Functions.Activation.Helpers;

/// <summary>
/// Pure compute helper: picks the best-fit site template from the available catalog
/// based on the agent's SiteFacts vibe hints and specialties.
///
/// V1 logic is intentionally simple — a few if/else checks are enough to make the
/// right call for the majority of agents. A full scoring algorithm is future work.
///
/// Available templates (10 total):
///   luxury-estate, light-luxury, commercial, warm-community, emerald-classic,
///   coastal-breeze, urban-modern, family-first, market-expert, heritage-trust
/// </summary>
public static class TemplateSelector
{
    // Keyword sets used by the selection heuristic (lowercase, trimmed)
    private static readonly HashSet<string> LuxuryVibeKeywords =
        ["luxury", "high-end", "high end", "prestige", "premium", "estate", "exclusive", "affluent"];

    private static readonly HashSet<string> CommercialSpecialtyKeywords =
        ["commercial", "investment", "industrial", "office", "retail", "multi-family", "multifamily", "noi"];

    private static readonly HashSet<string> WarmCommunityVibeKeywords =
        ["community", "family", "warm", "neighborhood", "local", "friendly", "first-time", "first time"];

    /// <summary>
    /// Selects a template name from the 10 available based on SiteFacts vibe hints and specialties.
    /// Returns a lowercase, hyphenated template identifier (e.g., "emerald-classic").
    /// </summary>
    public static string SelectTemplate(SiteFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var vibeHints = facts.Specialties.VibeHints
            .Select(v => v.ToLowerInvariant().Trim())
            .ToHashSet();

        var specialties = facts.Specialties.Specialties
            .Select(s => s.ToLowerInvariant().Trim())
            .ToHashSet();

        // 1. Commercial check — specialties drive this, not vibe
        if (specialties.Any(s => CommercialSpecialtyKeywords.Contains(s)
            || CommercialSpecialtyKeywords.Any(k => s.Contains(k))))
        {
            return "commercial";
        }

        // 2. Luxury — vibe hints take precedence over specialties for luxury
        if (vibeHints.Any(v => LuxuryVibeKeywords.Contains(v)
            || LuxuryVibeKeywords.Any(k => v.Contains(k))))
        {
            // "light-luxury" for newer agents (< 5 years), "luxury-estate" for established ones
            return facts.Agent.YearsExperience >= 5 ? "luxury-estate" : "light-luxury";
        }

        // Luxury can also be signaled by specialties
        if (specialties.Any(s => LuxuryVibeKeywords.Contains(s)
            || LuxuryVibeKeywords.Any(k => s.Contains(k))))
        {
            return "luxury-estate";
        }

        // 3. Warm / community — family-focused or first-time buyer market
        if (vibeHints.Any(v => WarmCommunityVibeKeywords.Contains(v)
            || WarmCommunityVibeKeywords.Any(k => v.Contains(k))))
        {
            return "warm-community";
        }

        // 4. Default — solid, professional baseline that works for any agent
        return "emerald-classic";
    }
}
