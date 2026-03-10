using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public partial class ProfileScraperService(
    HttpClient httpClient,
    ILogger<ProfileScraperService> logger) : IProfileScraper
{
    public async Task<ScrapedProfile?> ScrapeAsync(string url, CancellationToken ct)
    {
        string html;
        try
        {
            html = await httpClient.GetStringAsync(url, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to fetch profile page from {Host}", new Uri(url).Host);
            return null;
        }

        var text = StripHtml(html);
        if (string.IsNullOrWhiteSpace(text) || text.Length < 50)
        {
            logger.LogWarning("Page content too short to extract profile from {Host}", new Uri(url).Host);
            return null;
        }

        // TODO: Send text to Claude API for structured extraction (Task 20 wires Claude).
        // For now return a stub profile with the URL source.
        return new ScrapedProfile { Bio = $"Profile scraped from {url}" };
    }

    private static string StripHtml(string html)
    {
        var noScripts = ScriptStyleRegex().Replace(html, " ");
        var noTags = TagRegex().Replace(noScripts, " ");
        return WhitespaceRegex().Replace(noTags, " ").Trim();
    }

    [GeneratedRegex(@"<(script|style)[^>]*>[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
