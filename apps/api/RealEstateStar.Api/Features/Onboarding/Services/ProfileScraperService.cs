using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public partial class ProfileScraperService(
    HttpClient httpClient,
    string apiKey,
    ILogger<ProfileScraperService> logger) : IProfileScraper
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-haiku-4-5-20251001";
    private const int MaxTokens = 1024;

    private const string ExtractionPrompt = """
        Extract real estate agent profile data from the following page text.
        Return ONLY valid JSON (no markdown, no code fences) matching this schema:
        {
            "name": "string or null",
            "title": "string or null",
            "phone": "string or null",
            "email": "string or null",
            "brokerage": "string or null",
            "licenseId": "string or null",
            "state": "US state abbreviation or null",
            "officeAddress": "string or null",
            "serviceAreas": ["string"] or null,
            "bio": "string or null",
            "yearsExperience": number or null,
            "homesSold": number or null,
            "avgRating": number or null
        }
        If the page is not a real estate agent profile, return {"error": "not_agent_profile"}.
        """;

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

        // Truncate to avoid huge context windows
        if (text.Length > 8000)
            text = text[..8000];

        try
        {
            return await ExtractWithClaudeAsync(text, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Claude extraction failed for {Host}, returning partial profile", new Uri(url).Host);
            return new ScrapedProfile { Bio = $"Profile scraped from {url}" };
        }
    }

    private async Task<ScrapedProfile?> ExtractWithClaudeAsync(string pageText, CancellationToken ct)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            model = Model,
            max_tokens = MaxTokens,
            system = ExtractionPrompt,
            messages = new[]
            {
                new { role = "user", content = $"<page_text>\n{pageText}\n</page_text>" }
            }
        });

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(responseJson);
        var content = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? throw new InvalidOperationException("Empty response from Claude");

        var extracted = JsonDocument.Parse(content);
        var root = extracted.RootElement;

        if (root.TryGetProperty("error", out _))
            return null;

        return new ScrapedProfile
        {
            Name = GetStringOrNull(root, "name"),
            Title = GetStringOrNull(root, "title"),
            Phone = GetStringOrNull(root, "phone"),
            Email = GetStringOrNull(root, "email"),
            Brokerage = GetStringOrNull(root, "brokerage"),
            LicenseId = GetStringOrNull(root, "licenseId"),
            State = GetStringOrNull(root, "state"),
            OfficeAddress = GetStringOrNull(root, "officeAddress"),
            ServiceAreas = root.TryGetProperty("serviceAreas", out var sa) && sa.ValueKind == JsonValueKind.Array
                ? sa.EnumerateArray().Select(e => e.GetString()!).Where(s => s is not null).ToArray()
                : null,
            Bio = GetStringOrNull(root, "bio"),
            YearsExperience = root.TryGetProperty("yearsExperience", out var ye) && ye.ValueKind == JsonValueKind.Number ? ye.GetInt32() : null,
            HomesSold = root.TryGetProperty("homesSold", out var hs) && hs.ValueKind == JsonValueKind.Number ? hs.GetInt32() : null,
            AvgRating = root.TryGetProperty("avgRating", out var ar) && ar.ValueKind == JsonValueKind.Number ? ar.GetDouble() : null,
        };
    }

    private static string? GetStringOrNull(JsonElement root, string prop)
        => root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

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
