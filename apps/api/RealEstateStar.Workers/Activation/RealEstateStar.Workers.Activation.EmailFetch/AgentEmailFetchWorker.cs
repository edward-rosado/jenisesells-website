using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Services;

namespace RealEstateStar.Workers.Activation.EmailFetch;

/// <summary>
/// Phase 1 gather worker: fetches the agent's sent + inbox emails via Gmail,
/// extracts the email signature from sent mail, and strips noise (quoted replies,
/// boilerplate) from the corpus.
///
/// Pure compute — calls IGmailReader only. No storage, no DataServices.
/// </summary>
public sealed class AgentEmailFetchWorker(
    IGmailReader gmailReader,
    ILogger<AgentEmailFetchWorker> logger)
{
    private const int MaxResults = 100;

    public async Task<EmailCorpus> RunAsync(string accountId, string agentId, CancellationToken ct)
    {
        logger.LogInformation(
            "[EMAILFETCH-001] Starting email fetch for account {AccountId}, agent {AgentId}.",
            accountId, agentId);

        var sentTask = gmailReader.GetSentEmailsAsync(accountId, agentId, MaxResults, ct);
        var inboxTask = gmailReader.GetInboxEmailsAsync(accountId, agentId, MaxResults, ct);

        await Task.WhenAll(sentTask, inboxTask);

        var sentEmails = sentTask.Result;
        var inboxEmails = inboxTask.Result;

        logger.LogInformation(
            "[EMAILFETCH-002] Fetched {SentCount} sent + {InboxCount} inbox for account {AccountId}, agent {AgentId}.",
            sentEmails.Count, inboxEmails.Count, accountId, agentId);

        // Extract signature from sent emails
        var signature = ExtractSignature(sentEmails);

        // Strip noise from all emails
        var cleanSent = StripNoise(sentEmails, signature);
        var cleanInbox = StripNoise(inboxEmails, signature);

        // Detect language for each email
        var taggedSent = DetectLanguages(cleanSent);
        var taggedInbox = DetectLanguages(cleanInbox);

        var allTagged = taggedSent.Concat(taggedInbox).ToList();
        var distribution = allTagged
            .GroupBy(e => e.DetectedLocale ?? "en")
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}={g.Count()}");

        logger.LogInformation(
            "[EFETCH-010] Email language distribution for {AgentId}: {Distribution}",
            agentId, string.Join(", ", distribution));

        return new EmailCorpus(taggedSent, taggedInbox, signature);
    }

    /// <summary>
    /// Extracts a composite signature by parsing ALL candidate blocks from sent emails,
    /// then merging fields across them. This captures identity signals that appear in
    /// full client-facing signatures even when short office signatures are more frequent.
    ///
    /// For example, the agent's cell phone, name, and personal website might only appear
    /// in their full CMA email signature (20% of emails), while the short office signature
    /// (80% of quick replies) only has the office phone and brokerage name.
    /// </summary>
    internal static EmailSignature? ExtractSignature(IReadOnlyList<EmailMessage> sentEmails)
    {
        if (sentEmails.Count == 0)
            return null;

        // Collect candidate signature blocks from each sent email
        var candidates = new List<string>();
        foreach (var email in sentEmails)
        {
            var block = ExtractSignatureBlock(email.Body);
            if (!string.IsNullOrWhiteSpace(block))
                candidates.Add(block.Trim());
        }

        if (candidates.Count == 0)
            return null;

        // Parse EVERY candidate block, then merge the richest fields across all of them.
        // This captures the agent's name from their full signature even if their short
        // office signature is more common.
        var parsed = candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(ParseSignature)
            .ToList();

        return MergeSignatures(parsed);
    }

    /// <summary>
    /// Merges multiple parsed signatures by picking the best (most frequent non-null) value
    /// for each field. For name and phone, prefers the most frequently seen value.
    /// For fields like websiteUrl, any non-null value wins.
    /// </summary>
    internal static EmailSignature MergeSignatures(IReadOnlyList<EmailSignature> signatures)
    {
        if (signatures.Count == 0)
            return new EmailSignature(null, null, null, null, null, [], null, null, null);
        if (signatures.Count == 1)
            return signatures[0];

        // For each field, pick the most frequently seen non-null value
        var name = MostFrequent(signatures.Select(s => s.Name));
        var title = MostFrequent(signatures.Select(s => s.Title));
        var phone = MostFrequent(signatures.Select(s => s.Phone));
        var licenseNumber = MostFrequent(signatures.Select(s => s.LicenseNumber));
        var brokerageName = MostFrequent(signatures.Select(s => s.BrokerageName));
        var headshotUrl = signatures.Select(s => s.HeadshotUrl).FirstOrDefault(u => u is not null);
        var websiteUrl = signatures.Select(s => s.WebsiteUrl).FirstOrDefault(u => u is not null);
        var logoUrl = signatures.Select(s => s.LogoUrl).FirstOrDefault(u => u is not null);
        var socialLinks = signatures
            .SelectMany(s => s.SocialLinks)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new EmailSignature(name, title, phone, licenseNumber, brokerageName,
            socialLinks, headshotUrl, websiteUrl, logoUrl);
    }

    /// <summary>
    /// Returns the most frequently occurring non-null value from a sequence of candidates.
    /// </summary>
    private static string? MostFrequent(IEnumerable<string?> values)
    {
        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key.Length) // prefer longer on tie (e.g., full name vs first name)
            .FirstOrDefault()?.Key;
    }

    /// <summary>
    /// Extracts the trailing signature block from an email body.
    /// Looks for a "-- " separator line or common closing phrases.
    /// </summary>
    internal static string? ExtractSignatureBlock(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var lines = body.Split('\n');

        // Look for RFC 2822 signature separator "-- "
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].Trim() == "--")
            {
                return string.Join("\n", lines.Skip(i + 1)).Trim();
            }
        }

        // Look for common email closings in the last 20 lines
        var searchLines = lines.TakeLast(Math.Min(20, lines.Length)).ToArray();
        var closingPatterns = new[]
        {
            @"^(best regards|regards|sincerely|thanks|thank you|warm regards|kind regards|cheers)",
        };

        for (var i = 0; i < searchLines.Length; i++)
        {
            var trimmed = searchLines[i].Trim();
            if (closingPatterns.Any(p =>
                Regex.IsMatch(trimmed, p, RegexOptions.IgnoreCase)))
            {
                // Return everything from the closing line onward as signature block
                var startLine = lines.Length - searchLines.Length + i;
                return string.Join("\n", lines.Skip(startLine)).Trim();
            }
        }

        // Fallback: last 5 lines if they look like contact info
        var lastFive = lines.TakeLast(Math.Min(5, lines.Length)).ToArray();
        var blockText = string.Join("\n", lastFive).Trim();
        if (LooksLikeSignature(blockText))
            return blockText;

        return null;
    }

    private static bool LooksLikeSignature(string text)
    {
        return Regex.IsMatch(text, @"\d{3}[\s\-\.\(\)]+\d{3}[\s\-\.]+\d{4}") // phone
            || Regex.IsMatch(text, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}") // email
            || Regex.IsMatch(text, @"https?://") // URL
            || Regex.IsMatch(text, @"(REALTOR|Realty|Realtors|Properties|Real Estate)", RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Parses a raw signature block text into structured <see cref="EmailSignature"/> fields.
    /// </summary>
    internal static EmailSignature ParseSignature(string block)
    {
        var lines = block.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        string? name = null;
        string? title = null;
        string? phone = null;
        string? licenseNumber = null;
        string? brokerageName = null;
        string? headshotUrl = null;
        string? websiteUrl = null;
        string? logoUrl = null;
        var socialLinks = new List<string>();

        foreach (var line in lines)
        {
            // Phone number
            if (phone is null)
            {
                var phoneMatch = Regex.Match(line, @"(\(?\d{3}\)?[\s\-\.]?\d{3}[\s\-\.]\d{4})");
                if (phoneMatch.Success)
                {
                    phone = phoneMatch.Value.Trim();
                    continue;
                }
            }

            // License number
            if (licenseNumber is null)
            {
                var licenseMatch = Regex.Match(line,
                    @"(?:lic(?:ense)?\.?|license\s*#|#)\s*([A-Z]{0,3}\d{4,})",
                    RegexOptions.IgnoreCase);
                if (licenseMatch.Success)
                {
                    licenseNumber = licenseMatch.Groups[1].Value.Trim();
                    continue;
                }
            }

            // URLs (with https:// prefix)
            var urlMatch = Regex.Match(line, @"https?://\S+");
            if (urlMatch.Success)
            {
                var url = urlMatch.Value.TrimEnd('.', ',', ')');
                if (IsSocialUrl(url))
                    socialLinks.Add(url);
                else if (websiteUrl is null)
                    websiteUrl = url;
                continue;
            }

            // Bare domain URLs without https:// (e.g., "jenisesellsnj.com")
            if (websiteUrl is null)
            {
                var bareDomainMatch = Regex.Match(line, @"^([a-zA-Z0-9][a-zA-Z0-9\-]*\.(com|net|org|io|co|us|realty|realtor|homes))$",
                    RegexOptions.IgnoreCase);
                if (bareDomainMatch.Success)
                {
                    websiteUrl = $"https://www.{bareDomainMatch.Value}";
                    continue;
                }
            }

            // Image URLs (logo/headshot in img tags or standalone)
            var imgMatch = Regex.Match(line, @"src=[""']?(https?://\S+?)[""'\s>]", RegexOptions.IgnoreCase);
            if (imgMatch.Success)
            {
                var imgUrl = imgMatch.Groups[1].Value;
                if (headshotUrl is null && IsHeadshotUrl(imgUrl))
                    headshotUrl = imgUrl;
                else if (logoUrl is null)
                    logoUrl = imgUrl;
                continue;
            }

            // Brokerage name (lines containing real estate keywords)
            if (brokerageName is null && Regex.IsMatch(line,
                @"(Realty|Realtors|Properties|Real Estate|Brokerage)",
                RegexOptions.IgnoreCase))
            {
                brokerageName = line;
                continue;
            }

            // Title line — may also contain name (e.g., "Jenise Buckalew, REALTOR®")
            if (title is null && Regex.IsMatch(line,
                @"(REALTOR|Agent|Broker|Associate|Consultant|Specialist|Advisor)",
                RegexOptions.IgnoreCase))
            {
                // Try to split name from title: "Name, REALTOR®" or "Name | Agent" or "Name - Broker"
                var titleSplitMatch = Regex.Match(line,
                    @"^(.+?)\s*[,|\-–—]\s*(REALTOR|Agent|Broker|Associate|Consultant|Specialist|Advisor).*$",
                    RegexOptions.IgnoreCase);
                if (titleSplitMatch.Success)
                {
                    var candidateName = titleSplitMatch.Groups[1].Value.Trim();
                    if (name is null && LooksLikeName(candidateName))
                        name = candidateName;
                    title = line;
                }
                else
                {
                    title = line;
                }
                continue;
            }

            // Name: first non-empty, non-contact line that looks like a name
            if (name is null && LooksLikeName(line))
                name = line;
        }

        return new EmailSignature(
            name,
            title,
            phone,
            licenseNumber,
            brokerageName,
            socialLinks,
            headshotUrl,
            websiteUrl,
            logoUrl);
    }

    private static bool IsSocialUrl(string url) =>
        Regex.IsMatch(url, @"(facebook\.com|twitter\.com|linkedin\.com|instagram\.com|x\.com)",
            RegexOptions.IgnoreCase);

    private static bool IsHeadshotUrl(string url) =>
        Regex.IsMatch(url, @"(headshot|photo|profile|avatar|portrait)",
            RegexOptions.IgnoreCase);

    /// <summary>
    /// Common email sign-off phrases that start with a capital letter and look like names
    /// but are not. Only includes actual closing salutations — NOT identity signals like
    /// "Se Habla Español" or brand taglines like "Forward. Moving." which belong in the signature.
    /// </summary>
    private static readonly HashSet<string> SignOffPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "Best Regards", "Warm Regards", "Kind Regards", "Regards",
        "Sincerely", "Sincerely Yours", "Thanks", "Thank You", "Many Thanks",
        "Cheers", "Best", "Best Wishes", "Respectfully", "Cordially",
        "All the best", "Looking forward", "Talk soon", "Take care",
        "See you soon", "With appreciation", "Warmly",
    };

    private static bool LooksLikeName(string line) =>
        line.Length > 2
        && line.Length < 60
        && !line.Contains('@')
        && !line.Contains('/')
        && !line.Contains(':')
        && Regex.IsMatch(line, @"^[A-Z][a-z]")
        && !SignOffPhrases.Contains(line.TrimEnd('.', ',', '!'));

    /// <summary>
    /// Removes quoted reply blocks, "On ... wrote:" headers, and boilerplate from email bodies.
    /// Also strips signature blocks when a signature is known.
    /// </summary>
    internal static IReadOnlyList<EmailMessage> StripNoise(
        IReadOnlyList<EmailMessage> emails,
        EmailSignature? signature)
    {
        var cleaned = new List<EmailMessage>(emails.Count);

        foreach (var email in emails)
        {
            var body = StripQuotedReplies(email.Body);
            var signatureBlock = ExtractSignatureBlock(body);
            var cleanBody = signatureBlock is null ? body : body[..^signatureBlock.Length].TrimEnd();

            cleaned.Add(email with
            {
                Body = cleanBody,
                SignatureBlock = signatureBlock
            });
        }

        return cleaned;
    }

    /// <summary>
    /// Detects language for each email using the character-set heuristic
    /// and returns new records with DetectedLocale populated.
    /// </summary>
    internal static IReadOnlyList<EmailMessage> DetectLanguages(IReadOnlyList<EmailMessage> emails)
    {
        var result = new List<EmailMessage>(emails.Count);
        foreach (var email in emails)
        {
            var locale = LanguageDetector.DetectLocale(email.Subject + " " + email.Body);
            result.Add(email with { DetectedLocale = locale });
        }
        return result;
    }

    /// <summary>
    /// Strips quoted reply content ("> " lines and "On DATE, PERSON wrote:" blocks).
    /// </summary>
    internal static string StripQuotedReplies(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return body;

        var lines = body.Split('\n');
        var result = new List<string>();
        var inQuotedBlock = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // "On DATE, PERSON wrote:" — marks start of quoted block
            if (Regex.IsMatch(line, @"^On .+ wrote:$", RegexOptions.Singleline) ||
                Regex.IsMatch(line, @"^-{3,}\s*Original Message\s*-{3,}", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(line, @"^From:\s*.+\nSent:\s*.+\nTo:\s*.+", RegexOptions.Singleline))
            {
                inQuotedBlock = true;
                continue;
            }

            // Lines starting with ">" are quoted
            if (line.TrimStart().StartsWith('>'))
            {
                inQuotedBlock = true;
                continue;
            }

            if (inQuotedBlock)
            {
                // Empty line after quoted block may return to original
                if (string.IsNullOrWhiteSpace(line))
                {
                    // Check if next non-empty line starts with ">"
                    var nextQuoted = lines.Skip(i + 1)
                        .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
                        ?.TrimStart().StartsWith('>') ?? false;

                    if (!nextQuoted)
                        inQuotedBlock = false;

                    continue;
                }
                continue;
            }

            result.Add(line);
        }

        return string.Join("\n", result).TrimEnd();
    }
}
