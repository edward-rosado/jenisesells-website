using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Workers.Activation.EmailTransactionExtraction;

/// <summary>
/// Extracts structured transaction data (property, price, commission, clients, status)
/// from transaction-related emails using Claude.
///
/// Designed as a reusable worker — called from:
/// 1. Activation pipeline (Phase 1, after EmailFetch)
/// 2. Future email-checking feature (periodic Gmail polling)
///
/// Pure compute — calls IAnthropicClient only. No storage, no DataServices.
/// </summary>
public sealed class EmailTransactionExtractor(
    IAnthropicClient anthropicClient,
    ILogger<EmailTransactionExtractor> logger)
{
    private const int MaxEmailsToExtract = 20;
    private const int BatchSize = 5;
    private const int MaxTokens = 2048;
    private const string Model = "claude-sonnet-4-6";
    private const string Pipeline = "activation-email-extraction";

    internal const string ExtractionSystemPrompt =
        """
        You are a real estate email parser. Extract structured transaction data from each email.

        You will receive a batch of emails. For EACH email, extract the data and return a JSON array.

        Return ONLY a valid JSON array (no explanation, no markdown fences):
        [
          {
            "emailId":"<email ID>",
            "type":"<ListingAgreement|BuyerAgreement|PurchaseContract|Disclosure|ClosingStatement|Cma|Inspection|Appraisal|Other>",
            "date":"<ISO 8601 date from email context or null>",
            "clients":[{"name":"<full name>","role":"<Buyer|Seller|Both|Unknown>","email":"<or null>","phone":"<or null>"}],
            "property":{"address":"<street address>","city":"<or null>","state":"<state abbr or null>","zip":"<or null>"},
            "keyTerms":{"price":"<sale/list price string or null>","commission":"<string or null>","contingencies":["<description>"]},
            "inferredPath":"transactions/{address-slug}/{category}/{sanitized-subject}.eml",
            "agentIdentity":{"name":"<agent name or null>","brokerageName":"<or null>","licenseNumber":"<or null>","phone":"<or null>","email":"<or null>"},
            "language":"<en|es or null>",
            "transactionStatus":"<Pending|Active|Closed|Expired|Withdrawn or null>",
            "serviceAreas":["<city or county>"],
            "notes":"<MLS number, special conditions, key details or null>"
          }
        ]

        Rules:
        - Extract ALL clients mentioned (buyers, sellers, attorneys, other agents)
        - Extract property details even from partial mentions (e.g., "534 Jefferson" → address)
        - Look for price/commission in CMA emails, offer emails, closing emails
        - Infer transaction status from email context (e.g., "under contract" → Pending)
        - Skip emails with no transaction data — omit them from the array
        - Do not invent data. Only extract what is explicitly stated.
        """;

    /// <summary>
    /// Filters emails for transaction content, then extracts structured data via Claude.
    /// Returns DocumentExtraction records compatible with PDF extraction results.
    /// </summary>
    public async Task<IReadOnlyList<DocumentExtraction>> ExtractAsync(
        IReadOnlyList<EmailMessage> sentEmails,
        IReadOnlyList<EmailMessage> inboxEmails,
        CancellationToken ct)
    {
        // Filter for transaction-related emails
        var transactionEmails = sentEmails.Concat(inboxEmails)
            .Where(IsTransactionEmail)
            .OrderByDescending(e => e.Date)
            .Take(MaxEmailsToExtract)
            .ToList();

        if (transactionEmails.Count == 0)
        {
            logger.LogInformation("[EMAILTX-001] No transaction emails found to extract.");
            return [];
        }

        logger.LogInformation(
            "[EMAILTX-002] Found {Count} transaction emails to extract (from {Total} total).",
            transactionEmails.Count, sentEmails.Count + inboxEmails.Count);

        var allExtractions = new List<DocumentExtraction>();

        // Process in batches to stay within Claude token limits
        for (var i = 0; i < transactionEmails.Count; i += BatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = transactionEmails.Skip(i).Take(BatchSize).ToList();
            var results = await ExtractBatchAsync(batch, ct);
            allExtractions.AddRange(results);
        }

        logger.LogInformation(
            "[EMAILTX-003] Extracted {Count} transactions from {EmailCount} emails.",
            allExtractions.Count, transactionEmails.Count);

        return allExtractions;
    }

    /// <summary>
    /// Determines if an email is likely transaction-related based on keywords.
    /// </summary>
    internal static bool IsTransactionEmail(EmailMessage email)
    {
        var text = $"{email.Subject} {email.Body}";
        return TransactionKeywords.Any(kw =>
            text.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] TransactionKeywords =
    [
        "contract", "offer", "closing", "commission", "listing",
        "CMA", "comparative market", "home value", "appraisal",
        "disclosure", "addendum", "purchase", "under contract",
        "attorney review", "inspection", "title", "mortgage",
        "earnest money", "escrow", "settlement", "ALTA",
        "asking price", "list price", "sale price", "offer price",
        "pre-approval", "pre-qualification", "MLS",
        // Spanish equivalents
        "contrato", "oferta", "cierre", "comisión", "listado",
        "análisis comparativo", "tasación", "divulgación",
    ];

    private async Task<IReadOnlyList<DocumentExtraction>> ExtractBatchAsync(
        IReadOnlyList<EmailMessage> batch,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        foreach (var email in batch)
        {
            sb.AppendLine($"<email id=\"{email.Id}\">");
            sb.AppendLine($"Subject: {email.Subject}");
            sb.AppendLine($"From: {email.From}");
            sb.AppendLine($"To: {string.Join(", ", email.To)}");
            sb.AppendLine($"Date: {email.Date:yyyy-MM-dd}");
            sb.AppendLine($"Body:\n{email.Body[..Math.Min(email.Body.Length, 3000)]}");
            sb.AppendLine("</email>");
            sb.AppendLine();
        }

        try
        {
            var response = await anthropicClient.SendAsync(
                Model, ExtractionSystemPrompt, sb.ToString(), MaxTokens, Pipeline, ct);

            return ParseExtractionResponse(response.Content, batch, logger);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "[EMAILTX-020] Batch extraction failed for {Count} emails.", batch.Count);
            return [];
        }
    }

    /// <summary>
    /// Parses Claude's JSON array response into DocumentExtraction records.
    /// </summary>
    internal static IReadOnlyList<DocumentExtraction> ParseExtractionResponse(
        string json, IReadOnlyList<EmailMessage> sourceEmails, ILogger? logger = null)
    {
        var results = new List<DocumentExtraction>();

        // Strip markdown fences if present
        json = json.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            var lastFence = json.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                json = json[(firstNewline + 1)..lastFence].Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return results;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var emailId = item.TryGetProperty("emailId", out var eidProp) ? eidProp.GetString() : null;
                var email = emailId is not null
                    ? sourceEmails.FirstOrDefault(e => e.Id == emailId)
                    : null;

                var type = ParseDocumentType(
                    item.TryGetProperty("type", out var tProp) ? tProp.GetString() : null);
                var date = item.TryGetProperty("date", out var dProp) && dProp.ValueKind != JsonValueKind.Null
                    && dProp.GetString() is string ds && DateTime.TryParse(ds, out var dt) ? dt
                    : email?.Date;

                var clients = new List<ExtractedClient>();
                if (item.TryGetProperty("clients", out var cProp) && cProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in cProp.EnumerateArray())
                    {
                        var name = c.TryGetProperty("name", out var nProp) ? nProp.GetString() : null;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        var role = c.TryGetProperty("role", out var rProp) ? rProp.GetString() : "Unknown";
                        var cemail = c.TryGetProperty("email", out var eProp) && eProp.ValueKind != JsonValueKind.Null ? eProp.GetString() : null;
                        var cphone = c.TryGetProperty("phone", out var pProp) && pProp.ValueKind != JsonValueKind.Null ? pProp.GetString() : null;
                        clients.Add(new ExtractedClient(name, Enum.TryParse<ContactRole>(role, out var cr) ? cr : ContactRole.Unknown, cemail, cphone));
                    }
                }

                ExtractedProperty? property = null;
                if (item.TryGetProperty("property", out var ppProp) && ppProp.ValueKind == JsonValueKind.Object)
                {
                    var addr = ppProp.TryGetProperty("address", out var aProp) ? aProp.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(addr))
                    {
                        var city = ppProp.TryGetProperty("city", out var cyProp) && cyProp.ValueKind != JsonValueKind.Null ? cyProp.GetString() : null;
                        var state = ppProp.TryGetProperty("state", out var stProp) && stProp.ValueKind != JsonValueKind.Null ? stProp.GetString() : null;
                        var zip = ppProp.TryGetProperty("zip", out var zProp) && zProp.ValueKind != JsonValueKind.Null ? zProp.GetString() : null;
                        property = new ExtractedProperty(addr, city, state, zip);
                    }
                }

                ExtractedKeyTerms? keyTerms = null;
                if (item.TryGetProperty("keyTerms", out var ktProp) && ktProp.ValueKind == JsonValueKind.Object)
                {
                    var price = ktProp.TryGetProperty("price", out var prProp) && prProp.ValueKind != JsonValueKind.Null ? prProp.GetString() : null;
                    var comm = ktProp.TryGetProperty("commission", out var cmProp) && cmProp.ValueKind != JsonValueKind.Null ? cmProp.GetString() : null;
                    var contingencies = new List<string>();
                    if (ktProp.TryGetProperty("contingencies", out var cnProp) && cnProp.ValueKind == JsonValueKind.Array)
                        foreach (var cn in cnProp.EnumerateArray())
                            if (cn.GetString() is string cnVal && !string.IsNullOrWhiteSpace(cnVal))
                                contingencies.Add(cnVal);
                    if (price is not null || comm is not null || contingencies.Count > 0)
                        keyTerms = new ExtractedKeyTerms(price, comm, contingencies);
                }

                var inferredPath = item.TryGetProperty("inferredPath", out var ipProp) && ipProp.ValueKind != JsonValueKind.Null
                    ? ipProp.GetString() : null;

                ExtractedAgentIdentity? agentIdentity = null;
                if (item.TryGetProperty("agentIdentity", out var aiProp) && aiProp.ValueKind == JsonValueKind.Object)
                {
                    var aiName = aiProp.TryGetProperty("name", out var ainProp) && ainProp.ValueKind != JsonValueKind.Null ? ainProp.GetString() : null;
                    var aiBrok = aiProp.TryGetProperty("brokerageName", out var aibProp) && aibProp.ValueKind != JsonValueKind.Null ? aibProp.GetString() : null;
                    if (aiName is not null || aiBrok is not null)
                        agentIdentity = new ExtractedAgentIdentity(aiName, aiBrok,
                            aiProp.TryGetProperty("licenseNumber", out var ailProp) && ailProp.ValueKind != JsonValueKind.Null ? ailProp.GetString() : null,
                            aiProp.TryGetProperty("phone", out var aipProp) && aipProp.ValueKind != JsonValueKind.Null ? aipProp.GetString() : null,
                            aiProp.TryGetProperty("email", out var aieProp) && aieProp.ValueKind != JsonValueKind.Null ? aieProp.GetString() : null);
                }

                var language = item.TryGetProperty("language", out var lProp) && lProp.ValueKind != JsonValueKind.Null ? lProp.GetString() : null;
                var txStatus = item.TryGetProperty("transactionStatus", out var txProp) && txProp.ValueKind != JsonValueKind.Null ? txProp.GetString() : null;
                var serviceAreas = new List<string>();
                if (item.TryGetProperty("serviceAreas", out var saProp) && saProp.ValueKind == JsonValueKind.Array)
                    foreach (var sa in saProp.EnumerateArray())
                        if (sa.GetString() is string saVal && !string.IsNullOrWhiteSpace(saVal))
                            serviceAreas.Add(saVal);
                var notes = item.TryGetProperty("notes", out var noProp) && noProp.ValueKind != JsonValueKind.Null ? noProp.GetString() : null;

                var fileName = email is not null ? $"Email: {email.Subject}" : $"Email: {emailId}";

                results.Add(new DocumentExtraction(
                    emailId ?? Guid.NewGuid().ToString(), fileName, type, clients, property, date, keyTerms,
                    inferredPath, agentIdentity, language, txStatus,
                    serviceAreas.Count > 0 ? serviceAreas : null, notes));
            }
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex,
                "[EMAILTX-022] Failed to parse JSON extraction response from Claude ({JsonLength} chars).",
                json.Length);
        }

        return results;
    }

    private static DocumentType ParseDocumentType(string? value) =>
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
}
