using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public partial class ProfileScraperService(
    IHttpClientFactory httpClientFactory,
    string apiKey,
    string? scraperApiKey,
    ILogger<ProfileScraperService> logger) : IProfileScraper
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-haiku-4-5-20251001";
    private const int MaxTokens = 4096;

    // MED-1: Domain allowlist to prevent SSRF attacks
    private static readonly HashSet<string> AllowedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "realtor.com", "www.realtor.com",
        "zillow.com", "www.zillow.com",
        "redfin.com", "www.redfin.com",
        "coldwellbanker.com", "www.coldwellbanker.com",
        "century21.com", "www.century21.com",
        "kw.com", "www.kw.com",
        "compass.com", "www.compass.com",
        "bhhs.com", "www.bhhs.com",
        "sothebysrealty.com", "www.sothebysrealty.com",
        "weichert.com", "www.weichert.com",
    };

    private const string ExtractionPrompt = """
        You are extracting a real estate agent's full profile from their web page.
        Extract EVERYTHING you can find — this data will be used to auto-generate their website.
        Be thorough. Look in structured data (JSON-LD), visible text, and infer what you can.

        Return ONLY valid JSON (no markdown, no code fences) matching this schema:
        {
            "name": "string or null",
            "title": "string or null (e.g. 'REALTOR®', 'Broker Associate')",
            "tagline": "string or null (their personal slogan or motto if present)",
            "phone": "string or null",
            "email": "string or null",
            "photoUrl": "absolute URL string or null",
            "brokerage": "string or null",
            "brokerageLogoUrl": "absolute URL string or null",
            "licenseId": "string or null",
            "state": "US state abbreviation or null",
            "officeAddress": "string or null",
            "serviceAreas": ["string"] or null,
            "specialties": ["string"] or null (e.g. 'Buyer Agent', 'Listing Agent', 'Luxury Homes', 'First-Time Buyers')",
            "designations": ["string"] or null (e.g. 'CRS', 'ABR', 'SRES', 'GRI')",
            "languages": ["string"] or null,
            "bio": "string or null — extract the FULL bio text, not just first sentence",
            "yearsExperience": number or null,
            "homesSold": number or null (total career or recent count)",
            "avgRating": number or null (0-5 scale)",
            "reviewCount": number or null,
            "avgListPrice": number or null (average price if mentioned)",
            "primaryColor": "hex color or null — infer from brokerage brand (e.g. RE/MAX=#DC1C2E, Coldwell Banker=#012169, Century 21=#B5985A, Compass=#000000, KW=#B40101)",
            "accentColor": "hex color or null — complementary to primary",
            "websiteUrl": "their personal website URL if linked, or null",
            "facebookUrl": "string or null",
            "instagramUrl": "string or null",
            "linkedInUrl": "string or null",
            "testimonials": [{"reviewerName": "string or null", "text": "string", "rating": number or null, "date": "string or null"}] or null — extract up to 5 reviews,
            "recentSales": [{"address": "string", "price": number or null, "date": "string or null", "photoUrl": "string or null"}] or null — extract up to 5 recent sales
        }

        IMPORTANT:
        - For brand colors, use known brokerage brand colors if you recognize the brokerage.
        - Extract photo URLs as absolute URLs (include https://...).
        - For bio, get the COMPLETE text, not a truncated version.
        - If the page is not a real estate agent profile, return {"error": "not_agent_profile"}.
        """;

    // TODO: LOW-6 — Extract shared Anthropic API client into a common AnthropicClient service

    public async Task<ScrapedProfile?> ScrapeAsync(string url, CancellationToken ct)
    {
        // MED-1: Validate URL against domain allowlist and block private IPs
        var validationError = ValidateUrl(url);
        if (validationError is not null)
        {
            logger.LogWarning("Blocked profile scrape attempt: {Reason} for URL {Url}", validationError, url);
            return null;
        }

        var httpClient = httpClientFactory.CreateClient(nameof(ProfileScraperService));

        // Route through ScraperAPI when configured — handles JS rendering, proxies, and anti-bot
        var fetchUrl = !string.IsNullOrEmpty(scraperApiKey)
            ? $"https://api.scraperapi.com?api_key={scraperApiKey}&url={Uri.EscapeDataString(url)}&render=true"
            : url;

        logger.LogInformation("[SCRAPE-020] Fetching profile from {Host}, proxy={UseProxy}",
            new Uri(url).Host, !string.IsNullOrEmpty(scraperApiKey));

        string html;
        try
        {
            html = await httpClient.GetStringAsync(fetchUrl, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "[SCRAPE-021] Failed to fetch profile page from {Host}", new Uri(url).Host);
            return null;
        }

        var text = ExtractPageContent(html);
        if (string.IsNullOrWhiteSpace(text) || text.Length < 50)
        {
            logger.LogWarning("Page content too short to extract profile from {Host}", new Uri(url).Host);
            return null;
        }

        // Truncate to avoid huge context windows — but keep enough for reviews/sales
        if (text.Length > 15000)
            text = text[..15000];

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
        var httpClient = httpClientFactory.CreateClient(nameof(ProfileScraperService));
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
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("[SCRAPE-010] Anthropic API returned {StatusCode} for profile extraction. Body: {ErrorBody}",
                (int)response.StatusCode, errorBody);
            throw new HttpRequestException(
                $"[SCRAPE-010] Anthropic API returned {(int)response.StatusCode}: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(responseJson);
        var content = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? throw new InvalidOperationException("Empty response from Claude");

        // Claude sometimes wraps JSON in markdown code fences despite being told not to
        var cleanContent = content.Trim();
        if (cleanContent.StartsWith("```"))
        {
            var firstNewline = cleanContent.IndexOf('\n');
            if (firstNewline >= 0)
                cleanContent = cleanContent[(firstNewline + 1)..];
            if (cleanContent.EndsWith("```"))
                cleanContent = cleanContent[..^3];
            cleanContent = cleanContent.Trim();
        }

        var extracted = JsonDocument.Parse(cleanContent);
        var root = extracted.RootElement;

        if (root.TryGetProperty("error", out _))
            return null;

        return new ScrapedProfile
        {
            // Identity
            Name = GetStringOrNull(root, "name"),
            Title = GetStringOrNull(root, "title"),
            Tagline = GetStringOrNull(root, "tagline"),
            Phone = GetStringOrNull(root, "phone"),
            Email = GetStringOrNull(root, "email"),
            PhotoUrl = GetStringOrNull(root, "photoUrl"),

            // Brokerage & licensing
            Brokerage = GetStringOrNull(root, "brokerage"),
            BrokerageLogoUrl = GetStringOrNull(root, "brokerageLogoUrl"),
            LicenseId = GetStringOrNull(root, "licenseId"),
            State = GetStringOrNull(root, "state"),
            OfficeAddress = GetStringOrNull(root, "officeAddress"),

            // Service details
            ServiceAreas = GetStringArray(root, "serviceAreas"),
            Specialties = GetStringArray(root, "specialties"),
            Designations = GetStringArray(root, "designations"),
            Languages = GetStringArray(root, "languages"),
            Bio = GetStringOrNull(root, "bio"),

            // Stats
            YearsExperience = GetIntOrNull(root, "yearsExperience"),
            HomesSold = GetIntOrNull(root, "homesSold"),
            AvgRating = GetDoubleOrNull(root, "avgRating"),
            ReviewCount = GetIntOrNull(root, "reviewCount"),
            AvgListPrice = GetDoubleOrNull(root, "avgListPrice"),

            // Branding
            PrimaryColor = GetStringOrNull(root, "primaryColor"),
            AccentColor = GetStringOrNull(root, "accentColor"),

            // Social
            WebsiteUrl = GetStringOrNull(root, "websiteUrl"),
            FacebookUrl = GetStringOrNull(root, "facebookUrl"),
            InstagramUrl = GetStringOrNull(root, "instagramUrl"),
            LinkedInUrl = GetStringOrNull(root, "linkedInUrl"),

            // Testimonials
            Testimonials = root.TryGetProperty("testimonials", out var ts) && ts.ValueKind == JsonValueKind.Array
                ? ts.EnumerateArray().Select(t => new Testimonial
                {
                    ReviewerName = GetStringOrNull(t, "reviewerName"),
                    Text = GetStringOrNull(t, "text"),
                    Rating = GetDoubleOrNull(t, "rating"),
                    Date = GetStringOrNull(t, "date"),
                }).Where(t => t.Text is not null).ToArray()
                : null,

            // Recent sales
            RecentSales = root.TryGetProperty("recentSales", out var rs) && rs.ValueKind == JsonValueKind.Array
                ? rs.EnumerateArray().Select(s => new RecentSale
                {
                    Address = GetStringOrNull(s, "address"),
                    Price = GetDoubleOrNull(s, "price"),
                    Date = GetStringOrNull(s, "date"),
                    PhotoUrl = GetStringOrNull(s, "photoUrl"),
                }).Where(s => s.Address is not null).ToArray()
                : null,
        };
    }

    private static string? GetStringOrNull(JsonElement root, string prop)
        => root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static int? GetIntOrNull(JsonElement root, string prop)
        => root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetInt32() : null;

    private static double? GetDoubleOrNull(JsonElement root, string prop)
        => root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetDouble() : null;

    private static string[]? GetStringArray(JsonElement root, string prop)
        => root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Array
            ? el.EnumerateArray().Select(e => e.GetString()!).Where(s => s is not null).ToArray()
            : null;

    internal static string? ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return "Invalid URL format";

        if (uri.Scheme != Uri.UriSchemeHttps)
            return "Only HTTPS URLs are allowed";

        if (!AllowedDomains.Contains(uri.Host))
            return $"Domain '{uri.Host}' is not in the allowed list";

        // Block private/loopback IPs in case of DNS rebinding or direct IP URLs
        if (IPAddress.TryParse(uri.Host, out var ip))
        {
            if (IPAddress.IsLoopback(ip) || IsPrivateIp(ip))
                return "Private/loopback IP addresses are not allowed";
        }

        return null;
    }

    private static bool IsPrivateIp(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] switch
        {
            10 => true,
            172 => bytes[1] >= 16 && bytes[1] <= 31,
            192 => bytes[1] == 168,
            127 => true,
            169 => bytes[1] == 254,
            _ => false,
        };
    }

    internal static string ExtractPageContent(string html)
    {
        // Step 1: Extract structured data from script tags BEFORE stripping them
        var structured = new StringBuilder();

        // JSON-LD (schema.org) — often has agent name, phone, etc.
        // Content may be HTML-encoded (&quot; instead of ") so decode it
        foreach (Match m in JsonLdRegex().Matches(html))
            structured.AppendLine(HttpUtility.HtmlDecode(m.Groups[1].Value));

        // __NEXT_DATA__ (Next.js apps like Zillow)
        foreach (Match m in NextDataRegex().Matches(html))
            structured.AppendLine(HttpUtility.HtmlDecode(m.Groups[1].Value));

        // Step 2: Strip scripts/styles and HTML tags for visible text
        var noScripts = ScriptStyleRegex().Replace(html, " ");
        var noTags = TagRegex().Replace(noScripts, " ");
        var visibleText = WhitespaceRegex().Replace(noTags, " ").Trim();

        // Combine structured data + visible text for Claude to extract from
        if (structured.Length > 0)
            return $"[Structured Data]\n{structured}\n[Visible Text]\n{visibleText}";

        return visibleText;
    }

    [GeneratedRegex(@"<script[^>]*type=""application/ld\+json""[^>]*>([\s\S]*?)</script>", RegexOptions.IgnoreCase)]
    private static partial Regex JsonLdRegex();

    [GeneratedRegex(@"<script[^>]*id=""__NEXT_DATA__""[^>]*>([\s\S]*?)</script>", RegexOptions.IgnoreCase)]
    private static partial Regex NextDataRegex();

    [GeneratedRegex(@"<(script|style)[^>]*>[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
