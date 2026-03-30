using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
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

    public async Task ExecuteAsync(
        string accountId,
        string agentId,
        string brandingKit,
        string voiceSkill,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[BRAND-ACTIVITY-010] Running brand merge for accountId={AccountId}, agentId={AgentId}",
            accountId, agentId);

        var result = await brandMergeService.MergeAsync(accountId, agentId, brandingKit, voiceSkill, ct);

        var accountFolder = $"{FolderPrefix}/{accountId}";
        await storage.EnsureFolderExistsAsync(accountFolder, ct);

        // Write both files; overwrite since merge always produces the enriched version
        var profileTask = WriteOrUpdateAsync(accountFolder, BrandProfileFile, result.BrandProfileMarkdown, ct);
        var voiceTask = WriteOrUpdateAsync(accountFolder, BrandVoiceFile, result.BrandVoiceMarkdown, ct);

        await Task.WhenAll(profileTask, voiceTask);

        logger.LogInformation(
            "[BRAND-ACTIVITY-090] Brand files written for accountId={AccountId}", accountId);
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
