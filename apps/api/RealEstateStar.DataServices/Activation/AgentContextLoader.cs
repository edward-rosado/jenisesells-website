using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.Activation;

/// <summary>
/// Loads all activation outputs from IFileStorageProvider to reconstruct an AgentContext.
/// Per-agent files live at real-estate-star/{agentId}/.
/// Per-brokerage files live at real-estate-star/{accountId}/.
/// Returns null if no activation files exist for the agent.
/// </summary>
public sealed class AgentContextLoader(
    IFileStorageProvider storage,
    ILogger<AgentContextLoader> logger) : IAgentContextLoader
{
    internal const string FolderPrefix = "real-estate-star";
    internal const string LowConfidenceMarker = "Low confidence";

    internal const string VoiceSkillFile = "Voice Skill.md";
    internal const string PersonalitySkillFile = "Personality Skill.md";
    internal const string CmaStyleGuideFile = "CMA Style Guide.md";
    internal const string MarketingStyleFile = "Marketing Style.md";
    internal const string WebsiteStyleGuideFile = "Website Style Guide.md";
    internal const string SalesPipelineFile = "Sales Pipeline.md";
    internal const string CoachingReportFile = "Coaching Report.md";
    internal const string BrandingKitFile = "Branding Kit.md";
    internal const string ComplianceAnalysisFile = "Compliance Analysis.md";
    internal const string FeeStructureFile = "Fee Structure.md";
    internal const string BrandProfileFile = "Brand Profile.md";
    internal const string BrandVoiceFile = "Brand Voice.md";

    internal static readonly IReadOnlyList<string> RequiredAgentFiles =
    [
        VoiceSkillFile,
        PersonalitySkillFile,
        CmaStyleGuideFile,
        MarketingStyleFile,
        SalesPipelineFile,
        CoachingReportFile,
    ];

    public async Task<AgentContext?> LoadAsync(string accountId, string agentId, CancellationToken ct)
    {
        var agentFolder = $"{FolderPrefix}/{agentId}";
        var accountFolder = $"{FolderPrefix}/{accountId}";

        logger.LogDebug("[CTX-001] Loading agent context for agentId={AgentId}, accountId={AccountId}",
            agentId, accountId);

        // Load per-agent files in parallel
        var voiceTask = storage.ReadDocumentAsync(agentFolder, VoiceSkillFile, ct);
        var personalityTask = storage.ReadDocumentAsync(agentFolder, PersonalitySkillFile, ct);
        var cmaStyleTask = storage.ReadDocumentAsync(agentFolder, CmaStyleGuideFile, ct);
        var marketingTask = storage.ReadDocumentAsync(agentFolder, MarketingStyleFile, ct);
        var websiteTask = storage.ReadDocumentAsync(agentFolder, WebsiteStyleGuideFile, ct);
        var pipelineTask = storage.ReadDocumentAsync(agentFolder, SalesPipelineFile, ct);
        var coachingTask = storage.ReadDocumentAsync(agentFolder, CoachingReportFile, ct);
        var brandingKitTask = storage.ReadDocumentAsync(agentFolder, BrandingKitFile, ct);
        var complianceTask = storage.ReadDocumentAsync(agentFolder, ComplianceAnalysisFile, ct);
        var feeTask = storage.ReadDocumentAsync(agentFolder, FeeStructureFile, ct);

        // Load per-account (brokerage) files in parallel
        var brandProfileTask = storage.ReadDocumentAsync(accountFolder, BrandProfileFile, ct);
        var brandVoiceTask = storage.ReadDocumentAsync(accountFolder, BrandVoiceFile, ct);

        await Task.WhenAll(
            voiceTask, personalityTask, cmaStyleTask, marketingTask,
            websiteTask, pipelineTask, coachingTask, brandingKitTask,
            complianceTask, feeTask, brandProfileTask, brandVoiceTask);

        var voiceSkill = await voiceTask;
        var personalitySkill = await personalityTask;
        var cmaStyleGuide = await cmaStyleTask;
        var marketingStyle = await marketingTask;
        var websiteStyleGuide = await websiteTask;
        var salesPipeline = await pipelineTask;
        var coachingReport = await coachingTask;
        var brandingKit = await brandingKitTask;
        var complianceAnalysis = await complianceTask;
        var feeStructure = await feeTask;
        var brandProfile = await brandProfileTask;
        var brandVoice = await brandVoiceTask;

        // Return null if no activation files exist
        if (voiceSkill is null && personalitySkill is null && cmaStyleGuide is null &&
            marketingStyle is null && salesPipeline is null && coachingReport is null)
        {
            logger.LogDebug("[CTX-002] No activation files found for agentId={AgentId}", agentId);
            return null;
        }

        // IsActivated = all required files are present
        var isActivated =
            voiceSkill is not null &&
            personalitySkill is not null &&
            cmaStyleGuide is not null &&
            marketingStyle is not null &&
            salesPipeline is not null &&
            coachingReport is not null;

        // IsLowConfidence = any loaded file contains the low confidence marker
        var allFiles = new[] { voiceSkill, personalitySkill, cmaStyleGuide, marketingStyle,
            websiteStyleGuide, salesPipeline, coachingReport, brandingKit,
            complianceAnalysis, feeStructure, brandProfile, brandVoice };

        var isLowConfidence = allFiles
            .Where(f => f is not null)
            .Any(f => f!.Contains(LowConfidenceMarker, StringComparison.OrdinalIgnoreCase));

        logger.LogInformation(
            "[CTX-010] Loaded agent context for agentId={AgentId}: IsActivated={IsActivated}, IsLowConfidence={IsLowConfidence}",
            agentId, isActivated, isLowConfidence);

        return new AgentContext
        {
            VoiceSkill = voiceSkill,
            PersonalitySkill = personalitySkill,
            CmaStyleGuide = cmaStyleGuide,
            MarketingStyle = marketingStyle,
            WebsiteStyleGuide = websiteStyleGuide,
            SalesPipeline = salesPipeline,
            CoachingReport = coachingReport,
            BrandingKit = brandingKit,
            ComplianceAnalysis = complianceAnalysis,
            FeeStructure = feeStructure,
            BrandProfile = brandProfile,
            BrandVoice = brandVoice,
            IsActivated = isActivated,
            IsLowConfidence = isLowConfidence,
        };
    }
}
