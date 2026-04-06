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
    internal const string WebsiteStyleGuideFile = "Website Style Guide.md";
    internal const string SalesPipelineFile = "Sales Pipeline.md";
    internal const string CoachingReportFile = "Coaching Report.md";
    internal const string BrandingKitFile = "Branding Kit.md";
    internal const string ComplianceAnalysisFile = "Compliance Analysis.md";
    internal const string PipelineJsonFile = "pipeline.json";

    internal static readonly IReadOnlyList<string> RequiredAgentFiles =
    [
        VoiceSkillFile,
        PersonalitySkillFile,
        CmaStyleGuideFile,
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
        var websiteTask = storage.ReadDocumentAsync(agentFolder, WebsiteStyleGuideFile, ct);
        var pipelineTask = storage.ReadDocumentAsync(agentFolder, SalesPipelineFile, ct);
        var coachingTask = storage.ReadDocumentAsync(agentFolder, CoachingReportFile, ct);
        var brandingKitTask = storage.ReadDocumentAsync(agentFolder, BrandingKitFile, ct);
        var complianceTask = storage.ReadDocumentAsync(agentFolder, ComplianceAnalysisFile, ct);
        var pipelineJsonTask = storage.ReadDocumentAsync(agentFolder, PipelineJsonFile, ct);

        await Task.WhenAll(
            voiceTask, personalityTask, cmaStyleTask,
            websiteTask, pipelineTask, coachingTask, brandingKitTask,
            complianceTask, pipelineJsonTask);

        var voiceSkill = await voiceTask;
        var personalitySkill = await personalityTask;
        var cmaStyleGuide = await cmaStyleTask;
        var websiteStyleGuide = await websiteTask;
        var salesPipeline = await pipelineTask;
        var coachingReport = await coachingTask;
        var brandingKit = await brandingKitTask;
        var complianceAnalysis = await complianceTask;
        var pipelineJson = await pipelineJsonTask;

        // Return null if no activation files exist
        if (voiceSkill is null && personalitySkill is null && cmaStyleGuide is null &&
            salesPipeline is null && coachingReport is null)
        {
            logger.LogDebug("[CTX-002] No activation files found for agentId={AgentId}", agentId);
            return null;
        }

        // IsActivated = all required files are present
        var isActivated =
            voiceSkill is not null &&
            personalitySkill is not null &&
            cmaStyleGuide is not null &&
            salesPipeline is not null &&
            coachingReport is not null;

        // IsLowConfidence = any loaded file contains the low confidence marker
        var allFiles = new[] { voiceSkill, personalitySkill, cmaStyleGuide,
            websiteStyleGuide, salesPipeline, coachingReport, brandingKit,
            complianceAnalysis };

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
            WebsiteStyleGuide = websiteStyleGuide,
            SalesPipeline = salesPipeline,
            CoachingReport = coachingReport,
            BrandingKit = brandingKit,
            ComplianceAnalysis = complianceAnalysis,
            PipelineJson = pipelineJson,
            IsActivated = isActivated,
            IsLowConfidence = isLowConfidence,
        };
    }
}
