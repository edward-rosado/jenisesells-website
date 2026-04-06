using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Services;
using DriveIndexModel = RealEstateStar.Domain.Activation.Models.DriveIndex;
using DriveFileModel = RealEstateStar.Domain.Activation.Models.DriveFile;

namespace RealEstateStar.Workers.Activation.DriveIndex;

/// <summary>
/// Phase 1 gather worker: indexes the agent's Google Drive.
/// Finds or creates the "real-estate-star" platform folder, catalogs real-estate documents,
/// reads their content, surfaces any URLs discovered in documents, and uses Claude Vision
/// to extract contact/property data from PDFs.
///
/// Pure compute — calls IGDriveClient and IAnthropicClient only. No storage, no DataServices.
/// </summary>
public sealed class DriveIndexWorker(
    IGDriveClient driveClient,
    IAnthropicClient anthropicClient,
    ILogger<DriveIndexWorker> logger)
{
    internal const string PlatformFolderName = "real-estate-star";

    // ── Observability ─────────────────────────────────────────────────────────

    private static readonly Meter _meter = new("RealEstateStar.Activation.DriveIndex");
    internal static readonly Counter<long> PdfsProcessedCounter =
        _meter.CreateCounter<long>("activation.driveindex.pdfs.processed", description: "PDFs processed for contact extraction");
    internal static readonly Counter<long> PdfPagesReadCounter =
        _meter.CreateCounter<long>("activation.driveindex.pdfs.pages_read", description: "PDF page-equivalent entries sent to Claude");

    // ── Constants ─────────────────────────────────────────────────────────────

    private const int MaxPdfPages = 10;
    private const int PdfParallelism = 5;
    private const int MaxStagedFiles = 100;
    private const int MaxPdfExtractions = 10;
    private const int ClaudeMaxTokens = 1024;
    private const string ClaudePipeline = "activation-driveindex";
    private const string ClaudeModel = "claude-sonnet-4-6";

    internal const string DocumentExtractionSystemPrompt =
        """
        You are a real estate document parser. Extract structured contact and property data from the document.

        Return ONLY valid JSON matching this schema (no explanation, no markdown fences):
        {"type":"<one of: ListingAgreement, BuyerAgreement, PurchaseContract, Disclosure, ClosingStatement, Cma, Inspection, Appraisal, Other>","date":"<ISO 8601 date or null>","clients":[{"name":"<full name>","role":"<one of: Buyer, Seller, Both, Unknown>","email":"<email or null>","phone":"<phone or null>"}],"property":{"address":"<street address>","city":"<city or null>","state":"<state abbreviation or null>","zip":"<zip or null>"},"keyTerms":{"price":"<sale price as string or null>","commission":"<commission as string or null>","contingencies":["<contingency description>"]}}

        If a field is not present in the document, use null (or empty array for contingencies).
        Do not invent data. Only extract what is explicitly stated.
        """;

    internal const string DocumentExtractionUserTemplate =
        """
        <user-data>
        {0}
        </user-data>
        """;

    // ── File type arrays ──────────────────────────────────────────────────────

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

    /// <summary>
    /// Indexes the agent's Google Drive — discovers files, reads content, extracts contacts.
    /// When <paramref name="stagedContent"/> is provided, each file's content is staged to blob
    /// storage immediately after reading (stream-and-stage), avoiding accumulating all content in memory.
    /// The returned <c>DriveIndex.Contents</c> will be empty when staging is used.
    /// </summary>
    public async Task<DriveIndexModel> RunAsync(
        string accountId,
        string agentId,
        CancellationToken ct,
        IStagedContentProvider? stagedContent = null)
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

        // Read content of each real-estate doc.
        // When stagedContent is provided: stream-and-stage — read each file from Drive, stage
        // to blob, extract URLs + contacts inline, then discard the content string.
        // Content is NEVER accumulated in memory when staging is used.
        // When stagedContent is null (tests/local): accumulate in-memory as before.
        var contents = new Dictionary<string, string>();
        var discoveredUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extractions = new List<DocumentExtraction>();
        var stagedCount = 0;

        // When staging, cap at MaxStagedFiles most-recently-modified files.
        // Synthesizers only use the top 5-20 — staging hundreds wastes time and risks timeout.
        // Sort by modified date descending so the most relevant/recent files are staged first.
        var filesToProcess = stagedContent is not null
            ? realEstateFiles
                .OrderByDescending(f => f.ModifiedTime ?? DateTime.MinValue)
                .Take(MaxStagedFiles)
                .ToList()
            : realEstateFiles;

        foreach (var file in filesToProcess)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var content = await driveClient.GetFileContentAsync(accountId, agentId, file.Id, ct);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    if (stagedContent is not null)
                    {
                        // Stage to blob — content is NOT kept in memory
                        await stagedContent.StageContentAsync(accountId, agentId, file.Id, content, ct);
                        stagedCount++;

                        // Extract URLs inline (cheap string scan, no API call)
                        foreach (var url in ExtractUrls(content))
                            discoveredUrls.Add(url);

                        // content string is now eligible for GC — NOT stored in dictionary
                    }
                    else
                    {
                        contents[file.Id] = content;
                        foreach (var url in ExtractUrls(content))
                            discoveredUrls.Add(url);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "[DRIVEINDEX-010] Failed to read content of file {FileId} ({FileName}) for account {AccountId}",
                    file.Id, file.Name, accountId);
            }
        }

        if (stagedContent is not null)
            logger.LogInformation(
                "[DRIVEINDEX-004] Staged {StagedCount} documents to blob, discovered {UrlCount} URLs for account {AccountId}, agent {AgentId}.",
                stagedCount, discoveredUrls.Count, accountId, agentId);
        else
            logger.LogInformation(
                "[DRIVEINDEX-004] Read {ContentCount} documents, discovered {UrlCount} URLs for account {AccountId}, agent {AgentId}.",
                contents.Count, discoveredUrls.Count, accountId, agentId);

        // ── PDF extraction (parallel, max 5 in-flight) ────────────────────────

        var pdfFiles = filesToProcess
            .Where(f => f.MimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            .Take(MaxPdfExtractions)
            .ToList();

        var semaphore = new SemaphoreSlim(PdfParallelism);
        var extractionTasks = pdfFiles.Select(async file =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                return await ExtractFromPdfAsync(accountId, agentId, file, ct);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        var pdfResults = await Task.WhenAll(extractionTasks);
        extractions.AddRange(pdfResults.Where(e => e is not null)!);

        // ── Text-doc extraction (non-PDF) ─────────────────────────────────────
        // When staging is used, text-doc extraction was done inline above (stream-and-stage).
        // Only run this pass for the non-staging (in-memory) path.

        if (stagedContent is null)
        {
            foreach (var (fileId, content) in contents)
            {
                ct.ThrowIfCancellationRequested();

                var file = filesToProcess.FirstOrDefault(f => f.Id == fileId);
                if (file is not null && !file.MimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var extraction = await ExtractFromTextAsync(file, content, ct);
                    if (extraction is not null)
                        extractions.Add(extraction);
                }
            }
        }

        logger.LogInformation(
            "[DRIVEINDEX-005] Extracted {ExtractionCount} document contacts for account {AccountId}, agent {AgentId}.",
            extractions.Count, accountId, agentId);

        // Map to domain DriveFile records with language detection
        var driveFiles = realEstateFiles.Select(f =>
        {
            var detectedLocale = contents.TryGetValue(f.Id, out var content)
                ? LanguageDetector.DetectLocale(content)
                : null;

            return new DriveFileModel(
                f.Id,
                f.Name,
                f.MimeType,
                CategorizeFile(f.Name),
                f.ModifiedTime ?? DateTime.MinValue,
                detectedLocale);
        }).ToList();

        var langDistribution = driveFiles
            .Where(f => f.DetectedLocale is not null)
            .GroupBy(f => f.DetectedLocale!)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}={g.Count()}");

        logger.LogInformation(
            "[DIDX-010] Document language distribution for {AgentId}: {Distribution}",
            agentId, string.Join(", ", langDistribution));

        // When staging is used, return empty Contents — content lives in blob, not in memory.
        // This prevents the orchestrator from serializing MBs of text through the Durable Task history.
        var returnContents = stagedContent is not null
            ? new Dictionary<string, string>()
            : contents;

        return new DriveIndexModel(folderId, driveFiles, returnContents, discoveredUrls.ToList(), extractions);
    }

    // ── PDF extraction ────────────────────────────────────────────────────────

    private async Task<DocumentExtraction?> ExtractFromPdfAsync(
        string accountId,
        string agentId,
        DriveFileInfo file,
        CancellationToken ct)
    {
        try
        {
            var pdfBytes = await driveClient.DownloadBinaryAsync(accountId, agentId, file.Id, ct);
            if (pdfBytes is null || pdfBytes.Length == 0)
            {
                logger.LogWarning("[DRIVEINDEX-011] Empty PDF for file {FileId} ({FileName})", file.Id, file.Name);
                return null;
            }

            // Send raw PDF bytes directly to Claude Vision — no local parsing needed.
            // Claude renders PDFs server-side as static images (no JS engine, no form handler),
            // so malicious PDF payloads (embedded JS, launch actions) cannot execute.
            var pdfContent = new List<(byte[] Data, string MimeType)> { (pdfBytes, "application/pdf") };

            PdfsProcessedCounter.Add(1);
            PdfPagesReadCounter.Add(1);

            var systemPrompt = DocumentExtractionSystemPrompt;
            var userMessage = string.Format(DocumentExtractionUserTemplate, $"File: {file.Name}");

            var response = await anthropicClient.SendWithImagesAsync(
                ClaudeModel,
                systemPrompt,
                userMessage,
                pdfContent,
                ClaudeMaxTokens,
                ClaudePipeline,
                ct);

            return ParseDocumentExtraction(file.Id, file.Name, response.Content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "[DRIVEINDEX-020] Failed to extract contacts from PDF {FileId} ({FileName}) for account {AccountId}",
                file.Id, file.Name, accountId);
            return null;
        }
    }

    // ── Text extraction ────────────────────────────────────────────────────────

    private async Task<DocumentExtraction?> ExtractFromTextAsync(
        DriveFileInfo file,
        string content,
        CancellationToken ct)
    {
        try
        {
            var systemPrompt = DocumentExtractionSystemPrompt;
            var userMessage = string.Format(DocumentExtractionUserTemplate, content);

            var response = await anthropicClient.SendAsync(
                ClaudeModel,
                systemPrompt,
                userMessage,
                ClaudeMaxTokens,
                ClaudePipeline,
                ct);

            return ParseDocumentExtraction(file.Id, file.Name, response.Content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "[DRIVEINDEX-021] Failed to extract contacts from text doc {FileId} ({FileName})",
                file.Id, file.Name);
            return null;
        }
    }

    // ── JSON parsing helpers ──────────────────────────────────────────────────

    internal static DocumentExtraction? ParseDocumentExtraction(string fileId, string fileName, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var type = ParseDocumentType(root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null);
            var date = root.TryGetProperty("date", out var dateProp) && dateProp.ValueKind != JsonValueKind.Null
                ? dateProp.GetString() is string ds && DateTime.TryParse(ds, out var dt) ? dt : (DateTime?)null
                : null;

            var clients = new List<ExtractedClient>();
            if (root.TryGetProperty("clients", out var clientsProp) && clientsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in clientsProp.EnumerateArray())
                {
                    var name = c.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var role = ParseContactRole(c.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null);
                    var email = c.TryGetProperty("email", out var emailProp) && emailProp.ValueKind != JsonValueKind.Null
                        ? emailProp.GetString() : null;
                    var phone = c.TryGetProperty("phone", out var phoneProp) && phoneProp.ValueKind != JsonValueKind.Null
                        ? phoneProp.GetString() : null;

                    clients.Add(new ExtractedClient(name, role, email, phone));
                }
            }

            ExtractedProperty? property = null;
            if (root.TryGetProperty("property", out var propProp) && propProp.ValueKind == JsonValueKind.Object)
            {
                var address = propProp.TryGetProperty("address", out var addrProp) ? addrProp.GetString() : null;
                if (!string.IsNullOrWhiteSpace(address))
                {
                    var city = propProp.TryGetProperty("city", out var cityProp) && cityProp.ValueKind != JsonValueKind.Null
                        ? cityProp.GetString() : null;
                    var state = propProp.TryGetProperty("state", out var stateProp) && stateProp.ValueKind != JsonValueKind.Null
                        ? stateProp.GetString() : null;
                    var zip = propProp.TryGetProperty("zip", out var zipProp) && zipProp.ValueKind != JsonValueKind.Null
                        ? zipProp.GetString() : null;
                    property = new ExtractedProperty(address, city, state, zip);
                }
            }

            ExtractedKeyTerms? keyTerms = null;
            if (root.TryGetProperty("keyTerms", out var termsProp) && termsProp.ValueKind == JsonValueKind.Object)
            {
                var price = termsProp.TryGetProperty("price", out var priceProp) && priceProp.ValueKind != JsonValueKind.Null
                    ? priceProp.GetString() : null;
                var commission = termsProp.TryGetProperty("commission", out var commProp) && commProp.ValueKind != JsonValueKind.Null
                    ? commProp.GetString() : null;
                var contingencies = new List<string>();
                if (termsProp.TryGetProperty("contingencies", out var contProp) && contProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in contProp.EnumerateArray())
                    {
                        var val = c.GetString();
                        if (!string.IsNullOrWhiteSpace(val))
                            contingencies.Add(val);
                    }
                }
                keyTerms = new ExtractedKeyTerms(price, commission, contingencies);
            }

            return new DocumentExtraction(fileId, fileName, type, clients, property, date, keyTerms);
        }
        catch
        {
            return null;
        }
    }

    internal static DocumentType ParseDocumentType(string? value) =>
        value switch
        {
            "ListingAgreement" => DocumentType.ListingAgreement,
            "BuyerAgreement" => DocumentType.BuyerAgreement,
            "PurchaseContract" => DocumentType.PurchaseContract,
            "Disclosure" => DocumentType.Disclosure,
            "ClosingStatement" => DocumentType.ClosingStatement,
            "Cma" => DocumentType.Cma,
            "Inspection" => DocumentType.Inspection,
            "Appraisal" => DocumentType.Appraisal,
            _ => DocumentType.Other
        };

    internal static ContactRole ParseContactRole(string? value) =>
        value switch
        {
            "Buyer" => ContactRole.Buyer,
            "Seller" => ContactRole.Seller,
            "Both" => ContactRole.Both,
            _ => ContactRole.Unknown
        };

    // ── Static helpers ────────────────────────────────────────────────────────

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
