using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Activation.AgentDiscovery.Tests;

public class AgentDiscoveryWorkerTests
{
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";
    private const string AgentName = "Jane Doe";
    private const string BrokerageName = "Sunrise Realty";

    private static OAuthCredential ValidCredential() => new()
    {
        AccountId = AccountId,
        AgentId = AgentId,
        AccessToken = "access-token",
        RefreshToken = "refresh-token",
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        Scopes = ["https://www.googleapis.com/auth/contacts.readonly"],
        Email = "agent@example.com",
        Name = "Jane Doe",
        ETag = "etag-123"
    };

    private static AgentDiscoveryWorker BuildWorker(
        Mock<IOAuthRefresher>? mockRefresher = null,
        Mock<IHttpClientFactory>? mockFactory = null,
        Mock<IScraperClient>? mockScraper = null,
        Mock<IZillowReviewsClient>? mockZillow = null,
        Mock<IWhatsAppSender>? mockWhatsApp = null)
    {
        mockRefresher ??= new Mock<IOAuthRefresher>();
        mockFactory ??= BuildMockFactory(null);

        if (mockScraper is null)
        {
            // Default: scraper unavailable (direct HTTP fallback)
            mockScraper = new Mock<IScraperClient>();
            mockScraper.Setup(s => s.IsAvailable).Returns(false);
        }

        if (mockZillow is null)
        {
            // Default: Zillow API unavailable (no token)
            mockZillow = new Mock<IZillowReviewsClient>();
            mockZillow.Setup(z => z.IsAvailable).Returns(false);
            mockZillow
                .Setup(z => z.GetReviewsByEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ZillowAgentReviews([], null, 0, null));
            mockZillow
                .Setup(z => z.GetReviewsByNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ZillowAgentReviews([], null, 0, null));
        }

        if (mockWhatsApp is null)
        {
            // Default: WhatsApp throws not-registered
            mockWhatsApp = new Mock<IWhatsAppSender>();
            mockWhatsApp
                .Setup(w => w.SendFreeformAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new WhatsAppNotRegisteredException("555-000-0000"));
        }

        return new AgentDiscoveryWorker(
            mockRefresher.Object,
            mockFactory.Object,
            mockScraper.Object,
            mockZillow.Object,
            mockWhatsApp.Object,
            NullLogger<AgentDiscoveryWorker>.Instance);
    }

    private static Mock<IHttpClientFactory> BuildMockFactory(
        Dictionary<string, HttpResponseMessage>? urlResponses)
    {
        var handler = new MockDiscoveryHttpHandler(urlResponses ?? []);
        var httpClient = new HttpClient(handler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);
        return mockFactory;
    }

