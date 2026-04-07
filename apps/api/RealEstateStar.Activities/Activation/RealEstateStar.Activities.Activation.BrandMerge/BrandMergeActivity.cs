using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Activities.Activation.BrandMerge;

/// <summary>
/// Single writer for brokerage brand files.
/// Calls IBrandMergeService to get the merged Brand Profile and Brand Voice,
/// then writes both to real-estate-star/{accountId}/.
/// This is the ONLY place that reads and writes brand files.
/// </summary>
public sealed class BrandMergeActivity(
    IBrandMergeService brandMergeService,
    IFileStorageProvider storage,
    ILogger<BrandMergeActivity> logger)
{
    internal const string FolderPrefix = "real-estate-star";
    internal const string BrandProfileFile = "Brand Profile.md";
    internal const string BrandVoiceFile = "Brand Voice.md";

    // Brand-related localized skill key prefixes
    private static readonly string[] BrandSkillPrefixes = ["BrandExtraction", "BrandVoice"];

    public async Task ExecuteAsync(
        string accountId,
        string agentId,
        string brandingKit,
        string voiceSkill,
        CancellationToken ct,
        ActivationOutputs? outputs = null)
    {
        logger.LogInformation(
            "[BRAND-ACTIVITY-010] Running brand merge for accountId={AccountId}, agentId={AgentId}",
            accountId, agentId);

        var result = await brandMergeService.MergeAsync(accountId, agentId, brandingKit, voiceSkill, ct);

        var accountFolder = $"{FolderPrefix}/{accountId}";
        await storage.EnsureFolderExistsAsync(accountFolder, ct);

        // Write both files; overwrite since merge always produces the enriched version
        var writeTasks = new List<Task>
        {
            WriteOrUpdateAsync(accountFolder, BrandProfileFile, result.BrandProfileMarkdown, ct),
            WriteOrUpdateAsync(accountFolder, BrandVoiceFile, result.BrandVoiceMarkdown, ct),
        };

        // Merge localized brand skills (e.g., BrandExtraction.es, BrandVoice.es)
        if (outputs?.LocalizedSkills is not null)
        {
            var localizedLocales = GetBrandLocales(outputs.LocalizedSkills);
            foreach (var locale in localizedLocales)
            {
                writeTasks.Add(MergeLocalizedBrandAsync(
                    accountId, agentId, accountFolder, locale, outputs.LocalizedSkills, ct));
            }
        }

        await Task.WhenAll(writeTasks);

        logger.LogInformation(
            "[BRAND-ACTIVITY-090] Brand files written for accountId={AccountId}", accountId);
    }

    private async Task MergeLocalizedBrandAsync(
        string accountId,
        string agentId,
        string accountFolder,
        string locale,
        IReadOnlyDictionary<string, string> localizedSkills,
        CancellationToken ct)
    {
        // Use localized brand signals if available, otherwise fall back to empty
        localizedSkills.TryGetValue($"BrandExtraction.{locale}", out var localizedBrandingKit);
        localizedSkills.TryGetValue($"BrandVoice.{locale}", out var localizedVoiceSkill);

        var brandingKit = localizedBrandingKit ?? string.Empty;
        var voiceSkill = localizedVoiceSkill ?? string.Empty;

        if (string.IsNullOrWhiteSpace(brandingKit) && string.IsNullOrWhiteSpace(voiceSkill))
            return;

        logger.LogInformation(
            "[BRAND-ACTIVITY-020] Running localized brand merge for accountId={AccountId}, locale={Locale}",
            accountId, locale);

        var result = await brandMergeService.MergeAsync(
            accountId, agentId, brandingKit, voiceSkill, ct, locale);

        var profileTask = WriteOrUpdateAsync(accountFolder, $"Brand Profile.{locale}.md", result.BrandProfileMarkdown, ct);
        var voiceTask = WriteOrUpdateAsync(accountFolder, $"Brand Voice.{locale}.md", result.BrandVoiceMarkdown, ct);
        await Task.WhenAll(profileTask, voiceTask);

        logger.LogInformation(
            "[BRAND-ACTIVITY-025] Localized brand files written for accountId={AccountId}, locale={Locale}",
            accountId, locale);
    }

    /// <summary>
    /// Extracts distinct locales from brand-related localized skill keys.
    /// E.g., "BrandExtraction.es" and "BrandVoice.es" both yield "es".
    /// </summary>
    internal static IReadOnlyList<string> GetBrandLocales(IReadOnlyDictionary<string, string> localizedSkills)
    {
        return localizedSkills.Keys
            .Where(k => BrandSkillPrefixes.Any(p => k.StartsWith(p + ".", StringComparison.Ordinal)))
            .Select(k => k[(k.LastIndexOf('.') + 1)..])
            .Distinct()
            .ToList();
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
