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
}
