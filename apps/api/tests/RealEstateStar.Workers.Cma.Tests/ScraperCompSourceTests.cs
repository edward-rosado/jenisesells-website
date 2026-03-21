using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Workers.Cma;

namespace RealEstateStar.Workers.Cma.Tests;

public class ScraperCompSourceTests
{
    private static CompSearchRequest MakeRequest(
        string address = "123 Main St",
        string city = "Springfield",
        string state = "NJ",
        string zip = "07081",
        int? beds = 3,
        int? baths = 2,
        int? sqft = 1800) => new()
    {
        Address = address,
        City = city,
        State = state,
        Zip = zip,
        Beds = beds,
        Baths = baths,
        SqFt = sqft
    };

    /// <summary>Builds a single valid comp JSON object string.</summary>
    private static string CompJson(
        string address = "456 Oak Ave",
        decimal salePrice = 500_000m,
        string saleDate = "2025-02-10",
        int beds = 3,
        int baths = 2,
        int sqft = 1800,
        double distanceMiles = 0.4,
        int? daysOnMarket = 14) =>
        $$"""
        {
            "address": "{{address}}",
            "salePrice": {{salePrice}},
            "saleDate": "{{saleDate}}",
            "beds": {{beds}},
            "baths": {{baths}},
            "sqft": {{sqft}},
            "distanceMiles": {{distanceMiles}}{{(daysOnMarket.HasValue ? $",\n            \"daysOnMarket\": {daysOnMarket}" : "")}}
        }
        """;

    private static string WrapArray(params string[] items) =>
        $"[{string.Join(",", items)}]";

    // ---------------------------------------------------------------------------
    // BuildSlug
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildSlug_CreatesUrlSafeSlug()
    {
        var request = MakeRequest("123 Main St", "Springfield", "NJ", "07081");

        var slug = ScraperCompSource.BuildSlug(request);

        slug.Should().Be("123-main-st-springfield-nj-07081");
    }

    // ---------------------------------------------------------------------------
    // BuildCriteriaDescription
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildCriteriaDescription_IncludesAllFields()
    {
        var request = MakeRequest();

        var description = ScraperCompSource.BuildCriteriaDescription(request);

        description.Should().Contain("123 Main St");
        description.Should().Contain("Springfield");
        description.Should().Contain("NJ");
        description.Should().Contain("07081");
        description.Should().Contain("Beds: 3");
        description.Should().Contain("Baths: 2");
        description.Should().Contain("SqFt: 1800");
    }

    // ---------------------------------------------------------------------------
    // ParseComps — happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseComps_ValidJson_ReturnsList()
    {
        var json = WrapArray(CompJson("456 Oak Ave"), CompJson("789 Pine Rd"));

        var result = ScraperCompSource.ParseComps(json, CompSource.Zillow, NullLogger.Instance);

        result.Should().HaveCount(2);
        result[0].Address.Should().Be("456 Oak Ave");
        result[1].Address.Should().Be("789 Pine Rd");
        result.Should().AllSatisfy(c => c.Source.Should().Be(CompSource.Zillow));
    }

