using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Workers.Shared.Security;

namespace RealEstateStar.Workers.Activation.TeamScrape;

/// <summary>
/// Pure-compute worker that scrapes a brokerage team page and extracts
/// prospective agents (name, title, email, phone, bio, headshot URL).
///
/// All HTTP goes through SsrfGuard (SSRF protection).
/// All extracted text goes through HtmlTextExtractor (XSS sanitisation).
/// No storage, no DataServices — caller persists results.
/// </summary>
public sealed class TeamScrapeWorker(
    SsrfGuard ssrfGuard,
    ILogger<TeamScrapeWorker> logger)
{
    private const int MaxAgents = 50;
    private const int MaxBodyBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxRedirects = 3;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    // ── Selector patterns for common brokerage team page layouts ─────────────
    // Ordered from most-specific (structured CMS templates) to least-specific (generic).
    private static readonly string[] CardPatterns =
    [
        // WordPress / IDX themes
        @"<(?:div|article|li)[^>]*class=""[^""]*(?:team-member|agent-card|agent-item|agent-profile|staff-member|member-card|realtor-card|roster-item)[^""]*""[^>]*>(.*?)</(?:div|article|li)>",
        // "agents" grid (Squarespace, Wix, custom)
        @"<(?:div|article|li)[^>]*class=""[^""]*(?:agent|member|staff|realtor|person|broker)[^""]*""[^>]*>(.*?)</(?:div|article|li)>",
        // Schema.org Person markup
        @"<[^>]+itemtype=""https?://schema\.org/(?:Person|RealEstateAgent)""[^>]*>(.*?)</(?:div|article|section|li)>",
    ];

    // Name: look for h2/h3/h4/strong/p with common class hints, then fall back to first heading
    private static readonly Regex NamePattern = new(
        @"<(?:h[1-6]|strong|p)[^>]*class=""[^""]*(?:name|title|agent-name|member-name|staff-name)[^""]*""[^>]*>\s*(?:<[^>]+>)*([^<]{2,80})(?:</[^>]+>)*\s*</(?:h[1-6]|strong|p)>|" +
        @"<(?:h[2-5])[^>]*>(?:<[^>]+>)*\s*([A-Z][a-zA-Z'\-]{1,30}(?:\s+[A-Z][a-zA-Z'\-]{1,30}){1,4})\s*(?:</[^>]+>)*</(?:h[2-5])>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Title / designation (e.g. "REALTOR®", "Listing Agent", "Team Lead")
    private static readonly Regex TitlePattern = new(
        @"<(?:p|span|div)[^>]*class=""[^""]*(?:title|designation|role|position|job)[^""]*""[^>]*>\s*(?:<[^>]+>)*([^<]{2,100})(?:</[^>]+>)*\s*</(?:p|span|div)>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Email: mailto: link is the most reliable signal
    private static readonly Regex EmailPattern = new(
        @"href=""mailto:([^""]{5,100})""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Phone: tel: link preferred; fall back to formatted number inside the card
    private static readonly Regex PhonePattern = new(
        @"href=""tel:([^""]{7,20})""|" +
        @"(?<!\w)(\(?\d{3}\)?[\s.\-]?\d{3}[\s.\-]?\d{4})(?!\d)",
        RegexOptions.Compiled);

    // Bio snippet: first <p> inside a class hinting at bio/about/description, 50–400 chars
    private static readonly Regex BioPattern = new(
        @"<(?:p|div)[^>]*class=""[^""]*(?:bio|about|description|summary|blurb|excerpt)[^""]*""[^>]*>\s*(?:<[^>]+>)*([^<]{50,400})",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Headshot: <img> with class/alt hints; capture src attribute
    private static readonly Regex HeadshotPattern = new(
        @"<img[^>]*(?:class=""[^""]*(?:headshot|photo|avatar|portrait|agent-photo|member-photo|profile)[^""]*""|alt=""[^""]*(?:headshot|photo|avatar|portrait)[^""]*"")[^>]*src=""([^""]{10,500})""[^>]*/?>|" +
        @"<img[^>]*src=""([^""]{10,500})""[^>]*(?:class=""[^""]*(?:headshot|photo|avatar|portrait|agent-photo|member-photo|profile)[^""]*""|alt=""[^""]*(?:headshot|photo|avatar|portrait)[^""]*"")[^>]/?>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Scrapes a brokerage team page and returns up to <c>50</c> prospective agents.
    /// Never throws — returns an empty list on any fetch or parse failure.
    /// </summary>
    public async Task<IReadOnlyList<ProspectiveAgent>> ScrapeTeamPageAsync(
        string brokerageUrl,
        string accountId,
        CancellationToken ct)
    {
        if (!Uri.TryCreate(brokerageUrl, UriKind.Absolute, out var uri))
        {
            logger.LogWarning("[TEAM-001] Invalid URL for account {AccountId}: {Url}", accountId, brokerageUrl);
            return [];
        }

        var response = await ssrfGuard.SafeGetAsync(uri, MaxBodyBytes, RequestTimeout, MaxRedirects, ct);

        if (!response.Success || response.Body is null)
        {
            logger.LogWarning(
                "[TEAM-001] SSRF guard rejected or empty response for {Url}. Account: {AccountId}. Reason: {Error}",
                brokerageUrl, accountId, response.Error);
            return [];
        }

        var agents = ParseAgentCards(response.Body, brokerageUrl, accountId);

        logger.LogInformation(
            "[TEAM-010] Scraped {Count} agents from {Url} for account {AccountId}",
            agents.Count, brokerageUrl, accountId);

        return agents;
    }

    /// <summary>
    /// Parses agent cards from raw HTML.  Visible for testing.
    /// Never throws — returns whatever could be extracted.
    /// </summary>
    internal IReadOnlyList<ProspectiveAgent> ParseAgentCards(
        string html,
        string sourceUrl,
        string accountId)
    {
        var agents = new List<ProspectiveAgent>();
        var scrapedAt = DateTime.UtcNow;

        foreach (var pattern in CardPatterns)
        {
            if (agents.Count >= MaxAgents)
                break;

            try
            {
                var cardRegex = new Regex(
                    pattern,
                    RegexOptions.IgnoreCase | RegexOptions.Singleline,
                    TimeSpan.FromSeconds(5));

                var matches = cardRegex.Matches(html);
                if (matches.Count == 0)
                    continue;

                foreach (Match match in matches)
                {
                    if (agents.Count >= MaxAgents)
                        break;

                    var cardHtml = match.Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(cardHtml))
                        continue;

                    var agent = ExtractAgent(cardHtml, sourceUrl, accountId, scrapedAt);
                    if (agent is null)
                        continue;

                    // Deduplicate by name (same name across multiple pattern matches = same person)
                    if (!agents.Any(a => string.Equals(a.Name, agent.Name, StringComparison.OrdinalIgnoreCase)))
                        agents.Add(agent);
                }

                // If we found agents with this pattern, stop trying less-specific ones
                if (agents.Count > 0)
                    break;
            }
            catch (RegexMatchTimeoutException ex)
            {
                logger.LogWarning(ex,
                    "[TEAM-020] Regex timeout on card pattern for {Url}. Trying next pattern.",
                    sourceUrl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "[TEAM-021] Unexpected error parsing card pattern for {Url}. Trying next pattern.",
                    sourceUrl);
            }
        }

        return agents;
    }

    /// <summary>
    /// Extracts a single <see cref="ProspectiveAgent"/> from a card HTML fragment.
    /// Returns null if no recognisable name was found.
    /// </summary>
    private ProspectiveAgent? ExtractAgent(
        string cardHtml,
        string sourceUrl,
        string accountId,
        DateTime scrapedAt)
    {
        var name = ExtractName(cardHtml);
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return new ProspectiveAgent
        {
            AccountId = accountId,
            Name = name,
            Title = ExtractTitle(cardHtml),
            Email = ExtractEmail(cardHtml),
            Phone = ExtractPhone(cardHtml),
            BioSnippet = ExtractBio(cardHtml),
            HeadshotUrl = ExtractHeadshotUrl(cardHtml),
            ScrapedAt = scrapedAt,
            SourceUrl = sourceUrl,
        };
    }

    // ── Field extractors ─────────────────────────────────────────────────────

    internal static string? ExtractName(string cardHtml)
    {
        var match = NamePattern.Match(cardHtml);
        if (!match.Success)
            return null;

        // Group 1 = class-hinted heading; Group 2 = capitalised plain heading
        var raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return Sanitise(raw);
    }

    internal static string? ExtractTitle(string cardHtml)
    {
        var match = TitlePattern.Match(cardHtml);
        return match.Success ? Sanitise(match.Groups[1].Value) : null;
    }

    internal static string? ExtractEmail(string cardHtml)
    {
        var match = EmailPattern.Match(cardHtml);
        if (!match.Success)
            return null;

        var raw = HtmlTextExtractor.ToPlainText(match.Groups[1].Value).Trim();
        return raw.Contains('@') && raw.Length <= 254 ? raw : null;
    }

    internal static string? ExtractPhone(string cardHtml)
    {
        var match = PhonePattern.Match(cardHtml);
        if (!match.Success)
            return null;

        // Group 1 = tel: link; Group 2 = formatted number
        var raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        var plain = HtmlTextExtractor.ToPlainText(raw).Trim();
        return string.IsNullOrWhiteSpace(plain) ? null : plain;
    }

    internal static string? ExtractBio(string cardHtml)
    {
        var match = BioPattern.Match(cardHtml);
        return match.Success ? Sanitise(match.Groups[1].Value) : null;
    }

    internal static string? ExtractHeadshotUrl(string cardHtml)
    {
        var match = HeadshotPattern.Match(cardHtml);
        if (!match.Success)
            return null;

        var src = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        src = src.Trim();

        // Reject data URIs and obviously bad values
        if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        // Return as-is — caller (or activity) is responsible for downloading / validating
        return string.IsNullOrWhiteSpace(src) ? null : src;
    }

    /// <summary>
    /// Strips HTML tags and decodes entities, then trims.
    /// Returns null for empty results.
    /// </summary>
    private static string? Sanitise(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var plain = HtmlTextExtractor.ToPlainText(raw).Trim();
        return string.IsNullOrWhiteSpace(plain) ? null : plain;
    }
}
