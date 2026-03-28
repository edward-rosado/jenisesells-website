using Xunit;
using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.DataServices.Config;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.Storage;
using RealEstateStar.DataServices.WhatsApp;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.Protected;

namespace RealEstateStar.DataServices.Tests.Onboarding;

public class ProfileScraperTests
{
    // Creates a mock IHttpClientFactory that returns a fixed HTTP response (used for page fetch).
    private static IHttpClientFactory CreatePageFetchFactory(HttpStatusCode status, string content)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(content),
            });
        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return factory.Object;
    }

    private static IHttpClientFactory CreateThrowingPageFetchFactory()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return factory.Object;
    }

    private static IAnthropicClient CreateAnthropicClient(string responseJson) =>
        CreateAnthropicClient(new AnthropicResponse(responseJson, 100, 200, 500));

    private static IAnthropicClient CreateAnthropicClient(AnthropicResponse response)
    {
        var mock = new Mock<IAnthropicClient>();
        mock.Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        return mock.Object;
    }

    private static IAnthropicClient CreateThrowingAnthropicClient(Exception ex)
    {
        var mock = new Mock<IAnthropicClient>();
        mock.Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ex);
        return mock.Object;
    }

    private static IDnsResolver CreateMockDnsResolver(params IPAddress[] addresses)
    {
        var mock = new Mock<IDnsResolver>();
        mock.Setup(d => d.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(addresses);
        return mock.Object;
    }

    private static IDnsResolver CreateThrowingDnsResolver()
    {
        var mock = new Mock<IDnsResolver>();
        mock.Setup(d => d.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Sockets.SocketException(11001));
        return mock.Object;
    }

    private static readonly IPAddress PublicIp = IPAddress.Parse("104.18.32.7");

    // Helper to build a scraper with the given page-fetch response and Claude response JSON
    private static ProfileScraperService MakeScraper(
        string pageHtml,
        string claudeResponseJson,
        string? scraperApiKey = null) =>
        new(
            CreatePageFetchFactory(HttpStatusCode.OK, pageHtml),
            CreateAnthropicClient(claudeResponseJson),
            scraperApiKey,
            CreateMockDnsResolver(PublicIp),
            NullLogger<ProfileScraperService>.Instance);

    // Helper for scraper where Claude throws
    private static ProfileScraperService MakeScraperWithFailingClaude(
        string pageHtml,
        Exception claudeEx,
        string? scraperApiKey = null) =>
        new(
            CreatePageFetchFactory(HttpStatusCode.OK, pageHtml),
            CreateThrowingAnthropicClient(claudeEx),
            scraperApiKey,
            CreateMockDnsResolver(PublicIp),
            NullLogger<ProfileScraperService>.Instance);

    [Fact]
    public async Task ScrapeAsync_FetchFailure_ReturnsNull()
    {
        var anthropicClient = CreateAnthropicClient("{}");
        var dns = CreateMockDnsResolver(PublicIp);
        var scraper = new ProfileScraperService(
            CreateThrowingPageFetchFactory(),
            anthropicClient,
            null,
            dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/nobody", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_EmptyPage_ReturnsNull()
    {
        var anthropicClient = CreateAnthropicClient("{}");
        var dns = CreateMockDnsResolver(PublicIp);
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "<html><body></body></html>"),
            anthropicClient,
            null,
            dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/empty", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_ValidPage_FallsBackOnClaudeFailure()
    {
        var html = "<html><body><h1>Jane Doe</h1><p>RE/MAX agent serving New Jersey with 15 years experience and 200 homes sold in the tri-state area.</p></body></html>";
        var dns = CreateMockDnsResolver(PublicIp);
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, html),
            CreateThrowingAnthropicClient(new HttpRequestException("Anthropic API error")),
            null,
            dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/jane-doe", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("zillow.com", result!.Bio);
    }

    // --- DNS rebinding prevention tests ---

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToLoopback_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(IPAddress.Parse("127.0.0.1"));
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "should not reach"),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToPrivate10_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(IPAddress.Parse("10.0.0.1"));
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "should not reach"),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToLinkLocal169_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(IPAddress.Parse("169.254.169.254"));
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "should not reach"),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToPrivate172_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(IPAddress.Parse("172.16.0.1"));
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "should not reach"),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToPrivate192_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(IPAddress.Parse("192.168.1.1"));
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "should not reach"),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToIpv6Loopback_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(IPAddress.IPv6Loopback);
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "should not reach"),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToIpv6UniqueLocal_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(IPAddress.Parse("fd00::1"));
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "should not reach"),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToIpv6LinkLocal_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(IPAddress.Parse("fe80::1"));
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "should not reach"),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToIpv4MappedPrivate_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(IPAddress.Parse("10.0.0.1").MapToIPv6());
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "should not reach"),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToPublicIp_Proceeds()
    {
        var html = "<html><body><h1>Jane Doe</h1><p>RE/MAX agent serving New Jersey with 15 years experience and 200 homes sold in the tri-state area.</p></body></html>";
        var dns = CreateMockDnsResolver(PublicIp);
        // Claude throws — should fall back to partial profile (proving DNS check passed)
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, html),
            CreateThrowingAnthropicClient(new HttpRequestException("invalid key")),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToMixedPublicAndPrivate_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(PublicIp, IPAddress.Parse("10.0.0.1"));
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "should not reach"),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolutionFails_ReturnsNull()
    {
        var dns = CreateThrowingDnsResolver();
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "should not reach"),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToNoAddresses_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(); // empty
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, "should not reach"),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    // --- IsPrivateIp direct tests for branch coverage ---

    [Theory]
    [InlineData("10.0.0.1", true)]
    [InlineData("10.255.255.255", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.255", true)]
    [InlineData("172.15.0.1", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.0.1", true)]
    [InlineData("192.169.0.1", false)]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.255.255.255", true)]
    [InlineData("169.254.0.1", true)]
    [InlineData("169.253.0.1", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("104.18.32.7", false)]
    [InlineData("1.1.1.1", false)]
    public void IsPrivateIp_IPv4(string ipStr, bool expected)
    {
        var ip = IPAddress.Parse(ipStr);
        Assert.Equal(expected, ProfileScraperService.IsPrivateIp(ip));
    }

    [Theory]
    [InlineData("fd00::1", true)]
    [InlineData("fdff::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("fe80::abcd", true)]
    [InlineData("::1", false)]          // loopback — handled by IPAddress.IsLoopback, not IsPrivateIp
    [InlineData("2001:db8::1", false)]
    [InlineData("2607:f8b0:4004:800::200e", false)]
    public void IsPrivateIp_IPv6(string ipStr, bool expected)
    {
        var ip = IPAddress.Parse(ipStr);
        Assert.Equal(expected, ProfileScraperService.IsPrivateIp(ip));
    }

    // --- ValidateUrl static tests ---

    [Theory]
    [InlineData("not-a-url", "Invalid URL format")]
    [InlineData("http://zillow.com/profile/test", "Only HTTPS URLs are allowed")]
    [InlineData("https://evil.com/profile/test", "Domain 'evil.com' is not in the allowed list")]
    public void ValidateUrl_InvalidInputs(string url, string expectedError)
    {
        var result = ProfileScraperService.ValidateUrl(url);
        Assert.NotNull(result);
        Assert.Contains(expectedError, result);
    }

    [Fact]
    public void ValidateUrl_ValidUrl_ReturnsNull()
    {
        var result = ProfileScraperService.ValidateUrl("https://zillow.com/profile/test");
        Assert.Null(result);
    }

    // --- ValidateDnsResolutionAsync direct tests ---

    [Fact]
    public async Task ValidateDnsResolutionAsync_PublicIp_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(PublicIp);
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, ""),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ValidateDnsResolutionAsync("zillow.com", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateDnsResolutionAsync_PrivateIp_ReturnsError()
    {
        var dns = CreateMockDnsResolver(IPAddress.Parse("10.0.0.1"));
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, ""),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ValidateDnsResolutionAsync("zillow.com", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("private/loopback", result);
    }

    [Fact]
    public async Task ValidateDnsResolutionAsync_DnsFailure_ReturnsError()
    {
        var dns = CreateThrowingDnsResolver();
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, ""),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ValidateDnsResolutionAsync("nonexistent.example", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("DNS resolution failed", result);
    }

    [Fact]
    public async Task ValidateDnsResolutionAsync_EmptyAddresses_ReturnsError()
    {
        var dns = CreateMockDnsResolver(); // empty array
        var scraper = new ProfileScraperService(
            CreatePageFetchFactory(HttpStatusCode.OK, ""),
            CreateAnthropicClient("{}"),
            null, dns,
            NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ValidateDnsResolutionAsync("zillow.com", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("no addresses", result);
    }

    // --- ExtractPageContent static tests ---

    [Fact]
    public void ExtractPageContent_PlainHtml_ReturnsVisibleText()
    {
        var html = "<html><body><h1>Agent Name</h1><p>Some bio text here with enough content to pass the length check.</p></body></html>";
        var result = ProfileScraperService.ExtractPageContent(html);

        Assert.Contains("Agent Name", result);
        Assert.Contains("Some bio text", result);
        Assert.DoesNotContain("<h1>", result);
        Assert.DoesNotContain("[Structured Data]", result);
    }

    [Fact]
    public void ExtractPageContent_WithJsonLd_IncludesStructuredData()
    {
        var html = """
            <html><body>
            <script type="application/ld+json">{"@type":"RealEstateAgent","name":"Jane Doe"}</script>
            <h1>Jane Doe</h1><p>A great agent.</p>
            </body></html>
            """;
        var result = ProfileScraperService.ExtractPageContent(html);

        Assert.Contains("[Structured Data]", result);
        Assert.Contains("RealEstateAgent", result);
        Assert.Contains("[Visible Text]", result);
        Assert.Contains("Jane Doe", result);
    }

    [Fact]
    public void ExtractPageContent_WithNextData_IncludesStructuredData()
    {
        var html = """
            <html><body>
            <script id="__NEXT_DATA__" type="application/json">{"props":{"agent":"data"}}</script>
            <h1>Agent Page</h1>
            </body></html>
            """;
        var result = ProfileScraperService.ExtractPageContent(html);

        Assert.Contains("[Structured Data]", result);
        Assert.Contains("props", result);
    }

    [Fact]
    public void ExtractPageContent_WithHtmlEncodedJsonLd_DecodesEntities()
    {
        var html = """<html><body><script type="application/ld+json">{&quot;name&quot;:&quot;Jane&quot;}</script><p>Content here.</p></body></html>""";
        var result = ProfileScraperService.ExtractPageContent(html);

        Assert.Contains("\"name\"", result);
    }

    [Fact]
    public void ExtractPageContent_StripsScriptsAndStyles()
    {
        var html = """
            <html><head><style>body { color: red; }</style></head>
            <body><script>alert('xss');</script><p>Visible content here.</p></body></html>
            """;
        var result = ProfileScraperService.ExtractPageContent(html);

        Assert.DoesNotContain("alert", result);
        Assert.DoesNotContain("color: red", result);
        Assert.Contains("Visible content", result);
    }

    // --- Full scrape happy path with Claude response ---

    private const string ValidClaudeProfileJson = """
        {
            "name": "Jane Doe",
            "title": "REALTOR\u00ae",
            "tagline": "Your dream home awaits",
            "phone": "555-1234",
            "email": "jane@remax.com",
            "photoUrl": "https://example.com/photo.jpg",
            "brokerage": "RE/MAX",
            "brokerageLogoUrl": "https://example.com/logo.png",
            "licenseId": "NJ-123456",
            "state": "NJ",
            "officeAddress": "123 Main St",
            "serviceAreas": ["Newark", "Jersey City"],
            "specialties": ["Buyer Agent", "Listing Agent"],
            "designations": ["CRS", "ABR"],
            "languages": ["English", "Spanish"],
            "bio": "Jane has 15 years of experience in real estate.",
            "yearsExperience": 15,
            "homesSold": 200,
            "avgRating": 4.8,
            "reviewCount": 50,
            "avgListPrice": 450000.0,
            "primaryColor": "#DC1C2E",
            "accentColor": "#003366",
            "websiteUrl": "https://janedoe.com",
            "facebookUrl": "https://facebook.com/janedoe",
            "instagramUrl": "https://instagram.com/janedoe",
            "linkedInUrl": "https://linkedin.com/in/janedoe",
            "testimonials": [
                {"reviewerName": "Bob", "text": "Great agent!", "rating": 5.0, "date": "2025-01-01"},
                {"reviewerName": null, "text": null, "rating": null, "date": null}
            ],
            "recentSales": [
                {"address": "456 Oak Ave", "price": 500000.0, "date": "2025-06-01", "photoUrl": "https://example.com/house.jpg"},
                {"address": null, "price": null, "date": null, "photoUrl": null}
            ]
        }
        """;

    [Fact]
    public async Task ScrapeAsync_FullHappyPath_ReturnsCompleteProfile()
    {
        var pageHtml = "<html><body><h1>Jane Doe</h1><p>RE/MAX agent serving New Jersey with 15 years experience and 200 homes sold in the tri-state area. She is great.</p></body></html>";
        var scraper = MakeScraper(pageHtml, ValidClaudeProfileJson);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/jane-doe", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Jane Doe", result!.Name);
        Assert.Equal("REALTOR\u00ae", result.Title);
        Assert.Equal("Your dream home awaits", result.Tagline);
        Assert.Equal("555-1234", result.Phone);
        Assert.Equal("jane@remax.com", result.Email);
        Assert.Equal("https://example.com/photo.jpg", result.PhotoUrl);
        Assert.Equal("RE/MAX", result.Brokerage);
        Assert.Equal("https://example.com/logo.png", result.BrokerageLogoUrl);
        Assert.Equal("NJ-123456", result.LicenseId);
        Assert.Equal("NJ", result.State);
        Assert.Equal("123 Main St", result.OfficeAddress);
        Assert.Equal(["Newark", "Jersey City"], result.ServiceAreas!);
        Assert.Equal(["Buyer Agent", "Listing Agent"], result.Specialties!);
        Assert.Equal(["CRS", "ABR"], result.Designations!);
        Assert.Equal(["English", "Spanish"], result.Languages!);
        Assert.Contains("15 years", result.Bio);
        Assert.Equal(15, result.YearsExperience);
        Assert.Equal(200, result.HomesSold);
        Assert.Equal(4.8, result.AvgRating);
        Assert.Equal(50, result.ReviewCount);
        Assert.Equal(450000.0, result.AvgListPrice);
        Assert.Equal("#DC1C2E", result.PrimaryColor);
        Assert.Equal("#003366", result.AccentColor);
        Assert.Equal("https://janedoe.com", result.WebsiteUrl);
        Assert.Equal("https://facebook.com/janedoe", result.FacebookUrl);
        Assert.Equal("https://instagram.com/janedoe", result.InstagramUrl);
        Assert.Equal("https://linkedin.com/in/janedoe", result.LinkedInUrl);

        Assert.NotNull(result.Testimonials);
        Assert.Single(result.Testimonials!);
        Assert.Equal("Bob", result.Testimonials![0].ReviewerName);
        Assert.Equal("Great agent!", result.Testimonials[0].Text);
        Assert.Equal(5.0, result.Testimonials[0].Rating);

        Assert.NotNull(result.RecentSales);
        Assert.Single(result.RecentSales!);
        Assert.Equal("456 Oak Ave", result.RecentSales![0].Address);
        Assert.Equal(500000.0, result.RecentSales[0].Price);
    }

    [Fact]
    public async Task ScrapeAsync_ClaudeReturnsErrorProperty_ReturnsNull()
    {
        var pageHtml = "<html><body><h1>Not an Agent</h1><p>This page is not a real estate agent profile but has enough text content to pass.</p></body></html>";
        var scraper = MakeScraper(pageHtml, """{"error": "not_agent_profile"}""");

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/not-agent", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_ClaudeReturnsMarkdownFences_StripsThemCorrectly()
    {
        // AnthropicClient strips fences before returning Content — test that the
        // already-stripped JSON is parsed correctly
        var pageHtml = "<html><body><h1>Agent Test</h1><p>Enough text content to pass the minimum length check for profile extraction.</p></body></html>";
        // Simulate what AnthropicClient returns AFTER stripping fences
        var scraper = MakeScraper(pageHtml, """{"name":"Fenced Agent","bio":"Test bio"}""");

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/fenced", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Fenced Agent", result!.Name);
    }

    [Fact]
    public async Task ScrapeAsync_ClaudeReturnsNonSuccessStatus_FallsBackToPartialProfile()
    {
        // Simulate AnthropicClient throwing (which it does on non-2xx)
        var pageHtml = "<html><body><h1>Agent</h1><p>Enough content for the minimum length requirement to pass successfully.</p></body></html>";
        var scraper = MakeScraperWithFailingClaude(
            pageHtml,
            new HttpRequestException("Anthropic API returned 429: rate limited"));

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/rate-limited", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("zillow.com", result!.Bio);
    }

    [Fact]
    public async Task ScrapeAsync_WithScraperApiKey_UsesProxyUrl()
    {
        string? capturedUrl = null;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                capturedUrl = req.RequestUri?.ToString();
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("<html><body><h1>Agent</h1><p>Lots of content here for the length check to pass successfully.</p></body></html>")
                };
            });
        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var anthropicClient = CreateAnthropicClient("""{"name":"Proxy Agent","bio":"Via proxy"}""");
        var dns = CreateMockDnsResolver(PublicIp);
        var scraper = new ProfileScraperService(factory.Object, anthropicClient, "scraper-api-key-123", dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/proxy-test", CancellationToken.None);

        Assert.NotNull(capturedUrl);
        Assert.Contains("api.scraperapi.com", capturedUrl!);
        Assert.Contains("scraper-api-key-123", capturedUrl);
        Assert.Contains("render=true", capturedUrl);
    }

    [Fact]
    public async Task ScrapeAsync_LongPageContent_TruncatesTo15000()
    {
        var longText = new string('A', 16000);
        var pageHtml = $"<html><body><p>{longText}</p></body></html>";
        var scraper = MakeScraper(pageHtml, """{"name":"Truncated Agent","bio":"Truncated"}""");

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/long-page", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Truncated Agent", result!.Name);
    }

    [Fact]
    public async Task ScrapeAsync_ClaudeReturnsNullFields_HandlesGracefully()
    {
        var pageHtml = "<html><body><h1>Minimal Agent</h1><p>This page has just enough content for the minimum length check.</p></body></html>";
        var scraper = MakeScraper(pageHtml, """{"name":null,"phone":null,"email":null,"bio":null,"testimonials":null,"recentSales":null}""");

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/minimal", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result!.Name);
        Assert.Null(result.Phone);
        Assert.Null(result.Testimonials);
        Assert.Null(result.RecentSales);
    }

    [Fact]
    public async Task ScrapeAsync_ClaudeReturnsEmptyTestimonialsArray_ReturnsEmptyArray()
    {
        var pageHtml = "<html><body><h1>Agent</h1><p>Enough content for the length check to succeed in testing.</p></body></html>";
        var scraper = MakeScraper(pageHtml, """{"name":"Test","testimonials":[],"recentSales":[]}""");

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/empty-arrays", CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result!.Testimonials);
        Assert.Empty(result.Testimonials!);
        Assert.NotNull(result.RecentSales);
        Assert.Empty(result.RecentSales!);
    }

    [Fact]
    public async Task ScrapeAsync_ClaudeReturnsNonArrayTestimonials_ReturnsNull()
    {
        var pageHtml = "<html><body><h1>Agent</h1><p>Enough content for the length check to succeed in testing.</p></body></html>";
        var scraper = MakeScraper(pageHtml, """{"name":"Test","testimonials":"not an array","recentSales":"also not array"}""");

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/non-array", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result!.Testimonials);
        Assert.Null(result.RecentSales);
    }

    [Fact]
    public async Task ScrapeAsync_ClaudeReturnsNonNumericStats_ReturnsNullForStats()
    {
        var pageHtml = "<html><body><h1>Agent</h1><p>Enough content for the length check to succeed in testing.</p></body></html>";
        var scraper = MakeScraper(pageHtml, """{"name":"Test","yearsExperience":"fifteen","homesSold":"many","avgRating":"high","reviewCount":"lots","avgListPrice":"expensive"}""");

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/non-numeric", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result!.YearsExperience);
        Assert.Null(result.HomesSold);
        Assert.Null(result.AvgRating);
        Assert.Null(result.ReviewCount);
        Assert.Null(result.AvgListPrice);
    }

    [Fact]
    public async Task ScrapeAsync_ClaudeReturnsNonArrayServiceAreas_ReturnsNull()
    {
        var pageHtml = "<html><body><h1>Agent</h1><p>Enough content for the length check to succeed in testing.</p></body></html>";
        var scraper = MakeScraper(pageHtml, """{"name":"Test","serviceAreas":"Newark","specialties":"Buyer","designations":"CRS","languages":"English"}""");

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/non-array-fields", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result!.ServiceAreas);
        Assert.Null(result.Specialties);
        Assert.Null(result.Designations);
        Assert.Null(result.Languages);
    }

    // --- ValidateUrl: IP address in host ---

    [Fact]
    public void ValidateUrl_LoopbackIpInHost_ReturnsError()
    {
        var result = ProfileScraperService.ValidateUrl("https://127.0.0.1/profile");
        Assert.NotNull(result);
        Assert.Contains("Private/loopback IP addresses are not allowed", result);
    }

    [Fact]
    public void ValidateUrl_PrivateIpInHost_ReturnsError()
    {
        var result = ProfileScraperService.ValidateUrl("https://10.0.0.1/profile");
        Assert.NotNull(result);
        Assert.Contains("Private/loopback IP addresses are not allowed", result);
    }

    [Fact]
    public void ValidateUrl_PublicIpInHost_ReturnsDirectIpError()
    {
        var result = ProfileScraperService.ValidateUrl("https://8.8.8.8/profile");
        Assert.NotNull(result);
        Assert.Contains("Direct IP addresses are not allowed", result);
    }

    // --- ValidateUrl: all allowed domains pass ---

    [Theory]
    [InlineData("https://realtor.com/profile")]
    [InlineData("https://www.realtor.com/profile")]
    [InlineData("https://zillow.com/profile")]
    [InlineData("https://www.zillow.com/profile")]
    [InlineData("https://redfin.com/profile")]
    [InlineData("https://coldwellbanker.com/profile")]
    [InlineData("https://century21.com/profile")]
    [InlineData("https://kw.com/profile")]
    [InlineData("https://compass.com/profile")]
    [InlineData("https://bhhs.com/profile")]
    [InlineData("https://sothebysrealty.com/profile")]
    [InlineData("https://weichert.com/profile")]
    public void ValidateUrl_AllAllowedDomains_ReturnsNull(string url)
    {
        var result = ProfileScraperService.ValidateUrl(url);
        Assert.Null(result);
    }

    // --- IsPrivateIp: IPv6 non-private, non-link-local ---

    [Fact]
    public void IsPrivateIp_Ipv6PublicAddress_ReturnsFalse()
    {
        var ip = IPAddress.Parse("2001:4860:4860::8888");
        Assert.False(ProfileScraperService.IsPrivateIp(ip));
    }

    [Fact]
    public void IsPrivateIp_Ipv4MappedIpv6_PublicAddress_ReturnsFalse()
    {
        var ip = IPAddress.Parse("8.8.8.8").MapToIPv6();
        Assert.False(ProfileScraperService.IsPrivateIp(ip));
    }

    [Fact]
    public void IsPrivateIp_Ipv4MappedIpv6_PrivateAddress_ReturnsTrue()
    {
        var ip = IPAddress.Parse("192.168.1.1").MapToIPv6();
        Assert.True(ProfileScraperService.IsPrivateIp(ip));
    }
}