    // ---------------------------------------------------------------------------
    // ParseComps — filtering invalid records
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseComps_SkipsRecordsWithNegativePrice()
    {
        var json = WrapArray(
            CompJson("Good St", salePrice: 500_000m),
            CompJson("Bad St", salePrice: -1m));

        var result = ScraperCompSource.ParseComps(json, CompSource.Zillow, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Address.Should().Be("Good St");
    }

    [Fact]
    public void ParseComps_SkipsRecordsWithZeroSqft()
    {
        var json = WrapArray(
            CompJson("Good St", sqft: 1800),
            CompJson("Bad St", sqft: 0));

        var result = ScraperCompSource.ParseComps(json, CompSource.Zillow, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Address.Should().Be("Good St");
    }

    [Fact]
    public void ParseComps_SkipsInvalidDate()
    {
        var json = WrapArray(
            CompJson("Good St", saleDate: "2025-02-10"),
            CompJson("Bad St", saleDate: "not-a-date"));

        var result = ScraperCompSource.ParseComps(json, CompSource.Zillow, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Address.Should().Be("Good St");
    }

    [Fact]
    public void ParseComps_HandlesEmptyArray()
    {
        var result = ScraperCompSource.ParseComps("[]", CompSource.Redfin, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // ParseComps — markdown code fence stripping
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseComps_StripMarkdownCodeFences()
    {
        var inner = WrapArray(CompJson("456 Oak Ave"));
        var fenced = $"```json\n{inner}\n```";

        var result = ScraperCompSource.ParseComps(fenced, CompSource.Zillow, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Address.Should().Be("456 Oak Ave");
    }

    // ---------------------------------------------------------------------------
    // ParseComps — invalid JSON
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseComps_InvalidJson_ReturnsEmpty()
    {
        var result = ScraperCompSource.ParseComps("this is not json", CompSource.Redfin, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // ParseComps — empty/whitespace address skipped
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseComps_SkipsRecordWithWhitespaceAddress()
    {
        var goodComp = CompJson("Good St");
        var badComp = CompJson("   "); // whitespace-only address
        var json = WrapArray(goodComp, badComp);

        var result = ScraperCompSource.ParseComps(json, CompSource.Zillow, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Address.Should().Be("Good St");
    }

    [Fact]
    public void ParseComps_SkipsRecordWithEmptyAddress()
    {
        var goodComp = CompJson("Good St");
        var badComp = CompJson(""); // empty address
        var json = WrapArray(goodComp, badComp);

        var result = ScraperCompSource.ParseComps(json, CompSource.Zillow, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Address.Should().Be("Good St");
    }

    // ---------------------------------------------------------------------------
    // ParseComps — daysOnMarket null vs number
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseComps_DaysOnMarketNull_HasNullDaysOnMarket()
    {
        // Manually craft JSON with explicit null for daysOnMarket
        var compWithNullDom = """
        {
            "address": "456 Oak Ave",
            "salePrice": 500000,
            "saleDate": "2025-02-10",
            "beds": 3,
            "baths": 2,
            "sqft": 1800,
            "distanceMiles": 0.4,
            "daysOnMarket": null
        }
        """;
        var json = WrapArray(compWithNullDom);

        var result = ScraperCompSource.ParseComps(json, CompSource.Zillow, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].DaysOnMarket.Should().BeNull();
    }

    [Fact]
    public void ParseComps_DaysOnMarketNumber_HasIntDaysOnMarket()
    {
        var json = WrapArray(CompJson(daysOnMarket: 30));

        var result = ScraperCompSource.ParseComps(json, CompSource.Zillow, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].DaysOnMarket.Should().Be(30);
    }

    // ---------------------------------------------------------------------------
    // ParseComps — missing required property falls into catch block
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseComps_MissingRequiredProperty_SkipsRecordViaCatch()
    {
        // "beds" is missing — GetProperty("beds") will throw KeyNotFoundException
        var missingBeds = """
        {
            "address": "Bad St",
            "salePrice": 500000,
            "saleDate": "2025-02-10",
            "baths": 2,
            "sqft": 1800,
            "distanceMiles": 0.4
        }
        """;
        var goodComp = CompJson("Good St");
        var json = WrapArray(missingBeds, goodComp);

        var result = ScraperCompSource.ParseComps(json, CompSource.Zillow, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Address.Should().Be("Good St");
    }

    // ---------------------------------------------------------------------------
    // BuildCriteriaDescription — null beds/baths/sqft
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildCriteriaDescription_NullBedsBathsSqft_DoesNotIncludeThoseLines()
    {
        var request = MakeRequest(beds: null, baths: null, sqft: null);

        var description = ScraperCompSource.BuildCriteriaDescription(request);

        description.Should().NotContain("Beds:");
        description.Should().NotContain("Baths:");
        description.Should().NotContain("SqFt:");
    }

    // ---------------------------------------------------------------------------
    // ParseComps — null JSON value for "address" (hits the ?? "" null branch)
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseComps_NullJsonAddress_TreatsAsEmptyAndSkips()
    {
        // "address": null → GetString() returns null → ?? "" → empty → skipped by IsNullOrWhiteSpace
        var compWithNullAddress = """
        {
            "address": null,
            "salePrice": 500000,
            "saleDate": "2025-02-10",
            "beds": 3,
            "baths": 2,
            "sqft": 1800,
            "distanceMiles": 0.4
        }
        """;
        var goodComp = CompJson("Good St");
        var json = WrapArray(compWithNullAddress, goodComp);

        var result = ScraperCompSource.ParseComps(json, CompSource.Zillow, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Address.Should().Be("Good St");
    }

    // ---------------------------------------------------------------------------
    // ParseComps — null JSON value for "saleDate" (hits the ?? "" null branch)
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseComps_NullJsonSaleDate_TreatsAsEmptyStringAndSkips()
    {
        // "saleDate": null → GetString() returns null → ?? "" → fails DateOnly.TryParse → skipped
        var compWithNullDate = """
        {
            "address": "456 Oak Ave",
            "salePrice": 500000,
            "saleDate": null,
            "beds": 3,
            "baths": 2,
            "sqft": 1800,
            "distanceMiles": 0.4
        }
        """;
        var goodComp = CompJson("Good St");
        var json = WrapArray(compWithNullDate, goodComp);

        var result = ScraperCompSource.ParseComps(json, CompSource.Zillow, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Address.Should().Be("Good St");
    }
}
