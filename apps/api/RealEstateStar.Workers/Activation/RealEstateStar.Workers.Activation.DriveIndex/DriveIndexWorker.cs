using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;
using DriveIndexModel = RealEstateStar.Domain.Activation.Models.DriveIndex;
using DriveFileModel = RealEstateStar.Domain.Activation.Models.DriveFile;

namespace RealEstateStar.Workers.Activation.DriveIndex;

/// <summary>
/// Phase 1 gather worker: indexes the agent's Google Drive.
/// Finds or creates the "real-estate-star" platform folder, catalogs real-estate documents,
/// reads their content, and surfaces any URLs discovered in documents.
///
/// Pure compute — calls IGDriveClient only. No storage, no DataServices.
/// </summary>
public sealed class DriveIndexWorker(
    IGDriveClient driveClient,
    ILogger<DriveIndexWorker> logger)
{
    internal const string PlatformFolderName = "real-estate-star";

    private static readonly string[] RealEstateKeywords =
    [
        "contract", "agreement", "cma", "comparative", "market", "analysis",
        "listing", "listing presentation", "flyer", "brochure", "marketing",
        "seller", "buyer", "disclosure", "addendum", "amendment",
        "purchase", "offer", "commission", "brokerage", "property",
        "deed", "closing", "inspection", "appraisal"
    ];

    private static readonly string[] TextMimeTypes =
    [
        "application/vnd.google-apps.document",
        "application/vnd.google-apps.spreadsheet",
        "application/vnd.google-apps.presentation",
        "text/plain",
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    ];

    public async Task<DriveIndexModel> RunAsync(string accountId, string agentId, CancellationToken ct)
    {
        logger.LogInformation(
            "[DRIVEINDEX-001] Starting Drive index for account {AccountId}, agent {AgentId}.",
            accountId, agentId);

        // Find or create platform folder
        var folderIdTask = driveClient.GetOrCreateFolderAsync(accountId, agentId, PlatformFolderName, ct);

        // List all files
        var allFilesTask = driveClient.ListAllFilesAsync(accountId, agentId, ct);

        await Task.WhenAll(folderIdTask, allFilesTask);

        var folderId = folderIdTask.Result;
        var allFiles = allFilesTask.Result;

        logger.LogInformation(
            "[DRIVEINDEX-002] Found {FileCount} total files in Drive. Platform folder: {FolderId}",
            allFiles.Count, folderId);

        // Filter for real-estate-related documents
        var realEstateFiles = allFiles
            .Where(f => IsRealEstateFile(f.Name, f.MimeType))
            .ToList();

        logger.LogInformation(
            "[DRIVEINDEX-003] Identified {Count} real estate documents for account {AccountId}, agent {AgentId}.",
            realEstateFiles.Count, accountId, agentId);

        // Read content of each real-estate doc
        var contents = new Dictionary<string, string>();
        var discoveredUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in realEstateFiles)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var content = await driveClient.GetFileContentAsync(accountId, agentId, file.Id, ct);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    contents[file.Id] = content;
                    foreach (var url in ExtractUrls(content))
                        discoveredUrls.Add(url);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "[DRIVEINDEX-010] Failed to read content of file {FileId} ({FileName}) for account {AccountId}",
                    file.Id, file.Name, accountId);
            }
        }

        logger.LogInformation(
            "[DRIVEINDEX-004] Read {ContentCount} documents, discovered {UrlCount} URLs for account {AccountId}, agent {AgentId}.",
            contents.Count, discoveredUrls.Count, accountId, agentId);

        // Map to domain DriveFile records
        var driveFiles = realEstateFiles.Select(f => new DriveFileModel(
            f.Id,
            f.Name,
            f.MimeType,
            CategorizeFile(f.Name),
            f.ModifiedTime ?? DateTime.MinValue)).ToList();

        return new DriveIndexModel(
            folderId,
            driveFiles,
            contents,
            discoveredUrls.ToList());
    }

    internal static bool IsRealEstateFile(string name, string mimeType)
    {
        var lower = name.ToLowerInvariant();
        var hasKeyword = RealEstateKeywords.Any(k => lower.Contains(k));
        var isTextType = TextMimeTypes.Any(t =>
            mimeType.Equals(t, StringComparison.OrdinalIgnoreCase));

        return hasKeyword && isTextType;
    }

    internal static string CategorizeFile(string name)
    {
        var lower = name.ToLowerInvariant();

        if (lower.Contains("cma") || lower.Contains("comparative") || lower.Contains("market analysis"))
            return "CMA";

        if (lower.Contains("contract") || lower.Contains("agreement") || lower.Contains("purchase"))
            return "Contract";

        if (lower.Contains("listing") || lower.Contains("seller"))
            return "Listing";

        if (lower.Contains("flyer") || lower.Contains("brochure") || lower.Contains("marketing"))
            return "Marketing";

        if (lower.Contains("disclosure") || lower.Contains("addendum") || lower.Contains("amendment"))
            return "Disclosure";

        return "Document";
    }

    internal static IReadOnlyList<string> ExtractUrls(string content)
    {
        var matches = Regex.Matches(content, @"https?://[^\s<>""']+");
        return matches
            .Select(m => m.Value.TrimEnd('.', ',', ')', ']'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
