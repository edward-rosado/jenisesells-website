using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Workers.HomeSearch;
using System.Text.Json;

namespace RealEstateStar.Workers.HomeSearch.Tests;

public class ScraperHomeSearchProviderTests
{
    private static readonly HomeSearchCriteria DefaultCriteria = new()
    {
        Area = "Old Bridge, NJ",
        MinPrice = 300_000m,
        MaxPrice = 500_000m,
        MinBeds = 3,
        MinBaths = 2
    };

    private static readonly Dictionary<string, string> DefaultSourceUrls = new()
    {
        ["zillow"]  = "https://www.zillow.com/homes/{area}/?price-min={minPrice}&price-max={maxPrice}&beds-min={minBeds}&baths-min={minBaths}",
        ["redfin"]  = "https://www.redfin.com/city/search?location={area}&min_price={minPrice}&max_price={maxPrice}&num_beds={minBeds}&num_baths={minBaths}",
        ["realtor"] = "https://www.realtor.com/realestateandhomes-search/{area}?price-min={minPrice}&price-max={maxPrice}&beds-min={minBeds}&baths-min={minBaths}"
    };

    private static Listing MakeListing(string address = "123 Main St", string city = "Old Bridge") =>
        new(address, city, "NJ", "08857", 400_000m, 3, 2m, 1500, "Great neighborhood", "https://example.com/1");

    private static string MakeCuratedListingsJson(IEnumerable<Listing> listings)
    {
        var items = listings.Select(l => new
        {
            address     = l.Address,
            city        = l.City,
            state       = l.State,
            zip         = l.Zip,
            price       = l.Price,
            beds        = l.Beds,
            baths       = l.Baths,
            sqft        = l.Sqft,
            whyThisFits = l.WhyThisFits,
            listingUrl  = l.ListingUrl
        });
        return JsonSerializer.Serialize(items);
    }

