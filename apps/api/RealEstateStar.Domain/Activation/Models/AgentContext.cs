namespace RealEstateStar.Domain.Activation.Models;

public sealed record AgentContext
{
    public string? VoiceSkill { get; init; }
    public string? PersonalitySkill { get; init; }
    public string? CmaStyleGuide { get; init; }
    public string? MarketingStyle { get; init; }
    public string? SalesPipeline { get; init; }
    public string? CoachingReport { get; init; }
    public string? WebsiteStyleGuide { get; init; }
    public string? BrandingKit { get; init; }
    public string? ComplianceAnalysis { get; init; }
    public string? FeeStructure { get; init; }
    public string? BrandProfile { get; init; }
    public string? BrandVoice { get; init; }
    public bool IsActivated { get; init; }
    public bool IsLowConfidence { get; init; }
}
