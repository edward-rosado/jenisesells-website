using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Workers.Activation.EmailClassification;

/// <summary>
/// Pure-compute worker that classifies emails into categories using a single lightweight Claude call.
/// Runs in Phase 1.5 — after email fetch, before synthesis workers.
/// Uses Haiku for cost efficiency (classification is a simple task).
/// </summary>
public sealed class EmailClassificationWorker(
    IAnthropicClient anthropicClient,
    IContentSanitizer sanitizer,
    ILogger<EmailClassificationWorker> logger)
{
    private const string Model = "claude-haiku-4-5";
    private const int MaxTokens = 4096;
    private const int PreviewLength = 200;
    private const string Pipeline = "activation.email-classification";

    private const string SystemPrompt = """
        You are an email classifier for a real estate agent's inbox.
        For each email, assign one or more categories from this list:
        Transaction, Marketing, FeeRelated, Compliance, LeadNurture, Negotiation, Personal, Administrative

        Category definitions:
        - Transaction: contract, offer, closing, listing agreement, purchase, disclosure, inspection, appraisal, MLS, escrow, settlement
        - Marketing: just listed, open house, market update, newsletter, buyer alert, price reduced, just sold
        - FeeRelated: commission, fee, split, earnest money, referral, listing agreement pricing
        - Compliance: equal housing, licensed, disclaimer, disclosure, confidential, fair housing, realtor
        - LeadNurture: follow-up, check-in, how are you, thinking of you, touch base, drip
        - Negotiation: counter-offer, price reduction, concession, terms, conditions, counter
        - Personal: birthday, holiday, thank you, congratulations, referral thank you
        - Administrative: showing confirmation, appointment, scheduling, calendar, meeting

        Also produce a corpus summary with counts per category, language distribution, and dominant tone.

        Output valid JSON only. No explanation, no markdown fences.

        JSON Schema:
        {
          "classifications": [
            {"id": "<email ID>", "categories": ["Transaction", "Negotiation"]}
          ],
          "summary": {
            "totalEmails": 200,
            "transactionCount": 45,
            "marketingCount": 12,
            "feeRelatedCount": 8,
            "complianceCount": 5,
            "leadNurtureCount": 20,
            "languageDistribution": {"en": 180, "es": 20},
            "dominantTone": "warm-professional",
            "averageEmailLength": "medium"
          }
        }
        """;

    public async Task<EmailClassificationResult> ClassifyAsync(
        EmailCorpus emailCorpus,
        CancellationToken ct)
    {
        var allEmails = emailCorpus.SentEmails.Concat(emailCorpus.InboxEmails).ToList();

        if (allEmails.Count == 0)
        {
            logger.LogInformation("[CLASSIFY-001] No emails to classify — returning empty result");
            return new EmailClassificationResult(
                [], new CorpusSummary(0, 0, 0, 0, 0, 0, new Dictionary<string, int>(), null, null));
        }

        logger.LogInformation("[CLASSIFY-002] Classifying {Count} emails", allEmails.Count);

        var userMessage = BuildCompactEmailList(allEmails);
        var response = await anthropicClient.SendAsync(
            Model, SystemPrompt, sanitizer.Sanitize(userMessage), MaxTokens, Pipeline, ct);

        logger.LogInformation(
            "[CLASSIFY-003] Classification complete: {InputTokens} in, {OutputTokens} out, {DurationMs}ms",
            response.InputTokens, response.OutputTokens, response.DurationMs);

        return ParseClassificationResponse(response.Content);
    }

    internal static string BuildCompactEmailList(IReadOnlyList<EmailMessage> emails)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[");

        for (var i = 0; i < emails.Count; i++)
        {
            var email = emails[i];
            var preview = email.Body.Length > PreviewLength
                ? email.Body[..PreviewLength]
                : email.Body;
            // Escape for JSON safety
            var escapedSubject = JsonEncodedText.Encode(email.Subject).ToString();
            var escapedPreview = JsonEncodedText.Encode(preview).ToString();

            sb.Append($"  {{\"id\": \"{email.Id}\", \"subject\": \"{escapedSubject}\", \"preview\": \"{escapedPreview}\"}}");
            if (i < emails.Count - 1)
                sb.Append(',');
            sb.AppendLine();
        }

        sb.AppendLine("]");
        return sb.ToString();
    }

    internal static EmailClassificationResult ParseClassificationResponse(string content)
    {
        // Strip markdown code fences if present
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];
            if (trimmed.EndsWith("```", StringComparison.Ordinal))
                trimmed = trimmed[..^3].TrimEnd();
        }

        using var doc = JsonDocument.Parse(trimmed);
        var root = doc.RootElement;

        var classifications = new List<ClassifiedEmail>();
        if (root.TryGetProperty("classifications", out var classArray))
        {
            foreach (var item in classArray.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString() ?? "";
                var categories = new List<EmailCategory>();
                if (item.TryGetProperty("categories", out var cats))
                {
                    foreach (var cat in cats.EnumerateArray())
                    {
                        if (Enum.TryParse<EmailCategory>(cat.GetString(), ignoreCase: true, out var parsed))
                            categories.Add(parsed);
                    }
                }
                classifications.Add(new ClassifiedEmail(id, categories));
            }
        }

        var summary = ParseSummary(root);
        return new EmailClassificationResult(classifications, summary);
    }

    private static CorpusSummary ParseSummary(JsonElement root)
    {
        if (!root.TryGetProperty("summary", out var s))
            return new CorpusSummary(0, 0, 0, 0, 0, 0, new Dictionary<string, int>(), null, null);

        var langDist = new Dictionary<string, int>();
        if (s.TryGetProperty("languageDistribution", out var lang))
        {
            foreach (var prop in lang.EnumerateObject())
            {
                if (prop.Value.TryGetInt32(out var count))
                    langDist[prop.Name] = count;
            }
        }

        return new CorpusSummary(
            TotalEmails: s.TryGetProperty("totalEmails", out var te) ? te.GetInt32() : 0,
            TransactionCount: s.TryGetProperty("transactionCount", out var tc) ? tc.GetInt32() : 0,
            MarketingCount: s.TryGetProperty("marketingCount", out var mc) ? mc.GetInt32() : 0,
            FeeRelatedCount: s.TryGetProperty("feeRelatedCount", out var fc) ? fc.GetInt32() : 0,
            ComplianceCount: s.TryGetProperty("complianceCount", out var cc) ? cc.GetInt32() : 0,
            LeadNurtureCount: s.TryGetProperty("leadNurtureCount", out var lnc) ? lnc.GetInt32() : 0,
            LanguageDistribution: langDist,
            DominantTone: s.TryGetProperty("dominantTone", out var dt) ? dt.GetString() : null,
            AverageEmailLength: s.TryGetProperty("averageEmailLength", out var ael) ? ael.GetString() : null);
    }
}
