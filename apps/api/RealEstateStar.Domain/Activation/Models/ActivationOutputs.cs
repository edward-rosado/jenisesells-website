namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// All computed outputs from the activation pipeline, used to populate
/// agent config files and send the welcome notification.
/// </summary>
public sealed record ActivationOutputs
{
    // Per-agent skill files
    public string? VoiceSkill { get; init; }
    public string? PersonalitySkill { get; init; }
    public string? CmaStyleGuide { get; init; }
    public string? MarketingStyle { get; init; }
    public string? WebsiteStyleGuide { get; init; }
    public string? SalesPipeline { get; init; }
    public string? CoachingReport { get; init; }
    public string? BrandingKitMarkdown { get; init; }
    /// <summary>Raw brand extraction signals (intermediate output, not persisted as a skill file).</summary>
    public string? BrandExtractionSignals { get; init; }
    /// <summary>Raw brand voice signals (intermediate output, merged into Brand Voice.md by BrandMergeActivity).</summary>
    public string? BrandVoiceSignals { get; init; }
    public string? ComplianceAnalysis { get; init; }
    public string? FeeStructure { get; init; }
    public string? DriveIndex { get; init; }
    public string? AgentDiscoveryMarkdown { get; init; }
    public string? EmailSignature { get; init; }
    public string? ThirdPartyProfiles { get; init; }
    public string? ConsentLog { get; init; }

    // Localized skill variants — keyed by "{skillName}.{locale}" (e.g., "VoiceSkill.es")
    public IReadOnlyDictionary<string, string>? LocalizedSkills { get; init; }

    // Structured discovery data
    public AgentDiscovery? Discovery { get; init; }
    public BrandingKit? BrandingKit { get; init; }

    // Binary assets
    public byte[]? HeadshotBytes { get; init; }
    public byte[]? BrokerageLogoBytes { get; init; }
    public byte[]? BrokerageIconBytes { get; init; }

    // Agent identity
    public string? AgentName { get; init; }
    public string? AgentEmail { get; init; }
    public string? AgentPhone { get; init; }
    public string? AgentTitle { get; init; }
    public string? AgentLicenseNumber { get; init; }
    public string? AgentTagline { get; init; }
    public string? State { get; init; }
    public IReadOnlyList<string>? ServiceAreas { get; init; }
    public IReadOnlyList<string>? Languages { get; init; }
}
