using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Activities.Activation.PersistAgentProfile;

/// <summary>
/// Fan-out writes all per-agent activation outputs to real-estate-star/{agentId}/.
/// Also creates the leads/ and leads/consent/ subdirectories.
/// Then delegates to IAgentConfigService to generate account.json + content.json.
///
/// Lead data isolation: lead files fan-out to Agent Drive + Platform Blob ONLY —
/// never to Account Drive (brokerage folder).
/// </summary>
public sealed class AgentProfilePersistActivity(
    IFileStorageProviderFactory storageFactory,
    IAgentConfigService agentConfigService,
    ILogger<AgentProfilePersistActivity> logger)
{
    private IFileStorageProvider? _storage;
    private IFileStorageProvider storage => _storage ?? throw new InvalidOperationException("Call ExecuteAsync first");

    internal const string FolderPrefix = "real-estate-star";

    // Per-agent markdown skill files
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
    internal const string DriveIndexFile = "Drive Index.md";
    internal const string AgentDiscoveryFile = "Agent Discovery.md";
    internal const string EmailSignatureFile = "Email Signature.md";
    internal const string ThirdPartyProfilesFile = "Third Party Profiles.md";
    internal const string ConsentLogFile = "Consent Log.md";

    // Skill key → file name prefix mapping for localized skill files
    private static readonly Dictionary<string, string> SkillKeyToFilePrefix = new()
    {
        ["VoiceSkill"] = "Voice Skill",
        ["PersonalitySkill"] = "Personality Skill",
        ["MarketingStyle"] = "Marketing Style",
        ["BrandExtraction"] = "Brand Extraction",
        ["BrandVoice"] = "Brand Voice",
        ["CmaStyleGuide"] = "CMA Style Guide",
        ["WebsiteStyleGuide"] = "Website Style Guide",
    };

    // Binary asset file names
    internal const string HeadshotFile = "headshot.jpg";
    internal const string BrokerageLogoFile = "brokerage-logo.png";
    internal const string BrokerageIconFile = "brokerage-icon.png";

    // Lead subdirectories (created but not populated here)
    internal const string LeadsSubfolder = "leads";
    internal const string ConsentSubfolder = "leads/consent";

    public async Task ExecuteAsync(
        string accountId,
        string agentId,
        string handle,
        ActivationOutputs outputs,
        CancellationToken ct)
    {
        _storage = storageFactory.CreateForAgent(accountId, agentId);
        var agentFolder = $"{FolderPrefix}/{agentId}";

        logger.LogInformation(
            "[PERSIST-AGENT-010] Persisting activation outputs for agentId={AgentId}, accountId={AccountId}",
            agentId, accountId);

        // Ensure agent folder and lead subdirectories exist
        await storage.EnsureFolderExistsAsync(agentFolder, ct);
        await storage.EnsureFolderExistsAsync($"{agentFolder}/{LeadsSubfolder}", ct);
        await storage.EnsureFolderExistsAsync($"{agentFolder}/{ConsentSubfolder}", ct);

        // Fan-out write all per-agent markdown files
        var writeTasks = new List<Task>();

        writeTasks.AddRange(BuildMarkdownWriteTasks(agentFolder, outputs, ct));
        writeTasks.AddRange(BuildBinaryWriteTasks(agentFolder, outputs, ct));
        writeTasks.AddRange(BuildLocalizedSkillWriteTasks(agentFolder, agentId, outputs, ct));

        await Task.WhenAll(writeTasks);

        logger.LogInformation(
            "[PERSIST-AGENT-050] Markdown and binary assets persisted for agentId={AgentId}", agentId);

        // Verify critical files landed in the Agent Drive tier.
        // FanOutStorageProvider swallows Drive failures as warnings (correct for best-effort lead storage),
        // but during activation the agent expects to see files in their Google Drive.
        await VerifyDriveSyncAsync(agentFolder, outputs, ct);

        // Delegate config generation to AgentConfigService
        await agentConfigService.GenerateAsync(accountId, agentId, handle, outputs, ct);

        logger.LogInformation(
            "[PERSIST-AGENT-090] Activation persistence complete for agentId={AgentId}", agentId);
    }

    // ── Task builders ─────────────────────────────────────────────────────────

    private List<Task> BuildMarkdownWriteTasks(
        string agentFolder,
        ActivationOutputs outputs,
        CancellationToken ct)
    {
        var tasks = new List<Task>();

        var markdownFiles = new[]
        {
            (VoiceSkillFile, outputs.VoiceSkill),
            (PersonalitySkillFile, outputs.PersonalitySkill),
            (CmaStyleGuideFile, outputs.CmaStyleGuide),
            (WebsiteStyleGuideFile, outputs.WebsiteStyleGuide),
            (SalesPipelineFile, outputs.SalesPipeline),
            (CoachingReportFile, outputs.CoachingReport),
            (BrandingKitFile, outputs.BrandingKitMarkdown),
            (ComplianceAnalysisFile, outputs.ComplianceAnalysis),
            (DriveIndexFile, outputs.DriveIndex),
            (AgentDiscoveryFile, outputs.AgentDiscoveryMarkdown),
            (EmailSignatureFile, outputs.EmailSignature),
            (ThirdPartyProfilesFile, outputs.ThirdPartyProfiles),
            (ConsentLogFile, outputs.ConsentLog),
        };

        foreach (var (fileName, content) in markdownFiles)
        {
            if (content is null) continue;
            tasks.Add(WriteOrUpdateAsync(agentFolder, fileName, content, ct));
        }

        // Write pipeline.json (structured JSON, not markdown)
        if (outputs.PipelineJson is not null)
        {
            tasks.Add(WriteOrUpdateAsync(agentFolder, "pipeline.json", outputs.PipelineJson, ct));
        }

        return tasks;
    }

    private List<Task> BuildBinaryWriteTasks(
        string agentFolder,
        ActivationOutputs outputs,
        CancellationToken ct)
    {
        var tasks = new List<Task>();

        // Binary assets are written as base64-encoded strings to the document store
        // (a real implementation would use IDocumentStorageProvider with byte[] overload or blob storage)
        if (outputs.HeadshotBytes is not null)
            tasks.Add(WriteOrUpdateAsync(agentFolder, HeadshotFile,
                Convert.ToBase64String(outputs.HeadshotBytes), ct));

        if (outputs.BrokerageLogoBytes is not null)
            tasks.Add(WriteOrUpdateAsync(agentFolder, BrokerageLogoFile,
                Convert.ToBase64String(outputs.BrokerageLogoBytes), ct));

        if (outputs.BrokerageIconBytes is not null)
            tasks.Add(WriteOrUpdateAsync(agentFolder, BrokerageIconFile,
                Convert.ToBase64String(outputs.BrokerageIconBytes), ct));

        return tasks;
    }

    private List<Task> BuildLocalizedSkillWriteTasks(
        string agentFolder,
        string agentId,
        ActivationOutputs outputs,
        CancellationToken ct)
    {
        var tasks = new List<Task>();

        if (outputs.LocalizedSkills is null || outputs.LocalizedSkills.Count == 0)
            return tasks;

        foreach (var (key, content) in outputs.LocalizedSkills)
        {
            var fileName = MapLocalizedSkillKeyToFileName(key);
            if (fileName is null)
            {
                logger.LogWarning(
                    "[LANG-010] Unknown localized skill key {SkillKey} for agentId={AgentId}, skipping",
                    key, agentId);
                continue;
            }

            tasks.Add(WriteOrUpdateAsync(agentFolder, fileName, content, ct));

            // Extract skill name and locale from "SkillName.locale" format
            var dotIndex = key.LastIndexOf('.');
            var skillName = dotIndex > 0 ? key[..dotIndex] : key;
            var locale = dotIndex > 0 ? key[(dotIndex + 1)..] : "unknown";

            logger.LogInformation(
                "[LANG-009] Per-language skill {SkillName}.{Locale} persisted for {AgentId}",
                skillName, locale, agentId);
        }

        return tasks;
    }

    /// <summary>
    /// Maps a localized skill key like "VoiceSkill.es" to "Voice Skill.es.md".
    /// </summary>
    internal static string? MapLocalizedSkillKeyToFileName(string key)
    {
        // Key format: "{SkillName}.{locale}" — e.g., "VoiceSkill.es"
        var dotIndex = key.LastIndexOf('.');
        if (dotIndex <= 0) return null;

        var skillName = key[..dotIndex];
        var locale = key[(dotIndex + 1)..];

        if (!SkillKeyToFilePrefix.TryGetValue(skillName, out var filePrefix))
            return null;

        return $"{filePrefix}.{locale}.md";
    }

    /// <summary>
    /// Spot-checks a representative file to confirm the Agent Drive tier received the write.
    /// Logs an ERROR if the file is missing — indicates the Drive tier silently failed.
    /// </summary>
    internal async Task VerifyDriveSyncAsync(
        string agentFolder,
        ActivationOutputs outputs,
        CancellationToken ct)
    {
        // Pick the first non-null markdown file as the verification probe
        var probeFile = outputs.VoiceSkill is not null ? VoiceSkillFile
            : outputs.PersonalitySkill is not null ? PersonalitySkillFile
            : outputs.DriveIndex is not null ? DriveIndexFile
            : null;

        if (probeFile is null) return;

        try
        {
            var content = await storage.ReadDocumentAsync(agentFolder, probeFile, ct);
            if (content is null)
            {
                logger.LogError(
                    "[PERSIST-AGENT-060] Drive sync verification failed — {ProbeFile} not found in {AgentFolder}. " +
                    "The Agent Drive tier may have silently failed during activation.",
                    probeFile, agentFolder);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "[PERSIST-AGENT-061] Drive sync verification threw for {ProbeFile} in {AgentFolder}",
                probeFile, agentFolder);
        }
    }

    private async Task WriteOrUpdateAsync(
        string folder,
        string fileName,
        string content,
        CancellationToken ct)
    {
        var existing = await storage.ReadDocumentAsync(folder, fileName, ct);
        if (existing is null)
            await storage.WriteDocumentAsync(folder, fileName, content, ct);
        else
            await storage.UpdateDocumentAsync(folder, fileName, content, ct);
    }
}
