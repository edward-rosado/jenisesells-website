namespace RealEstateStar.Domain.Activation.Models;

public sealed record AgentContext
{
    public string? VoiceSkill { get; init; }
    public string? PersonalitySkill { get; init; }
    public string? CmaStyleGuide { get; init; }
    public string? SalesPipeline { get; init; }
    public string? CoachingReport { get; init; }
    public string? WebsiteStyleGuide { get; init; }
    public string? BrandingKit { get; init; }
    public string? ComplianceAnalysis { get; init; }

    /// <summary>Structured pipeline JSON — agent's lead pipeline for fast C# querying.</summary>
    public string? PipelineJson { get; init; }

    public bool IsActivated { get; init; }
    public bool IsLowConfidence { get; init; }

    // Localized skill variants — keyed by "{skillName}.{locale}" (e.g., "VoiceSkill.es")
    public IReadOnlyDictionary<string, string>? LocalizedSkills { get; init; }

    /// <summary>
    /// Returns the skill content for the given skill name, preferring the specified locale.
    /// Falls back to English if no localized variant exists.
    /// </summary>
    public string? GetSkill(string skillName, string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale) || locale == "en")
            return skillName switch
            {
                "VoiceSkill" => VoiceSkill,
                "PersonalitySkill" => PersonalitySkill,
                "MarketingStyle" => MarketingStyle,
                "BrandVoice" => BrandVoice,
                _ => null
            };

        var key = $"{skillName}.{locale}";
        if (LocalizedSkills is not null && LocalizedSkills.TryGetValue(key, out var localized))
            return localized;

        // Fallback to English
        return GetSkill(skillName, "en");
    }
}