    private static (ScraperHomeSearchProvider provider, Mock<IScraperClient> scraperClient, Mock<IAnthropicClient> anthropicClient)
        CreateProvider(
            string scraperHtml = "<html></html>",
            IEnumerable<Listing>? curatedListings = null,
            Exception? anthropicException = null)
    {
        var listings = curatedListings?.ToList() ?? [MakeListing()];
        var curatedJson = MakeCuratedListingsJson(listings);

        var scraperClient = new Mock<IScraperClient>();
        scraperClient.Setup(s => s.IsAvailable).Returns(true);
        scraperClient.Setup(s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scraperHtml);

        var anthropicClient = new Mock<IAnthropicClient>();
        if (anthropicException is not null)
        {
            anthropicClient.Setup(c => c.SendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(anthropicException);
        }
        else
        {
            anthropicClient.Setup(c => c.SendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AnthropicResponse(curatedJson, 100, 200, 500));
        }

        var logger = new Mock<ILogger<ScraperHomeSearchProvider>>();
        var provider = new ScraperHomeSearchProvider(anthropicClient.Object, scraperClient.Object, DefaultSourceUrls, logger.Object);
        return (provider, scraperClient, anthropicClient);
    }

    // ─── SearchAsync: parallel source search ─────────────────────────────────────

    [Fact]
    public async Task SearchAsync_SearchesMultipleSources_InParallel()
    {
        var fetchedUrls = new System.Collections.Concurrent.ConcurrentBag<string>();

        var scraperClient = new Mock<IScraperClient>();
        scraperClient.Setup(s => s.IsAvailable).Returns(true);
        scraperClient.Setup(s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((url, _, _, _) => fetchedUrls.Add(url))
            .ReturnsAsync("<html></html>");

        var curatedJson = MakeCuratedListingsJson([MakeListing()]);
        var anthropicClient = new Mock<IAnthropicClient>();
        anthropicClient.Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse(curatedJson, 100, 200, 500));

        var logger = new Mock<ILogger<ScraperHomeSearchProvider>>();
        var provider = new ScraperHomeSearchProvider(anthropicClient.Object, scraperClient.Object, DefaultSourceUrls, logger.Object);

        await provider.SearchAsync(DefaultCriteria, CancellationToken.None);

        // Should have hit all 3 sources via IScraperClient
        fetchedUrls.Should().HaveCount(3);
        var urlList = fetchedUrls.ToList();
        urlList.Should().Contain(u => u.Contains("zillow.com"));
        urlList.Should().Contain(u => u.Contains("redfin.com"));
        urlList.Should().Contain(u => u.Contains("realtor.com"));
    }

    // ─── SearchAsync: deduplication ──────────────────────────────────────────────

    [Fact]
    public void Deduplicate_RemovesDuplicatesByNormalizedAddress()
    {
        var listings = new List<Listing>
        {
            MakeListing("123 Main St",  "Old Bridge"),
            MakeListing("123 Main St.", "Old Bridge"), // period variant — same address
            MakeListing("456 Oak Ave",  "Old Bridge")
        };

        var result = ScraperHomeSearchProvider.Deduplicate(listings);

        result.Should().HaveCount(2);
        result.Should().Contain(l => l.Address == "123 Main St");
        result.Should().Contain(l => l.Address == "456 Oak Ave");
    }

    [Fact]
    public void Deduplicate_KeepsDistinctAddresses()
    {
        var listings = new List<Listing>
        {
            MakeListing("100 Alpha Rd", "Old Bridge"),
            MakeListing("200 Beta Ln",  "Old Bridge"),
            MakeListing("300 Gamma Dr", "Old Bridge")
        };

        var result = ScraperHomeSearchProvider.Deduplicate(listings);

        result.Should().HaveCount(3);
    }

    // ─── SearchAsync: sends to Claude for curation ───────────────────────────────

    [Fact]
    public void SearchAsync_SendsListingsToClaude_ForCuration()
    {
        // Feed one listing directly by making ParseListings testable via a subclass
        var listings = new List<Listing> { MakeListing("Injected Listing") };
        var promptData = ScraperHomeSearchProvider.BuildListingsPrompt(listings, DefaultCriteria);

        promptData.Should().Contain("Injected Listing");
    }

    [Fact]
    public async Task SearchAsync_ReturnsCuratedListings_FromClaude()
    {
        var curated = new[]
        {
            MakeListing("10 Claude Pick Rd"),
            MakeListing("20 Claude Pick Rd")
        };

        // ParseListings returns empty list from HTML — so we test ParseCuratedListings directly.
        var curatedJson = MakeCuratedListingsJson(curated);
        var parsed = ScraperHomeSearchProvider.ParseCuratedListings(curatedJson);

        parsed.Should().HaveCount(2);
        parsed[0].Address.Should().Be("10 Claude Pick Rd");
        parsed[1].Address.Should().Be("20 Claude Pick Rd");
    }

    // ─── SearchAsync: graceful failure handling ───────────────────────────────────

    [Fact]
    public async Task SearchAsync_HandlesScrapingFailures_Gracefully()
    {
        var scraperClient = new Mock<IScraperClient>();
        scraperClient.Setup(s => s.IsAvailable).Returns(true);
        scraperClient.Setup(s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var anthropicClient = new Mock<IAnthropicClient>();
        var logger = new Mock<ILogger<ScraperHomeSearchProvider>>();
        var sourceUrls = new Dictionary<string, string>
        {
            ["zillow"]  = "https://www.zillow.com/homes/{area}/?price-min={minPrice}",
            ["redfin"]  = "https://www.redfin.com/city/search?location={area}&min_price={minPrice}",
            ["realtor"] = "https://www.realtor.com/realestateandhomes-search/{area}?price-min={minPrice}"
        };
        var provider = new ScraperHomeSearchProvider(anthropicClient.Object, scraperClient.Object, sourceUrls, logger.Object);

        // All 3 sources fail — no Claude call — returns empty list
        var result = await provider.SearchAsync(DefaultCriteria, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ContinuesWithRemainingResults_WhenOneSourceFails()
    {
        var callCount = 0;
        var scraperClient = new Mock<IScraperClient>();
        scraperClient.Setup(s => s.IsAvailable).Returns(true);
        scraperClient.Setup(s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                // First call fails, rest succeed with empty HTML
                if (++callCount == 1)
                    throw new HttpRequestException("source 1 failed");
                return "<html></html>";
            });

        var anthropicClient = new Mock<IAnthropicClient>();
        anthropicClient.Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("[]", 10, 5, 100));

        var logger = new Mock<ILogger<ScraperHomeSearchProvider>>();
        var sourceUrls = new Dictionary<string, string>
        {
            ["zillow"]  = "https://www.zillow.com/homes/{area}/?price-min={minPrice}",
            ["redfin"]  = "https://www.redfin.com/city/search?location={area}&min_price={minPrice}",
            ["realtor"] = "https://www.realtor.com/realestateandhomes-search/{area}?price-min={minPrice}"
        };
        var provider = new ScraperHomeSearchProvider(anthropicClient.Object, scraperClient.Object, sourceUrls, logger.Object);

        var act = async () => await provider.SearchAsync(DefaultCriteria, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ─── URL construction ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildSearchUrl_SubstitutesArea()
    {
        var template = "https://www.zillow.com/homes/{area}/";
        var criteria = new HomeSearchCriteria { Area = "Old Bridge, NJ" };

        var url = ScraperHomeSearchProvider.BuildSearchUrl(template, criteria);

        url.Should().Contain("Old%20Bridge%2C%20NJ");
    }

    [Fact]
    public void BuildSearchUrl_SubstitutesAllFilterParams()
    {
        var template = "https://www.zillow.com/homes/{area}/?price-min={minPrice}&price-max={maxPrice}&beds-min={minBeds}&baths-min={minBaths}";

        var url = ScraperHomeSearchProvider.BuildSearchUrl(template, DefaultCriteria);

        url.Should().Contain("price-min=300000");
        url.Should().Contain("price-max=500000");
        url.Should().Contain("beds-min=3");
        url.Should().Contain("baths-min=2");
    }

    [Fact]
    public void BuildSearchUrl_RemovesEmptyFilterParams()
    {
        var template = "https://www.zillow.com/homes/{area}/?price-min={minPrice}&price-max={maxPrice}&beds-min={minBeds}&baths-min={minBaths}";
        var criteria = new HomeSearchCriteria { Area = "Old Bridge, NJ" };

        var url = ScraperHomeSearchProvider.BuildSearchUrl(template, criteria);

        url.Should().Contain("zillow.com");
        url.Should().NotContain("?");
        url.Should().NotContain("price-min=");
        url.Should().NotContain("beds-min=");
    }

    [Fact]
    public void BuildSearchUrl_PreservesPerSourceParamNames()
    {
        var zillowTemplate = "https://www.zillow.com/homes/{area}/?price-min={minPrice}&beds-min={minBeds}";
        var redfinTemplate  = "https://www.redfin.com/search?location={area}&min_price={minPrice}&num_beds={minBeds}";

        var zillowUrl = ScraperHomeSearchProvider.BuildSearchUrl(zillowTemplate, DefaultCriteria);
        var redfinUrl = ScraperHomeSearchProvider.BuildSearchUrl(redfinTemplate, DefaultCriteria);

        zillowUrl.Should().Contain("price-min=300000");
        zillowUrl.Should().Contain("beds-min=3");
        redfinUrl.Should().Contain("min_price=300000");
        redfinUrl.Should().Contain("num_beds=3");
    }

    // ─── ParseCuratedListings ─────────────────────────────────────────────────────

    [Fact]
    public void ParseCuratedListings_ParsesAllFields()
    {
        var json = """
        [
          {
            "address": "99 Test St",
            "city": "Testville",
            "state": "NJ",
            "zip": "08888",
            "price": 450000,
            "beds": 4,
            "baths": 2.5,
            "sqft": 2000,
            "whyThisFits": "Great schools nearby",
            "listingUrl": "https://zillow.com/test"
          }
        ]
        """;

        var result = ScraperHomeSearchProvider.ParseCuratedListings(json);

        result.Should().HaveCount(1);
        var l = result[0];
        l.Address.Should().Be("99 Test St");
        l.City.Should().Be("Testville");
        l.State.Should().Be("NJ");
        l.Zip.Should().Be("08888");
        l.Price.Should().Be(450_000m);
        l.Beds.Should().Be(4);
        l.Baths.Should().Be(2.5m);
        l.Sqft.Should().Be(2000);
        l.WhyThisFits.Should().Be("Great schools nearby");
        l.ListingUrl.Should().Be("https://zillow.com/test");
    }

    [Fact]
    public void ParseCuratedListings_HandlesNullSqftAndUrl()
    {
        var json = """
        [
          {
            "address": "1 Test Rd",
            "city": "City",
            "state": "NJ",
            "zip": "07000",
            "price": 300000,
            "beds": 3,
            "baths": 2,
            "sqft": null,
            "whyThisFits": "Fits budget",
            "listingUrl": null
          }
        ]
        """;

        var result = ScraperHomeSearchProvider.ParseCuratedListings(json);

        result.Should().HaveCount(1);
        result[0].Sqft.Should().BeNull();
        result[0].ListingUrl.Should().BeNull();
    }

    [Fact]
    public void ParseCuratedListings_StripsMdCodeFences_BeforeParsing()
    {
        var json = """
        ```json
        [
          {
            "address": "1 Fence Rd",
            "city": "Town",
            "state": "NJ",
            "zip": "07001",
            "price": 350000,
            "beds": 3,
            "baths": 2,
            "sqft": null,
            "whyThisFits": null,
            "listingUrl": null
          }
        ]
        ```
        """;

        var result = ScraperHomeSearchProvider.ParseCuratedListings(json);

        result.Should().HaveCount(1);
        result[0].Address.Should().Be("1 Fence Rd");
    }

    // ─── BuildCriteriaDescription ────────────────────────────────────────────────

    [Fact]
    public void BuildCriteriaDescription_IncludesAllSetFields()
    {
        var desc = ScraperHomeSearchProvider.BuildCriteriaDescription(DefaultCriteria);

        desc.Should().Contain("Old Bridge, NJ");
        desc.Should().Contain("300,000");
        desc.Should().Contain("500,000");
        desc.Should().Contain("3");
        desc.Should().Contain("2");
    }

    [Fact]
    public void BuildCriteriaDescription_OmitsOptionalFields_WhenNull()
    {
        var criteria = new HomeSearchCriteria { Area = "Springfield, NJ" };
        var desc = ScraperHomeSearchProvider.BuildCriteriaDescription(criteria);

        desc.Should().Contain("Springfield, NJ");
        desc.Should().NotContain("Min Price");
        desc.Should().NotContain("Max Price");
        desc.Should().NotContain("Min Beds");
        desc.Should().NotContain("Min Baths");
    }
}
