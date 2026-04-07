using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Activities.Lead.ContactDetection;

/// <summary>
/// Extracts contacts from general inbox emails by sending batches of 40 to Claude Haiku.
/// Strips quoted reply chains and truncates bodies to 500 chars to minimize token cost.
/// Uses &lt;user-data&gt; tags to prevent prompt injection.
/// </summary>
internal sealed class EmailContactExtractor(
    IAnthropicClient anthropicClient,
    ILogger<EmailContactExtractor> logger)
{
    internal const int BatchSize = 40;
    internal const string Model = "claude-haiku-4-5";
    internal const int MaxTokens = 4096;
    internal const string Pipeline = "contact-detection";
    internal const int MaxBodyLength = 500;

    private const string SystemPrompt = """
        You are a contact extraction assistant for a real estate agent.
        Extract unique contacts (clients, buyers, sellers, prospects) from the provided emails.
        Only extract real people who appear to be clients or potential clients — not vendors, spam, or the agent themselves.

        Return a JSON array with this structure:
        [
          {
            "name": "Full Name",
            "email": "email@example.com or null",
            "phone": "phone number or null",
            "role": "Buyer|Seller|Both|Unknown"
          }
        ]

        Return only the JSON array, no other text.
        """;

    /// <summary>
    /// Batches the provided emails in groups of 20 and sends each batch to Claude Sonnet
    /// for contact extraction. Returns all extracted clients across all batches.
    /// </summary>
    internal async Task<IReadOnlyList<ExtractedClient>> ExtractAsync(
        IReadOnlyList<EmailMessage> emails,
        CancellationToken ct)
    {
        if (emails.Count == 0) return [];

        var results = new List<ExtractedClient>();
        var batches = emails
            .Select((email, index) => (email, index))
            .GroupBy(x => x.index / BatchSize)
            .Select(g => g.Select(x => x.email).ToList())
            .ToList();

        logger.LogInformation(
            "[CONTACT-EXTRACT-010] Processing {EmailCount} emails in {BatchCount} batches",
            emails.Count, batches.Count);

        foreach (var batch in batches)
        {
            ct.ThrowIfCancellationRequested();
            var extracted = await ExtractBatchAsync(batch, ct);
            results.AddRange(extracted);
        }

        logger.LogInformation(
            "[CONTACT-EXTRACT-090] Extracted {ContactCount} contacts from {EmailCount} emails",
            results.Count, emails.Count);

        return results;
    }

    private async Task<IReadOnlyList<ExtractedClient>> ExtractBatchAsync(
        List<EmailMessage> batch,
        CancellationToken ct)
    {
        var userMessage = BuildUserMessage(batch);

        try
        {
            var response = await anthropicClient.SendAsync(
                Model, SystemPrompt, userMessage, MaxTokens, Pipeline, ct);

            return ParseResponse(response.Content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "[CONTACT-EXTRACT-020] Failed to extract contacts from batch of {Count} emails",
                batch.Count);
            return [];
        }
    }

    internal static string BuildUserMessage(List<EmailMessage> batch)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Extract contacts from the following emails:");
        sb.AppendLine();

        for (var i = 0; i < batch.Count; i++)
        {
            var email = batch[i];
            sb.AppendLine($"Email {i + 1}:");
            sb.AppendLine($"From: {email.From}");
            sb.AppendLine($"Subject: {email.Subject}");
            // Use <user-data> tags to prevent prompt injection from email content
            sb.AppendLine("<user-data>");
            var body = StripQuotedReplies(email.Body);
            if (body.Length > MaxBodyLength)
                body = body[..MaxBodyLength];
            sb.AppendLine(body);
            sb.AppendLine("</user-data>");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Strips quoted reply chains from email bodies. Removes lines starting with '>'
    /// and "On ... wrote:" header lines that introduce quoted content.
    /// </summary>
    internal static string StripQuotedReplies(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;

        var lines = body.Split('\n');
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            // Skip quoted lines (lines starting with >)
            if (trimmed.StartsWith('>'))
                continue;

            // Skip "On ... wrote:" reply headers
            if (Regex.IsMatch(trimmed, @"^On\s+.+wrote:\s*$", RegexOptions.IgnoreCase))
                continue;

            sb.AppendLine(line);
        }

        return sb.ToString().TrimEnd();
    }

    internal static IReadOnlyList<ExtractedClient> ParseResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return [];

        try
        {
            // Strip markdown code fences if present
            var json = content.Trim();
            if (json.StartsWith("```"))
            {
                var start = json.IndexOf('\n') + 1;
                var end = json.LastIndexOf("```");
                if (end > start)
                    json = json[start..end].Trim();
            }

            var items = JsonSerializer.Deserialize<List<ContactDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (items is null) return [];

            return items
                .Where(dto => !string.IsNullOrWhiteSpace(dto.Name))
                .Select(dto => new ExtractedClient(
                    Name: dto.Name!.Trim(),
                    Role: ParseRole(dto.Role),
                    Email: string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
                    Phone: string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim()))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static ContactRole ParseRole(string? role) =>
        role?.ToLowerInvariant() switch
        {
            "buyer" => ContactRole.Buyer,
            "seller" => ContactRole.Seller,
            "both" => ContactRole.Both,
            _ => ContactRole.Unknown
        };

    private sealed class ContactDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Role { get; set; }
    }
}
