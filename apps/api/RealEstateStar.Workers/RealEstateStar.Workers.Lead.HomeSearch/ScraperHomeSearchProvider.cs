using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.HomeSearch;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Workers.Lead.HomeSearch;

public class ScraperHomeSearchProvider(
    IAnthropicClient anthropicClient,
    IScraperClient scraperClient,
    Dictionary<string, string> sourceUrls,
    ILogger<ScraperHomeSearchProvider> logger) : IHomeSearchProvider
{
    private const string ClaudeModel = "claude-sonnet-4-6";
    private const int ClaudeMaxTokens = 4096;

    private const string CurationSystemPrompt = """
        You are a real estate search assistant. You will receive a list of home listings and buyer search criteria.
        Curate the top 5-10 listings that best match the buyer's needs. For each selected listing, add a
        personalized "Why this fits" note explaining why it matches the buyer's criteria.

        Return ONLY valid JSON (no markdown, no code fences) with this schema:
        [
          {
            "address": "<string>",
            "city": "<string>",
            "state": "<string>",
            "zip": "<string>",
            "price": <number>,
            "beds": <number>,
            "baths": <number>,
            "sqft": <number or null>,
            "whyThisFits": "<string>",
            "listingUrl": "<string or null>"
          }
        ]
        """;

    public async Task<List<Listing>> SearchAsync(HomeSearchCriteria criteria, CancellationToken ct)
    {
        logger.LogInformation("[HSP-001] Starting home search for area={Area}", criteria.Area);

        var searchTasks = sourceUrls.Select(kvp =>
            FetchFromSourceAsync(kvp.Key, BuildSearchUrl(kvp.Value, criteria), criteria, ct)).ToArray();

        var results = await Task.WhenAll(searchTasks);

        var allListings = results.SelectMany(r => r).ToList();
        logger.LogInformation("[HSP-002] Collected {Total} raw listings from all sources", allListings.Count);

        var deduplicated = Deduplicate(allListings);
        logger.LogInformation("[HSP-003] Deduplicated to {Count} unique listings", deduplicated.Count);

        if (deduplicated.Count == 0)
        {
            logger.LogWarning("[HSP-004] No listings found for area={Area}", criteria.Area);
            return [];
        }

        return await CurateWithClaudeAsync(deduplicated, criteria, ct);
    }

    private async Task<List<Listing>> FetchFromSourceAsync(
        string sourceName, string sourceUrl, HomeSearchCriteria criteria, CancellationToken ct)
    {
        try
        {
            logger.LogInformation("[HSP-010] Fetching {Source} listings from {Url}", sourceName, sourceUrl);

            var html = await scraperClient.FetchAsync(sourceUrl, $"home-search-{sourceName}", "home-search", ct);
            if (html is null) return [];

            var listings = ParseListings(html, criteria);
            logger.LogInformation("[HSP-011] Parsed {Count} listings from {Source}", listings.Count, sourceName);
            return listings;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[HSP-012] Source {Source} failed; continuing with other sources", sourceName);
            return [];
        }
    }

    internal static string BuildSearchUrl(string template, HomeSearchCriteria criteria)
    {
        var expanded = template
            .Replace("{area}", Uri.EscapeDataString(criteria.Area))
            .Replace("{minPrice}", criteria.MinPrice?.ToString() ?? "")
            .Replace("{maxPrice}", criteria.MaxPrice?.ToString() ?? "")
            .Replace("{minBeds}", criteria.MinBeds?.ToString() ?? "")
            .Replace("{minBaths}", criteria.MinBaths?.ToString() ?? "");

        // Split into base and query string, then drop params whose value is empty
        var questionMark = expanded.IndexOf('?');
        if (questionMark < 0) return expanded;

        var baseUrl = expanded[..questionMark];
        var queryPart = expanded[(questionMark + 1)..];

        var cleanParams = queryPart
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p =>
            {
                var eq = p.IndexOf('=');
                return eq >= 0 && !string.IsNullOrEmpty(p[(eq + 1)..]);
            });

        var cleanQuery = string.Join("&", cleanParams);
        return string.IsNullOrEmpty(cleanQuery) ? baseUrl : $"{baseUrl}?{cleanQuery}";
    }

    // Parses listing cards from scraped HTML. Real parsing would extract structured data from
    // JSON-LD / __NEXT_DATA__ script blocks; this stub returns an empty list until HTML patterns
    // are confirmed against live scraper output.
    internal static List<Listing> ParseListings(string html, HomeSearchCriteria criteria) => [];

    internal static List<Listing> Deduplicate(List<Listing> listings) =>
        listings
            .GroupBy(l => NormalizeAddress($"{l.Address},{l.City},{l.State}"))
            .Select(g => g.First())
            .ToList();

    internal static string NormalizeAddress(string address) =>
        address.Trim().ToUpperInvariant().Replace(".", "").Replace(",", " ").Replace("  ", " ");

    private async Task<List<Listing>> CurateWithClaudeAsync(
        List<Listing> listings, HomeSearchCriteria criteria, CancellationToken ct)
    {
        var listingsData = BuildListingsPrompt(listings, criteria);
        var userMessage = $"<search_criteria>\n{BuildCriteriaDescription(criteria)}\n</search_criteria>\n\n<listings>\n{listingsData}\n</listings>\n\nCurate the top listings and return ONLY valid JSON matching the schema.";

        logger.LogInformation("[HSP-020] Sending {Count} listings to Claude for curation", listings.Count);

        var response = await anthropicClient.SendAsync(ClaudeModel, CurationSystemPrompt, userMessage, ClaudeMaxTokens, "home-search", ct);

        logger.LogInformation("[HSP-021] Received curation response from Claude");
        HomeSearchDiagnostics.LlmTokensInput.Add(response.InputTokens);
        HomeSearchDiagnostics.LlmTokensOutput.Add(response.OutputTokens);

        return ParseCuratedListings(response.Content);
    }

    internal static string BuildCriteriaDescription(HomeSearchCriteria criteria)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Area: {criteria.Area}");
        if (criteria.MinPrice.HasValue) sb.AppendLine($"Min Price: ${criteria.MinPrice.Value:N0}");
        if (criteria.MaxPrice.HasValue) sb.AppendLine($"Max Price: ${criteria.MaxPrice.Value:N0}");
        if (criteria.MinBeds.HasValue) sb.AppendLine($"Min Beds: {criteria.MinBeds.Value}");
        if (criteria.MinBaths.HasValue) sb.AppendLine($"Min Baths: {criteria.MinBaths.Value}");
        return sb.ToString();
    }

    internal static string BuildListingsPrompt(List<Listing> listings, HomeSearchCriteria criteria)
    {
        var sb = new StringBuilder();
        foreach (var l in listings)
        {
            var sqft = l.Sqft.HasValue ? $" / {l.Sqft:N0} sqft" : "";
            sb.AppendLine($"- {l.Address}, {l.City}, {l.State} {l.Zip} | ${l.Price:N0} | {l.Beds}bd/{l.Baths}ba{sqft}{(l.ListingUrl is not null ? $" | {l.ListingUrl}" : "")}");
        }
        return sb.ToString();
    }

    internal static List<Listing> ParseCuratedListings(string json)
    {
        // Strip markdown code fences that Claude sometimes adds despite instructions
        var cleaned = json.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            if (firstNewline >= 0) cleaned = cleaned[(firstNewline + 1)..];
            var lastFence = cleaned.LastIndexOf("```");
            if (lastFence >= 0) cleaned = cleaned[..lastFence].TrimEnd();
        }

        var array = JsonDocument.Parse(cleaned).RootElement;
        var listings = new List<Listing>();

        foreach (var item in array.EnumerateArray())
        {
            var address = item.GetProperty("address").GetString() ?? "";
            var city = item.GetProperty("city").GetString() ?? "";
            var state = item.GetProperty("state").GetString() ?? "";
            var zip = item.GetProperty("zip").GetString() ?? "";
            var price = item.GetProperty("price").GetDecimal();
            var beds = item.GetProperty("beds").GetInt32();
            var baths = item.GetProperty("baths").GetDecimal();
            int? sqft = item.TryGetProperty("sqft", out var sqftEl) && sqftEl.ValueKind == JsonValueKind.Number
                ? sqftEl.GetInt32()
                : null;
            var whyThisFits = item.TryGetProperty("whyThisFits", out var why) ? why.GetString() : null;
            var listingUrl = item.TryGetProperty("listingUrl", out var urlEl) ? urlEl.GetString() : null;

            listings.Add(new Listing(address, city, state, zip, price, beds, baths, sqft, whyThisFits, listingUrl));
        }

        return listings;
    }
}