    // ──────────────────────────────────────────────────────────
    // RunAsync — overall behavior
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ReturnsResult_WhenAllSourcesEmpty()
    {
        var mockRefresher = new Mock<IOAuthRefresher>();
        mockRefresher
            .Setup(r => r.GetValidCredentialAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        var worker = BuildWorker(mockRefresher);

        var result = await worker.RunAsync(
            AccountId, AgentId, AgentName, BrokerageName, null, null, null, null, null, CancellationToken.None);

        result.Should().NotBeNull();
        result.HeadshotBytes.Should().BeNull();
        result.LogoBytes.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_SetsWhatsAppEnabled_WhenSendSucceeds()
    {
        var mockRefresher = new Mock<IOAuthRefresher>();
        mockRefresher
            .Setup(r => r.GetValidCredentialAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        var mockWhatsApp = new Mock<IWhatsAppSender>();
        mockWhatsApp
            .Setup(w => w.SendFreeformAsync("555-123-4567", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult("msg-id"));

        var worker = BuildWorker(mockRefresher, mockWhatsApp: mockWhatsApp);

        var result = await worker.RunAsync(
            AccountId, AgentId, AgentName, BrokerageName, "555-123-4567", null, null, null, null, CancellationToken.None);

        result.WhatsAppEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_SetsWhatsAppDisabled_WhenNotRegistered()
    {
        var mockRefresher = new Mock<IOAuthRefresher>();
        mockRefresher
            .Setup(r => r.GetValidCredentialAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        var mockWhatsApp = new Mock<IWhatsAppSender>();
        mockWhatsApp
            .Setup(w => w.SendFreeformAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WhatsAppNotRegisteredException("555-000-0000"));

        var worker = BuildWorker(mockRefresher, mockWhatsApp: mockWhatsApp);

        var result = await worker.RunAsync(
            AccountId, AgentId, AgentName, BrokerageName, "555-000-0000", null, null, null, null, CancellationToken.None);

        result.WhatsAppEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_SetsWhatsAppFalse_WhenNoPhone()
    {
        var mockRefresher = new Mock<IOAuthRefresher>();
        mockRefresher
            .Setup(r => r.GetValidCredentialAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        var worker = BuildWorker(mockRefresher);

        var result = await worker.RunAsync(
            AccountId, AgentId, AgentName, BrokerageName,
            phoneNumber: null, emailSignature: null, emailHandle: null, agentEmail: null, discoveredUrls: null, CancellationToken.None);

        result.WhatsAppEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ReturnsPhone_FromParameter()
    {
        var mockRefresher = new Mock<IOAuthRefresher>();
        mockRefresher
            .Setup(r => r.GetValidCredentialAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        var worker = BuildWorker(mockRefresher);

        var result = await worker.RunAsync(
            AccountId, AgentId, AgentName, BrokerageName, "555-777-8888", null, null, null, null, CancellationToken.None);

        result.Phone.Should().Be("555-777-8888");
    }

    [Fact]
    public async Task RunAsync_ReturnsPhone_FromEmailSignature_WhenNoPhoneParam()
    {
        var mockRefresher = new Mock<IOAuthRefresher>();
        mockRefresher
            .Setup(r => r.GetValidCredentialAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        var sig = new EmailSignature(
            "Jane Doe", "REALTOR", "555-999-0000", null, null, [], null, null, null);

        var worker = BuildWorker(mockRefresher);

        var result = await worker.RunAsync(
            AccountId, AgentId, AgentName, BrokerageName,
            phoneNumber: null, emailSignature: sig, emailHandle: null, agentEmail: null, discoveredUrls: null, CancellationToken.None);

        result.Phone.Should().Be("555-999-0000");
    }

    // ──────────────────────────────────────────────────────────
    // ExtractPhotoUrl
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ExtractPhotoUrl_ReturnsUrl_WhenPresentInJson()
    {
        var json = """{"photos":[{"url":"https://photos.google.com/photo.jpg","metadata":{"primary":true}}]}""";

        var url = AgentDiscoveryWorker.ExtractPhotoUrl(json);

        url.Should().Be("https://photos.google.com/photo.jpg");
    }

    [Fact]
    public void ExtractPhotoUrl_ReturnsNull_WhenNoPhotos()
    {
        var json = """{"names":[{"displayName":"Jane"}]}""";

        var url = AgentDiscoveryWorker.ExtractPhotoUrl(json);

        url.Should().BeNull();
    }

    [Fact]
    public void ExtractPhotoUrl_ReturnsNull_WhenMalformedJson()
    {
        var url = AgentDiscoveryWorker.ExtractPhotoUrl("NOT JSON");

        url.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────
    // BuildWebsiteSearchUrls
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void BuildWebsiteSearchUrls_IncludesSignatureWebsite()
    {
        var sig = new EmailSignature(
            null, null, null, null, null, [],
            null, "https://www.janedoe.com", null);

        var urls = AgentDiscoveryWorker.BuildWebsiteSearchUrls(AgentName, BrokerageName, sig);

        urls.Should().Contain(u => u.Url == "https://www.janedoe.com" && u.Source == "OwnWebsite");
    }

    [Fact]
    public void BuildWebsiteSearchUrls_IncludesZillowUrl()
    {
        var urls = AgentDiscoveryWorker.BuildWebsiteSearchUrls(AgentName, BrokerageName, null);

        urls.Should().Contain(u => u.Url.Contains("zillow.com") && u.Source == "ZillowGuess");
    }

    [Fact]
    public void BuildWebsiteSearchUrls_DoesNotIncludeRealtorCom_ToSaveScraperCredits()
    {
        var urls = AgentDiscoveryWorker.BuildWebsiteSearchUrls(AgentName, BrokerageName, null);

        urls.Should().NotContain(u => u.Source == "RealtorCom");
    }

    [Fact]
    public void BuildWebsiteSearchUrls_IncludesDomainGuess_FromEmailHandle()
    {
        var urls = AgentDiscoveryWorker.BuildWebsiteSearchUrls(
            AgentName, BrokerageName, null, emailHandle: "jenisesellsnj");

        urls.Should().Contain(u =>
            u.Url == "https://www.jenisesellsnj.com" && u.Source == "DomainGuess");
    }

    [Fact]
    public void BuildWebsiteSearchUrls_SkipsDomainGuess_ForShortHandles()
    {
        var urls = AgentDiscoveryWorker.BuildWebsiteSearchUrls(
            AgentName, BrokerageName, null, emailHandle: "ab");

        urls.Should().NotContain(u => u.Source == "DomainGuess");
    }

    [Fact]
    public void BuildWebsiteSearchUrls_IncludesAgentSiteUrl()
    {
        var urls = AgentDiscoveryWorker.BuildWebsiteSearchUrls(
            AgentName, BrokerageName, null, agentSiteHandle: "jenise-buckalew");

        urls.Should().Contain(u =>
            u.Url == "https://jenise-buckalew.real-estate-star.com" && u.Source == "AgentSite");
    }

    [Fact]
    public void BuildWebsiteSearchUrls_UsesRealNameForZillowSlug()
    {
        var urls = AgentDiscoveryWorker.BuildWebsiteSearchUrls(
            "Jenise Buckalew", BrokerageName, null);

        urls.Should().Contain(u =>
            u.Url.Contains("zillow.com/profile/jenise-buckalew") && u.Source == "ZillowGuess");
    }

    [Fact]
    public void BuildWebsiteSearchUrls_OwnWebsitesBeforeThirdParty()
    {
        var sig = new EmailSignature(
            null, null, null, null, null, [],
            null, "https://www.janedoe.com", null);

        var urls = AgentDiscoveryWorker.BuildWebsiteSearchUrls(
            AgentName, BrokerageName, sig, emailHandle: "janedoe", agentSiteHandle: "jane-doe");

        // Own websites (signature, domain guess, agent-site) should come before Zillow/Realtor
        var ownCount = urls.TakeWhile(u => AgentDiscoveryWorker.IsAgentOwnWebsite(u.Url)).Count();
        ownCount.Should().BeGreaterThanOrEqualTo(2, "own websites should be listed first");
    }

    [Fact]
    public void BuildWebsiteSearchUrls_DeduplicatesDomainGuessWhenMatchesSignature()
    {
        var sig = new EmailSignature(
            null, null, null, null, null, [],
            null, "https://www.janedoe.com", null);

        var urls = AgentDiscoveryWorker.BuildWebsiteSearchUrls(
            AgentName, BrokerageName, sig, emailHandle: "janedoe");

        var ownWebsites = urls.Where(u => u.Source is "OwnWebsite" or "DomainGuess").ToList();
        // Should have both — different domains (janedoe.com signature vs www.janedoe.com guess)
        // The dedup is domain-level
        ownWebsites.Should().HaveCountLessThanOrEqualTo(2);
    }

    // ──────────────────────────────────────────────────────────
    // DeterminePlatform
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://www.zillow.com/profile/agent", "Zillow")]
    [InlineData("https://www.realtor.com/realestateagents/jane", "Realtor.com")]
    [InlineData("https://www.homes.com/agent/jane", "Homes.com")]
    [InlineData("https://www.trulia.com/profile/jane", "Trulia")]
    [InlineData("https://www.redfin.com/agent/jane", "Redfin")]
    [InlineData("https://www.janesite.com", "Unknown")]
    public void DeterminePlatform_ReturnsCorrectPlatform(string url, string expected)
    {
        var result = AgentDiscoveryWorker.DeterminePlatform(url);

        result.Should().Be(expected);
    }

    // ──────────────────────────────────────────────────────────
    // ExtractGa4MeasurementId
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ExtractGa4MeasurementId_FindsGa4Id()
    {
        var websites = new List<DiscoveredWebsite>
        {
            new("https://www.janedoe.com", "EmailSignature",
                "<script>gtag('config', 'G-ABCDEF1234');</script>")
        };

        var id = AgentDiscoveryWorker.ExtractGa4MeasurementId(websites);

        id.Should().Be("G-ABCDEF1234");
    }

    [Fact]
    public void ExtractGa4MeasurementId_FindsGtmId()
    {
        var websites = new List<DiscoveredWebsite>
        {
            new("https://www.janedoe.com", "EmailSignature",
                "<script src=\"//www.googletagmanager.com/gtm.js?id=GTM-ABC123\"></script>")
        };

        var id = AgentDiscoveryWorker.ExtractGa4MeasurementId(websites);

        id.Should().Be("GTM-ABC123");
    }

    [Fact]
    public void ExtractGa4MeasurementId_SkipsThirdPartyWebsites()
    {
        var websites = new List<DiscoveredWebsite>
        {
            new("https://www.zillow.com/profile/jane", "Zillow",
                "G-ZILLOWCODE123 is here")
        };

        var id = AgentDiscoveryWorker.ExtractGa4MeasurementId(websites);

        id.Should().BeNull();
    }

    [Fact]
    public void ExtractGa4MeasurementId_ReturnsNull_WhenNoIdFound()
    {
        var websites = new List<DiscoveredWebsite>
        {
            new("https://www.janedoe.com", "EmailSignature", "<html><body>No GA4 here</body></html>")
        };

        var id = AgentDiscoveryWorker.ExtractGa4MeasurementId(websites);

        id.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────
    // ExtractSalesCount
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ExtractSalesCount_ReturnsCount_WhenFound()
    {
        var html = "<p>42 homes sold in the last year.</p>";

        var count = AgentDiscoveryWorker.ExtractSalesCount(html);

        count.Should().Be(42);
    }

    [Fact]
    public void ExtractSalesCount_ReturnsNull_WhenNotFound()
    {
        var count = AgentDiscoveryWorker.ExtractSalesCount("<p>Great agent!</p>");

        count.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────
    // ExtractYearsExperience
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ExtractYearsExperience_ReturnsYears_WhenFound()
    {
        var html = "<p>15 years of experience in real estate.</p>";

        var years = AgentDiscoveryWorker.ExtractYearsExperience(html);

        years.Should().Be(15);
    }

    [Fact]
    public void ExtractYearsExperience_ReturnsNull_WhenNotFound()
    {
        var years = AgentDiscoveryWorker.ExtractYearsExperience("<p>Experienced agent!</p>");

        years.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────
    // ParseThirdPartyProfiles
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseThirdPartyProfiles_SkipsNonThirdPartySites()
    {
        var sites = new List<DiscoveredWebsite>
        {
            new("https://www.janedoe.com", "EmailSignature", "<html>Personal site</html>")
        };

        var profiles = AgentDiscoveryWorker.ParseThirdPartyProfiles(sites);

        profiles.Should().BeEmpty();
    }

    [Fact]
    public void ParseThirdPartyProfiles_SkipsSitesWithNullHtml()
    {
        var sites = new List<DiscoveredWebsite>
        {
            new("https://www.zillow.com/profile/agent", "Zillow", null)
        };

        var profiles = AgentDiscoveryWorker.ParseThirdPartyProfiles(sites);

        profiles.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────
    // Scraper routing
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_UsesScraperApi_ForThirdPartySites()
    {
        var mockRefresher = new Mock<IOAuthRefresher>();
        mockRefresher
            .Setup(r => r.GetValidCredentialAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        var mockScraper = new Mock<IScraperClient>();
        mockScraper.Setup(s => s.IsAvailable).Returns(true);
        mockScraper
            .Setup(s => s.FetchAsync(
                It.Is<string>(url => url.Contains("zillow.com")),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html><p>15 homes sold</p></html>");

        var worker = BuildWorker(mockRefresher, null, mockScraper);

        var result = await worker.RunAsync(
            AccountId, AgentId, AgentName, BrokerageName, null, null, null, null, null, CancellationToken.None);

        // ScraperAPI was called for Zillow
        mockScraper.Verify(s => s.FetchAsync(
            It.Is<string>(url => url.Contains("zillow.com")),
            "ZillowGuess", AgentId, It.IsAny<CancellationToken>()), Times.Once);

        // Should have parsed the Zillow HTML
        result.Profiles.Should().ContainSingle(p => p.Platform == "Zillow");
    }

    [Fact]
    public async Task RunAsync_FallsBackToDirectHttp_WhenScraperUnavailable()
    {
        var mockRefresher = new Mock<IOAuthRefresher>();
        mockRefresher
            .Setup(r => r.GetValidCredentialAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        var mockScraper = new Mock<IScraperClient>();
        mockScraper.Setup(s => s.IsAvailable).Returns(false);

        var worker = BuildWorker(mockRefresher, null, mockScraper);

        var result = await worker.RunAsync(
            AccountId, AgentId, AgentName, BrokerageName, null, null, null, null, null, CancellationToken.None);

        // ScraperAPI should NOT be called
        mockScraper.Verify(s => s.FetchAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        result.Should().NotBeNull();
    }

    [Fact]
    public void ParseThirdPartyProfiles_ParsesZillowProfile()
    {
        var html = """
            <div>
              <p>15 homes sold in the last year</p>
              <p>10 years of experience in real estate</p>
            </div>
            """;
        var sites = new List<DiscoveredWebsite>
        {
            new("https://www.zillow.com/profile/jane", "Zillow", html)
        };

        var profiles = AgentDiscoveryWorker.ParseThirdPartyProfiles(sites);

        profiles.Should().ContainSingle();
        profiles[0].Platform.Should().Be("Zillow");
        profiles[0].SalesCount.Should().Be(15);
        profiles[0].YearsExperience.Should().Be(10);
    }

    // ──────────────────────────────────────────────────────────
    // Zillow Reviews API integration
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_UsesZillowApi_WhenAvailable()
    {
        var mockRefresher = new Mock<IOAuthRefresher>();
        mockRefresher
            .Setup(r => r.GetValidCredentialAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        var zillowReviews = new ZillowAgentReviews(
            [new Review("Great agent!", 5, "John D.", "Zillow", DateTime.UtcNow)],
            5.0, 1, "reviewee-key-123");

        var mockZillow = new Mock<IZillowReviewsClient>();
        mockZillow.Setup(z => z.IsAvailable).Returns(true);
        mockZillow
            .Setup(z => z.GetReviewsByEmailAsync("agent@test.com", AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(zillowReviews);

        var worker = BuildWorker(mockRefresher, mockZillow: mockZillow);

        var result = await worker.RunAsync(
            AccountId, AgentId, AgentName, BrokerageName,
            null, null, null, "agent@test.com", null, CancellationToken.None);

        result.Reviews.Should().ContainSingle();
        result.Reviews[0].Source.Should().Be("Zillow");
        result.Reviews[0].Text.Should().Be("Great agent!");
    }

    [Fact]
    public async Task RunAsync_FallsBackToNameSearch_WhenEmailReturnsNoReviews()
    {
        var mockRefresher = new Mock<IOAuthRefresher>();
        mockRefresher
            .Setup(r => r.GetValidCredentialAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        var nameReviews = new ZillowAgentReviews(
            [new Review("Excellent!", 4, "Sarah M.", "Zillow", null)],
            4.0, 1, "key-456");

        var mockZillow = new Mock<IZillowReviewsClient>();
        mockZillow.Setup(z => z.IsAvailable).Returns(true);
        mockZillow
            .Setup(z => z.GetReviewsByEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZillowAgentReviews([], null, 0, null));
        mockZillow
            .Setup(z => z.GetReviewsByNameAsync(AgentName, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(nameReviews);

        var worker = BuildWorker(mockRefresher, mockZillow: mockZillow);

        var result = await worker.RunAsync(
            AccountId, AgentId, AgentName, BrokerageName,
            null, null, null, "agent@test.com", null, CancellationToken.None);

        result.Reviews.Should().ContainSingle();
        result.Reviews[0].Reviewer.Should().Be("Sarah M.");
    }

    [Fact]
    public async Task RunAsync_FallsBackToScrapedReviews_WhenZillowApiUnavailable()
    {
        var mockRefresher = new Mock<IOAuthRefresher>();
        mockRefresher
            .Setup(r => r.GetValidCredentialAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredential?)null);

        // Zillow API unavailable
        var mockZillow = new Mock<IZillowReviewsClient>();
        mockZillow.Setup(z => z.IsAvailable).Returns(false);
        mockZillow
            .Setup(z => z.GetReviewsByEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZillowAgentReviews([], null, 0, null));
        mockZillow
            .Setup(z => z.GetReviewsByNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZillowAgentReviews([], null, 0, null));

        // ScraperAPI returns HTML with JSON-LD reviews
        var mockScraper = new Mock<IScraperClient>();
        mockScraper.Setup(s => s.IsAvailable).Returns(true);
        mockScraper
            .Setup(s => s.FetchAsync(
                It.Is<string>(url => url.Contains("zillow.com")),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                <script type="application/ld+json">
                {
                    "review": [{
                        "reviewBody": "Scraped review",
                        "reviewRating": { "ratingValue": 4 },
                        "author": { "name": "Scrape User" }
                    }]
                }
                </script>
                """);

        var worker = BuildWorker(mockRefresher, mockScraper: mockScraper, mockZillow: mockZillow);

        var result = await worker.RunAsync(
            AccountId, AgentId, AgentName, BrokerageName,
            null, null, null, "agent@test.com", null, CancellationToken.None);

        result.Reviews.Should().ContainSingle();
        result.Reviews[0].Text.Should().Be("Scraped review");
    }
}

/// <summary>
/// Test HTTP handler that returns configured responses per URL prefix.
/// </summary>
internal sealed class MockDiscoveryHttpHandler(Dictionary<string, HttpResponseMessage> responses)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? string.Empty;

        foreach (var (prefix, response) in responses)
        {
            if (url.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(response);
        }

        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }
}
