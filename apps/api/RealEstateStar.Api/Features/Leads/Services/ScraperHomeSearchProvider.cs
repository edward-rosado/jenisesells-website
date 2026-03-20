using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Leads.Services;

public class ScraperHomeSearchProvider(
    IHttpClientFactory httpClientFactory,
    string scraperApiKey,
    string claudeApiKey,
    ILogger<ScraperHomeSearchProvider>? logger = null) : IHomeSearchProvider
{
    private const string ScraperApiClientName = "ScraperAPI";
    private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
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
        logger?.LogInformation("[HSP-001] Starting home search for area={Area}", criteria.Area);

        var searchTasks = new[]
        {
            FetchFromSourceAsync("Zillow",  BuildZillowUrl(criteria),  criteria, ct),
            FetchFromSourceAsync("Redfin",  BuildRedfinUrl(criteria),  criteria, ct),
            FetchFromSourceAsync("MLS",     BuildMlsUrl(criteria),     criteria, ct)
        };

        var results = await Task.WhenAll(searchTasks);

        var allListings = results.SelectMany(r => r).ToList();
        logger?.LogInformation("[HSP-002] Collected {Total} raw listings from all sources", allListings.Count);

        var deduplicated = Deduplicate(allListings);
        logger?.LogInformation("[HSP-003] Deduplicated to {Count} unique listings", deduplicated.Count);

        if (deduplicated.Count == 0)
        {
            logger?.LogWarning("[HSP-004] No listings found for area={Area}", criteria.Area);
            return [];
        }

        return await CurateWithClaudeAsync(deduplicated, criteria, ct);
    }

    private async Task<List<Listing>> FetchFromSourceAsync(
        string sourceName, string sourceUrl, HomeSearchCriteria criteria, CancellationToken ct)
    {
        try
        {
            var scraperUrl = BuildScraperUrl(sourceUrl);
            logger?.LogInformation("[HSP-010] Fetching {Source} listings from {Url}", sourceName, sourceUrl);

            var httpClient = httpClientFactory.CreateClient(ScraperApiClientName);
            var html = await httpClient.GetStringAsync(scraperUrl, ct);

            var listings = ParseListings(html, criteria);
            logger?.LogInformation("[HSP-011] Parsed {Count} listings from {Source}", listings.Count, sourceName);
            return listings;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[HSP-012] Source {Source} failed; continuing with other sources", sourceName);
            return [];
        }
    }

    internal string BuildScraperUrl(string targetUrl) =>
        $"https://api.scraperapi.com/?api_key={scraperApiKey}&url={Uri.EscapeDataString(targetUrl)}&render=true";

    internal static string BuildZillowUrl(HomeSearchCriteria criteria)
    {
        var area = Uri.EscapeDataString(criteria.Area);
        var url = $"https://www.zillow.com/homes/{area}_rb/";

        var filters = new List<string>();
        if (criteria.MinPrice.HasValue) filters.Add($"price-min={criteria.MinPrice.Value}");
        if (criteria.MaxPrice.HasValue) filters.Add($"price-max={criteria.MaxPrice.Value}");
        if (criteria.MinBeds.HasValue)  filters.Add($"beds-min={criteria.MinBeds.Value}");
        if (criteria.MinBaths.HasValue) filters.Add($"baths-min={criteria.MinBaths.Value}");

        return filters.Count > 0 ? $"{url}?{string.Join("&", filters)}" : url;
    }

    internal static string BuildRedfinUrl(HomeSearchCriteria criteria)
    {
        var area = Uri.EscapeDataString(criteria.Area.Replace(" ", "-").ToLowerInvariant());
        var url = $"https://www.redfin.com/city/{area}";

        var filters = new List<string>();
        if (criteria.MinPrice.HasValue) filters.Add($"min_price={criteria.MinPrice.Value}");
        if (criteria.MaxPrice.HasValue) filters.Add($"max_price={criteria.MaxPrice.Value}");
        if (criteria.MinBeds.HasValue)  filters.Add($"num_beds={criteria.MinBeds.Value}");
        if (criteria.MinBaths.HasValue) filters.Add($"num_baths={criteria.MinBaths.Value}");

        return filters.Count > 0 ? $"{url}?{string.Join("&", filters)}" : url;
    }

    internal static string BuildMlsUrl(HomeSearchCriteria criteria)
    {
        var area = Uri.EscapeDataString(criteria.Area);
        var url = $"https://www.realtor.com/realestateandhomes-search/{area}";

        var filters = new List<string>();
        if (criteria.MinPrice.HasValue) filters.Add($"price-min={criteria.MinPrice.Value}");
        if (criteria.MaxPrice.HasValue) filters.Add($"price-max={criteria.MaxPrice.Value}");
        if (criteria.MinBeds.HasValue)  filters.Add($"beds-min={criteria.MinBeds.Value}");
        if (criteria.MinBaths.HasValue) filters.Add($"baths-min={criteria.MinBaths.Value}");

        return filters.Count > 0 ? $"{url}?{string.Join("&", filters)}" : url;
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

        var requestBody = JsonSerializer.Serialize(new
        {
            model = ClaudeModel,
            max_tokens = ClaudeMaxTokens,
            system = CurationSystemPrompt,
            messages = new[]
            {
                new { role = "user", content = $"<search_criteria>\n{BuildCriteriaDescription(criteria)}\n</search_criteria>\n\n<listings>\n{listingsData}\n</listings>\n\nCurate the top listings and return ONLY valid JSON matching the schema." }
            }
        });

        var request = new HttpRequestMessage(HttpMethod.Post, ClaudeApiUrl)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", claudeApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        logger?.LogInformation("[HSP-020] Sending {Count} listings to Claude for curation", listings.Count);

        var httpClient = httpClientFactory.CreateClient(nameof(ScraperHomeSearchProvider));
        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        logger?.LogInformation("[HSP-021] Received curation response from Claude");

        var doc = JsonDocument.Parse(responseJson);
        var content = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? throw new InvalidOperationException("[HSP-022] Empty response from Claude API");

        return ParseCuratedListings(content);
    }

    internal static string BuildCriteriaDescription(HomeSearchCriteria criteria)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Area: {criteria.Area}");
        if (criteria.MinPrice.HasValue) sb.AppendLine($"Min Price: ${criteria.MinPrice.Value:N0}");
        if (criteria.MaxPrice.HasValue) sb.AppendLine($"Max Price: ${criteria.MaxPrice.Value:N0}");
        if (criteria.MinBeds.HasValue)  sb.AppendLine($"Min Beds: {criteria.MinBeds.Value}");
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
            var city    = item.GetProperty("city").GetString() ?? "";
            var state   = item.GetProperty("state").GetString() ?? "";
            var zip     = item.GetProperty("zip").GetString() ?? "";
            var price   = item.GetProperty("price").GetDecimal();
            var beds    = item.GetProperty("beds").GetInt32();
            var baths   = item.GetProperty("baths").GetDecimal();
            int? sqft   = item.TryGetProperty("sqft", out var sqftEl) && sqftEl.ValueKind == JsonValueKind.Number
                ? sqftEl.GetInt32()
                : null;
            var whyThisFits = item.TryGetProperty("whyThisFits", out var why) ? why.GetString() : null;
            var listingUrl  = item.TryGetProperty("listingUrl", out var urlEl) ? urlEl.GetString() : null;

            listings.Add(new Listing(address, city, state, zip, price, beds, baths, sqft, whyThisFits, listingUrl));
        }

        return listings;
    }
}
