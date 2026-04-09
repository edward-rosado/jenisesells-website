using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using AgentDiscoveryModel = RealEstateStar.Domain.Activation.Models.AgentDiscovery;

namespace RealEstateStar.Workers.Activation.AgentDiscovery;

/// <summary>
/// Phase 1 gather worker: discovers the agent's web presence, downloads their headshot
/// and brokerage logo, scrapes third-party profiles (Zillow, Realtor.com), extracts GA4
/// measurement IDs, and checks WhatsApp registration status.
///
/// Pure compute — calls IOAuthRefresher, IHttpClientFactory, IScraperClient,
/// IZillowReviewsClient, IGoogleReviewsClient, and IWhatsAppSender only.
/// No storage, no DataServices.
/// </summary>
public sealed class AgentDiscoveryWorker(
    IOAuthRefresher oAuthRefresher,
    IHttpClientFactory httpClientFactory,
    IScraperClient scraperClient,
    IZillowReviewsClient zillowClient,
    IGoogleReviewsClient googleReviewsClient,
    IWhatsAppSender whatsAppSender,
    ILogger<AgentDiscoveryWorker> logger)
{
    private const string UserAgent =
        "Mozilla/5.0 (compatible; RealEstateStar-Activation/1.0; +https://real-estate-star.com)";

    private static readonly string[] ThirdPartyDomains =
    [
        "zillow.com", "realtor.com", "homes.com", "trulia.com", "redfin.com"
    ];

    /// <summary>
    /// Known third-party platform domains — NOT the agent's own website.
    /// Third-party platforms have standardized templates; the agent's own site has their real brand.
    /// </summary>
    private static readonly string[] AllThirdPartyDomains =
    [
        "zillow.com", "realtor.com", "redfin.com", "trulia.com", "homes.com",
        "facebook.com", "instagram.com", "linkedin.com", "twitter.com", "x.com",
        "youtube.com", "tiktok.com", "google.com", "yelp.com",
        "coldwellbanker.com", "century21.com", "remax.com", "kw.com",
        "compass.com", "sothebysrealty.com"
    ];

    /// <summary>
    /// Determines if a URL is the agent's own website (not a third-party platform).
    /// Third-party platforms have standardized templates — the agent's own site has their real brand.
    /// </summary>
    internal static bool IsAgentOwnWebsite(string url)
    {
        var lower = url.ToLowerInvariant();
        return !AllThirdPartyDomains.Any(d => lower.Contains(d));
    }

    public async Task<AgentDiscoveryModel> RunAsync(
        string accountId,
        string agentId,
        string agentName,
        string brokerageName,
        string? phoneNumber,
        EmailSignature? emailSignature,
        string? emailHandle,
        string? agentEmail,
        IReadOnlyList<string>? discoveredUrls,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[AGENTDISCOVERY-001] Starting discovery for account {AccountId}, agent {AgentId}, name={AgentName}.",
            accountId, agentId, agentName);

        // Fetch headshot + logo in parallel
        var headshotTask = DownloadHeadshotAsync(accountId, agentId, ct);
        var logoTask = DownloadLogoAsync(emailSignature, ct);

        // Discover websites via email sig, domain guessing, agent-site, discovered URLs, and third-party profiles
        var websiteUrls = BuildWebsiteSearchUrls(agentName, brokerageName, emailSignature, emailHandle, agentId, discoveredUrls);

        // Log whether we found an agent-owned website for branding extraction
        var ownWebsite = websiteUrls.FirstOrDefault(u => u.Source == "OwnWebsite");
        if (ownWebsite != default)
        {
            logger.LogInformation(
                "[DISCOVERY-050] Found agent's own website: {Url}", ownWebsite.Url);
        }
        else
        {
            logger.LogInformation(
                "[DISCOVERY-051] No agent website found, falling back to third-party profiles");
        }

        // Fetch all discovered websites (ScraperAPI for third-party, direct HTTP for own sites)
        var websiteTask = FetchWebsitesAsync(websiteUrls, agentId, ct);

        // Fetch reviews from Zillow API + Google Places API in parallel — both are cheap, structured.
        var zillowReviewsTask = FetchZillowReviewsAsync(agentEmail, agentName, agentId, ct);
        var googleSearchQuery = $"{agentName} {brokerageName}".Trim();
        var googleReviewsTask = FetchGoogleReviewsAsync(googleSearchQuery, agentId, ct);

        // Check WhatsApp
        var whatsAppTask = CheckWhatsAppAsync(phoneNumber, ct);

        await Task.WhenAll(headshotTask, logoTask, websiteTask, zillowReviewsTask, googleReviewsTask, whatsAppTask);

        var headshot = headshotTask.Result;
        var logo = logoTask.Result;
        var websites = websiteTask.Result;
        var zillowReviews = zillowReviewsTask.Result;
        var googleReviews = googleReviewsTask.Result;
        var whatsAppEnabled = whatsAppTask.Result;

        // Parse third-party profiles from fetched websites (bio, sales count, specialties — not reviews)
        var profiles = ParseThirdPartyProfiles(websites);

        // Merge reviews from all sources: Zillow API + Google Places + HTML-scraped
        var allReviews = new List<Review>();
        allReviews.AddRange(zillowReviews.Reviews);
        allReviews.AddRange(googleReviews.Reviews);
        if (allReviews.Count == 0)
            allReviews.AddRange(profiles.SelectMany(p => p.Reviews));

        // Extract GA4 measurement ID from agent-owned websites
        var ga4Id = ExtractGa4MeasurementId(websites);

        // Extract phone from Google profile if not already known
        var resolvedPhone = phoneNumber ?? ExtractPhoneFromSignature(emailSignature);

        logger.LogInformation(
            "[AGENTDISCOVERY-002] Discovery complete for account {AccountId}, agent {AgentId}. " +
            "Websites: {WebsiteCount}, Profiles: {ProfileCount}, Reviews: {ReviewCount}, GA4: {Ga4Id}, WhatsApp: {WhatsApp}",
            accountId, agentId, websites.Count, profiles.Count, allReviews.Count, ga4Id ?? "none", whatsAppEnabled);

        return new AgentDiscoveryModel(
            HeadshotBytes: headshot,
            LogoBytes: logo,
            Phone: resolvedPhone,
            Websites: websites,
            Reviews: allReviews,
            Profiles: profiles,
            Ga4MeasurementId: ga4Id,
            WhatsAppEnabled: whatsAppEnabled);
    }

    private async Task<byte[]?> DownloadHeadshotAsync(string accountId, string agentId, CancellationToken ct)
    {
        try
        {
            var credential = await oAuthRefresher.GetValidCredentialAsync(accountId, agentId, ct);
            if (credential is null)
            {
                logger.LogWarning(
                    "[AGENTDISCOVERY-010] No OAuth credential available for headshot download. Account: {AccountId}",
                    accountId);
                return null;
            }

            var http = httpClientFactory.CreateClient("AgentDiscovery");
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credential.AccessToken);

            // Call Google People API to get profile photo URL
            var peopleResponse = await http.GetAsync(
                "https://people.googleapis.com/v1/people/me?personFields=photos", ct);

            if (!peopleResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "[AGENTDISCOVERY-011] People API returned {Status} for account {AccountId}",
                    peopleResponse.StatusCode, accountId);
                return null;
            }

            var json = await peopleResponse.Content.ReadAsStringAsync(ct);
            var photoUrl = ExtractPhotoUrl(json);

            if (photoUrl is null)
                return null;

            if (!IsAllowedUrl(photoUrl))
            {
                logger.LogWarning(
                    "[AGENTDISCOVERY-051] Headshot URL rejected by SSRF filter: {Url}", photoUrl);
                return null;
            }

            var photoResponse = await http.GetAsync(photoUrl, ct);
            if (!photoResponse.IsSuccessStatusCode)
                return null;

            var bytes = await photoResponse.Content.ReadAsByteArrayAsync(ct);
            logger.LogInformation(
                "[AGENTDISCOVERY-003] Downloaded headshot ({Bytes} bytes) for account {AccountId}, agent {AgentId}.",
                bytes.Length, accountId, agentId);
            return bytes;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "[AGENTDISCOVERY-030] Headshot download failed for account {AccountId}, agent {AgentId}.",
                accountId, agentId);
            return null;
        }
    }

    internal static string? ExtractPhotoUrl(string peopleApiJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(peopleApiJson);
            var photos = doc.RootElement.GetProperty("photos");
            foreach (var photo in photos.EnumerateArray())
            {
                if (photo.TryGetProperty("url", out var urlEl))
                    return urlEl.GetString();
            }
        }
        catch
        {
            // Ignore malformed JSON
        }
        return null;
    }

    /// <summary>
    /// Validates a URL before fetching to prevent SSRF attacks.
    /// Only HTTPS URLs to public internet hosts are allowed.
    /// </summary>
    internal static bool IsAllowedUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) return false;
        var host = uri.Host.ToLowerInvariant();
        if (host is "localhost" or "127.0.0.1" or "0.0.0.0" or "::1") return false;
        if (host.StartsWith("169.254.")) return false; // link-local
        if (host.StartsWith("10.")) return false;      // RFC 1918
        if (host.StartsWith("192.168.")) return false; // RFC 1918
        if (host.StartsWith("172.") &&
            host.Split('.').Length >= 2 &&
            int.TryParse(host.Split('.')[1], out var second) &&
            second >= 16 && second <= 31) return false; // RFC 1918 172.16-31.x.x
        if (host.StartsWith("fc00:") || host.StartsWith("fd")) return false; // IPv6 ULA
        return true;
    }

    private async Task<byte[]?> DownloadLogoAsync(EmailSignature? signature, CancellationToken ct)
    {
        var logoUrl = signature?.LogoUrl;
        if (logoUrl is null)
            return null;

        if (!IsAllowedUrl(logoUrl))
        {
            logger.LogWarning(
                "[AGENTDISCOVERY-050] Logo URL rejected by SSRF filter: {Url}", logoUrl);
            return null;
        }

        try
        {
            var http = httpClientFactory.CreateClient("AgentDiscovery");
            var response = await http.GetAsync(logoUrl, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            logger.LogInformation(
                "[AGENTDISCOVERY-004] Downloaded logo ({Bytes} bytes) from {Url}.",
                bytes.Length, logoUrl);
            return bytes;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "[AGENTDISCOVERY-031] Logo download failed from {Url}.", logoUrl);
            return null;
        }
    }

    /// <summary>
    /// Builds search URLs: agent's own website first (for real branding), then third-party profiles
    /// (for reviews/stats). The agent's own site has their real brand colors, fonts, and messaging.
    /// Zillow/Realtor.com give you the platform's template with the agent's name — not real branding.
    /// </summary>
    internal static IReadOnlyList<(string Url, string Source)> BuildWebsiteSearchUrls(
        string agentName,
        string brokerageName,
        EmailSignature? signature,
        string? emailHandle = null,
        string? agentSiteHandle = null,
        IReadOnlyList<string>? discoveredUrls = null)
    {
        var ownWebsiteUrls = new List<(string, string)>();
        var thirdPartyUrls = new List<(string, string)>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Discovered URLs from email bodies and Drive documents — these are the agent's REAL
        // profile URLs (e.g., zillow.com/profile/jenisebuck), not guessed from their name.
        // Prefer these over any guessed URLs.
        var hasDiscoveredZillow = false;
        if (discoveredUrls is not null)
        {
            foreach (var url in discoveredUrls)
            {
                if (seenUrls.Contains(url)) continue;
                seenUrls.Add(url);

                if (IsAgentOwnWebsite(url))
                    ownWebsiteUrls.Add((url, "Discovered"));
                else
                {
                    thirdPartyUrls.Add((url, "Discovered"));
                    if (url.Contains("zillow.com", StringComparison.OrdinalIgnoreCase))
                        hasDiscoveredZillow = true;
                }
            }
        }

        // Check email signature URL — if it's the agent's own website, prioritize it
        if (!string.IsNullOrWhiteSpace(signature?.WebsiteUrl) && !seenUrls.Contains(signature.WebsiteUrl))
        {
            seenUrls.Add(signature.WebsiteUrl);
            if (IsAgentOwnWebsite(signature.WebsiteUrl))
                ownWebsiteUrls.Add((signature.WebsiteUrl, "OwnWebsite"));
            else
                thirdPartyUrls.Add((signature.WebsiteUrl, "EmailSignature"));
        }

        // Domain guessing from email handle (e.g., jenisesellsnj@gmail.com → jenisesellsnj.com)
        if (!string.IsNullOrWhiteSpace(emailHandle))
        {
            var guessedUrl = $"https://www.{emailHandle.ToLowerInvariant()}.com";
            if (Regex.IsMatch(emailHandle.ToLowerInvariant(), @"^[a-z0-9][a-z0-9\-]{2,}$") &&
                !seenUrls.Contains(guessedUrl))
            {
                ownWebsiteUrls.Add((guessedUrl, "DomainGuess"));
                seenUrls.Add(guessedUrl);
            }
        }

        // Agent-site URL (e.g., jenise-buckalew.real-estate-star.com)
        if (!string.IsNullOrWhiteSpace(agentSiteHandle))
        {
            var agentSiteUrl = $"https://{agentSiteHandle}.real-estate-star.com";
            if (!seenUrls.Contains(agentSiteUrl))
                ownWebsiteUrls.Add((agentSiteUrl, "AgentSite"));
        }

        // Only guess the Zillow URL if we didn't discover a real one from emails/docs.
        // Guessed URLs (from agent name) are often wrong — Zillow usernames are user-chosen.
        if (!hasDiscoveredZillow)
        {
            var zillowSlug = agentName.Trim().Replace(" ", "-").ToLowerInvariant();
            thirdPartyUrls.Add(($"https://www.zillow.com/profile/{Uri.EscapeDataString(zillowSlug)}/", "ZillowGuess"));
        }

        // Own website first, then third-party profiles
        var urls = new List<(string, string)>(ownWebsiteUrls.Count + thirdPartyUrls.Count);
        urls.AddRange(ownWebsiteUrls);
        urls.AddRange(thirdPartyUrls);
        return urls;
    }

    private async Task<IReadOnlyList<DiscoveredWebsite>> FetchWebsitesAsync(
        IReadOnlyList<(string Url, string Source)> urlSources,
        string agentId,
        CancellationToken ct)
    {
        var results = new List<DiscoveredWebsite>();
        var http = httpClientFactory.CreateClient("AgentDiscovery");
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        foreach (var (url, source) in urlSources)
        {
            ct.ThrowIfCancellationRequested();

            if (!IsAllowedUrl(url))
            {
                logger.LogWarning(
                    "[AGENTDISCOVERY-052] Website URL rejected by SSRF filter: {Url} ({Source})", url, source);
                results.Add(new DiscoveredWebsite(url, source, null));
                continue;
            }

            // Use ScraperAPI for third-party sites (Zillow, Realtor.com, etc.) — they block raw HTTP.
            // Use direct HTTP for agent-owned sites (cheaper, faster, no JS rendering needed).
            var isThirdParty = !IsAgentOwnWebsite(url);
            if (isThirdParty && scraperClient.IsAvailable)
            {
                var html = await scraperClient.FetchAsync(url, source, agentId, ct);
                if (html is not null)
                {
                    results.Add(new DiscoveredWebsite(url, source, html));
                    logger.LogInformation(
                        "[AGENTDISCOVERY-022] Fetched {Source} via ScraperAPI ({Chars} chars): {Url}",
                        source, html.Length, url);
                    continue;
                }

                logger.LogWarning(
                    "[AGENTDISCOVERY-023] ScraperAPI returned null for {Source}: {Url}. " +
                    "Reason: ScraperAPI may be rate-limited or the page may not exist. " +
                    "Impact: no profile data from {Source} — reviews, sales count, and bio will be missing.",
                    source, url, source);
                results.Add(new DiscoveredWebsite(url, source, null));
                continue;
            }

            try
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "[AGENTDISCOVERY-020] SKIP {Source}: {Url} returned HTTP {Status}. " +
                        "Reason: {Reason}. Impact: no data from this source.",
                        source, url, (int)response.StatusCode,
                        isThirdParty
                            ? "third-party site blocked raw HTTP (bot detection) and ScraperAPI unavailable"
                            : "site may be down or URL is incorrect");
                    results.Add(new DiscoveredWebsite(url, source, null));
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync(ct);
                results.Add(new DiscoveredWebsite(url, source, html));

                logger.LogInformation(
                    "[AGENTDISCOVERY-021] Fetched {Source} via direct HTTP ({Chars} chars): {Url}",
                    source, html.Length, url);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "[AGENTDISCOVERY-032] SKIP {Source}: failed to fetch {Url}. " +
                    "Reason: {ExMessage}. Impact: no data from this source.",
                    source, url, ex.Message);
                results.Add(new DiscoveredWebsite(url, source, null));
            }
        }

        return results;
    }

    /// <summary>
    /// Parses structured data from Zillow, Realtor.com, and similar third-party profiles.
    /// Uses regex heuristics on the HTML — no scraping library dependency.
    /// </summary>
    internal static IReadOnlyList<ThirdPartyProfile> ParseThirdPartyProfiles(
        IReadOnlyList<DiscoveredWebsite> websites)
    {
        var profiles = new List<ThirdPartyProfile>();

        foreach (var site in websites)
        {
            if (site.Html is null)
                continue;

            if (!ThirdPartyDomains.Any(d => site.Url.Contains(d, StringComparison.OrdinalIgnoreCase)))
                continue;

            var platform = DeterminePlatform(site.Url);
            var bio = ExtractBio(site.Html);
            var reviews = ExtractReviews(site.Html, platform);
            var salesCount = ExtractSalesCount(site.Html);
            var yearsExp = ExtractYearsExperience(site.Html);
            var specialties = ExtractSpecialties(site.Html);
            var serviceAreas = ExtractServiceAreas(site.Html);
            var activeListings = ExtractListings(site.Html, "active");
            var recentSales = ExtractListings(site.Html, "sold");

            profiles.Add(new ThirdPartyProfile(
                Platform: platform,
                Bio: bio,
                Reviews: reviews,
                SalesCount: salesCount,
                ActiveListingCount: activeListings.Count > 0 ? activeListings.Count : null,
                YearsExperience: yearsExp,
                Specialties: specialties,
                ServiceAreas: serviceAreas,
                RecentSales: recentSales,
                ActiveListings: activeListings));
        }

        return profiles;
    }

    internal static string DeterminePlatform(string url) =>
        url switch
        {
            _ when url.Contains("zillow.com", StringComparison.OrdinalIgnoreCase) => "Zillow",
            _ when url.Contains("realtor.com", StringComparison.OrdinalIgnoreCase) => "Realtor.com",
            _ when url.Contains("homes.com", StringComparison.OrdinalIgnoreCase) => "Homes.com",
            _ when url.Contains("trulia.com", StringComparison.OrdinalIgnoreCase) => "Trulia",
            _ when url.Contains("redfin.com", StringComparison.OrdinalIgnoreCase) => "Redfin",
            _ => "Unknown"
        };

    internal static string? ExtractBio(string html)
    {
        // Look for common bio/about-me patterns
        var patterns = new[]
        {
            @"<[^>]*(?:bio|about|description)[^>]*>\s*<[^>]+>\s*([^<]{50,1000})",
            @"""description""\s*:\s*""([^""]{50,1000})""",
            @"""bio""\s*:\s*""([^""]{50,1000})""",
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
                return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
        }

        return null;
    }

    internal static IReadOnlyList<Review> ExtractReviews(string html, string source)
    {
        var reviews = new List<Review>();

        // JSON-LD structured data
        var jsonLdMatches = Regex.Matches(html,
            @"<script[^>]*type=[""']application/ld\+json[""'][^>]*>(.*?)</script>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match jsonLdMatch in jsonLdMatches)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonLdMatch.Groups[1].Value);
                var root = doc.RootElement;

                if (root.TryGetProperty("review", out var reviewArray))
                {
                    foreach (var review in reviewArray.EnumerateArray())
                    {
                        var text = review.TryGetProperty("reviewBody", out var bodyEl)
                            ? bodyEl.GetString() : null;
                        var authorName = review.TryGetProperty("author", out var authorEl)
                            ? (authorEl.ValueKind == JsonValueKind.Object
                                ? (authorEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null)
                                : authorEl.GetString())
                            : null;
                        var ratingValue = review.TryGetProperty("reviewRating", out var ratingEl)
                            ? (ratingEl.TryGetProperty("ratingValue", out var rvEl) ? rvEl.GetInt32() : 5)
                            : 5;

                        if (text is not null)
                        {
                            reviews.Add(new Review(
                                Text: text,
                                Rating: ratingValue,
                                Reviewer: authorName ?? "Anonymous",
                                Source: source,
                                Date: null));
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed JSON-LD
            }
        }

        return reviews;
    }

    internal static int? ExtractSalesCount(string html)
    {
        var match = Regex.Match(html,
            @"(\d+)\s*(?:homes?|properties|sales?|transactions?)\s*(?:sold|closed)",
            RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var count) ? count : null;
    }

    internal static int? ExtractYearsExperience(string html)
    {
        var match = Regex.Match(html,
            @"(\d+)\+?\s*years?\s*(?:of\s*)?(?:experience|in\s*real\s*estate)",
            RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var years) ? years : null;
    }

    internal static IReadOnlyList<string> ExtractSpecialties(string html)
    {
        var specialtyTerms = new[]
        {
            "First-Time Buyers", "Luxury Homes", "Investment Properties", "Condos",
            "Short Sales", "Foreclosures", "New Construction", "Relocation",
            "Senior Real Estate", "Waterfront", "Farm & Ranch"
        };

        return specialtyTerms
            .Where(s => html.Contains(s, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    internal static IReadOnlyList<string> ExtractServiceAreas(string html)
    {
        // Look for service area patterns in JSON or text
        var areas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var jsonMatch = Regex.Matches(html,
            @"""(?:serviceArea|areaServed|city)""\s*:\s*""([^""]{2,50})""",
            RegexOptions.IgnoreCase);

        foreach (Match m in jsonMatch)
            areas.Add(m.Groups[1].Value.Trim());

        return areas.Take(20).ToList();
    }

    internal static IReadOnlyList<ListingInfo> ExtractListings(string html, string status)
    {
        var listings = new List<ListingInfo>();

        // Look for JSON-LD listings
        var jsonLdMatches = Regex.Matches(html,
            @"<script[^>]*type=[""']application/ld\+json[""'][^>]*>(.*?)</script>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match jsonLdMatch in jsonLdMatches)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonLdMatch.Groups[1].Value);
                var root = doc.RootElement;

                // Skip if not a listing type
                if (!root.TryGetProperty("@type", out var typeEl))
                    continue;

                var type = typeEl.GetString() ?? string.Empty;
                if (!type.Contains("RealEstateListing", StringComparison.OrdinalIgnoreCase))
                    continue;

                var listingStatus = root.TryGetProperty("status", out var statusEl)
                    ? statusEl.GetString() : null;

                if (!string.IsNullOrEmpty(status) &&
                    !string.IsNullOrEmpty(listingStatus) &&
                    !listingStatus.Contains(status, StringComparison.OrdinalIgnoreCase))
                    continue;

                var address = string.Empty;
                var city = string.Empty;
                var state = string.Empty;

                if (root.TryGetProperty("address", out var addrEl) && addrEl.ValueKind == JsonValueKind.Object)
                {
                    address = addrEl.TryGetProperty("streetAddress", out var sa) ? sa.GetString() ?? string.Empty : string.Empty;
                    city = addrEl.TryGetProperty("addressLocality", out var al) ? al.GetString() ?? string.Empty : string.Empty;
                    state = addrEl.TryGetProperty("addressRegion", out var ar) ? ar.GetString() ?? string.Empty : string.Empty;
                }

                var price = root.TryGetProperty("price", out var priceEl) ? priceEl.ToString() : string.Empty;

                listings.Add(new ListingInfo(
                    Address: address,
                    City: city,
                    State: state,
                    Price: price,
                    Status: listingStatus,
                    Beds: null,
                    Baths: null,
                    Sqft: null,
                    ImageUrl: null,
                    Date: null));
            }
            catch
            {
                // Ignore malformed JSON-LD
            }
        }

        return listings;
    }

    internal static string? ExtractGa4MeasurementId(IReadOnlyList<DiscoveredWebsite> websites)
    {
        foreach (var site in websites)
        {
            if (site.Html is null)
                continue;

            // Skip known third-party sites — GA4 would belong to the agent's own site
            if (ThirdPartyDomains.Any(d => site.Url.Contains(d, StringComparison.OrdinalIgnoreCase)))
                continue;

            var match = Regex.Match(site.Html, @"G-[A-Z0-9]{8,12}");
            if (match.Success)
                return match.Value;

            // Also check GTM
            var gtmMatch = Regex.Match(site.Html, @"GTM-[A-Z0-9]{5,8}");
            if (gtmMatch.Success)
                return gtmMatch.Value;
        }

        return null;
    }

    /// <summary>
    /// Fetches reviews from the Zillow Reviews API (Bridge Interactive).
    /// Tries email lookup first (exact match), falls back to name search.
    /// Returns empty if the API is unavailable — the worker continues without reviews.
    /// </summary>
    private async Task<ZillowAgentReviews> FetchZillowReviewsAsync(
        string? agentEmail, string agentName, string agentId, CancellationToken ct)
    {
        if (!zillowClient.IsAvailable)
        {
            logger.LogWarning(
                "[AGENTDISCOVERY-060] SKIP Zillow Reviews API: not configured (no API token). " +
                "Agent: {AgentId}. Impact: reviews will only come from HTML scraping (if ScraperAPI available).",
                agentId);
            return new ZillowAgentReviews([], null, 0, null);
        }

        // Try email first — exact match, most reliable
        if (!string.IsNullOrWhiteSpace(agentEmail))
        {
            var byEmail = await zillowClient.GetReviewsByEmailAsync(agentEmail, agentId, ct);
            if (byEmail.TotalReviewCount > 0)
                return byEmail;

            logger.LogInformation(
                "[AGENTDISCOVERY-061] No Zillow reviews found by email ({Email}), trying name search.",
                agentEmail, agentId);
        }

        // Fall back to name search
        var byName = await zillowClient.GetReviewsByNameAsync(agentName, agentId, ct);
        if (byName.TotalReviewCount == 0)
        {
            logger.LogInformation(
                "[AGENTDISCOVERY-062] No Zillow reviews found for agent {AgentId} by email or name.",
                agentId);
        }

        return byName;
    }

    /// <summary>
    /// Fetches reviews from Google Places API.
    /// Searches by agent name + brokerage name to find their Google Business listing.
    /// </summary>
    private async Task<GooglePlaceReviews> FetchGoogleReviewsAsync(
        string searchQuery, string agentId, CancellationToken ct)
    {
        if (!googleReviewsClient.IsAvailable)
        {
            logger.LogWarning(
                "[AGENTDISCOVERY-070] SKIP Google Reviews: not configured (no API key). " +
                "Agent: {AgentId}. Impact: no Google reviews.",
                agentId);
            return new GooglePlaceReviews([], null, 0, null, null, null);
        }

        var result = await googleReviewsClient.GetReviewsAsync(searchQuery, agentId, ct);
        if (result.TotalReviewCount == 0)
        {
            logger.LogInformation(
                "[AGENTDISCOVERY-071] No Google reviews found for agent {AgentId}, query '{Query}'.",
                agentId, searchQuery);
        }

        return result;
    }

    private async Task<bool> CheckWhatsAppAsync(string? phoneNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            logger.LogDebug("[AGENTDISCOVERY-040] No phone number available for WhatsApp check.");
            return false;
        }

        try
        {
            // Attempt to send a freeform message to the number — if it throws
            // WhatsAppNotRegisteredException the agent is not on WhatsApp.
            // We use a 0-length "ping" approach: just validate the number resolves.
            // Since we can't do a true "is registered" check without a message,
            // we optimistically return true if the number is present and no exception occurs
            // during a template message attempt. In practice the activation orchestrator
            // will set this correctly after a live check.
            await whatsAppSender.SendFreeformAsync(
                phoneNumber, "\u200b", ct); // zero-width space as "ping"
            return true;
        }
        catch (WhatsAppNotRegisteredException)
        {
            logger.LogInformation(
                "[AGENTDISCOVERY-041] Phone {Phone} is not registered on WhatsApp.",
                phoneNumber);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // If the error is anything else (API error, rate limit etc.), we assume not enabled
            // rather than crashing the discovery worker.
            logger.LogWarning(ex,
                "[AGENTDISCOVERY-042] WhatsApp check failed for phone {Phone}. Assuming not enabled.",
                phoneNumber);
            return false;
        }
    }

    private static string? ExtractPhoneFromSignature(EmailSignature? signature) =>
        signature?.Phone;
}
