using FluentAssertions;
using RealEstateStar.Domain.Cma.Models;

namespace RealEstateStar.Workers.Lead.CMA.Tests;

public class ClaudeCmaAnalyzerTests
{
    private static RealEstateStar.Domain.Leads.Models.Lead MakeLead() => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "test",
        LeadType = LeadType.Seller,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "j@e.com",
        Phone = "555",
        Timeline = "3-6 months",
        SellerDetails = new SellerDetails
        {
            Address = "123 Main St",
            City = "Springfield",
            State = "NJ",
            Zip = "07081",
            Beds = 3,
            Baths = 2,
            Sqft = 1800
        }
    };

    private static Comp MakeComp(string address = "456 Oak Ave") => new()
    {
        Address = address,
        SalePrice = 520_000m,
        SaleDate = new DateOnly(2025, 2, 10),
        Beds = 3,
        Baths = 2,
        Sqft = 1850,
        DistanceMiles = 0.4,
        Source = CompSource.Zillow
    };

    /// <summary>Builds minimal valid CMA JSON for ParseResponse tests.</summary>
    private static string MakeValidJson(
        decimal valueLow = 480_000m,
        decimal valueMid = 510_000m,
        decimal valueHigh = 540_000m,
        string marketTrend = "Seller's",
        string narrative = "Strong seller's market with low inventory.",
        int medianDom = 21) => $$"""
        {
            "valueLow": {{valueLow}},
            "valueMid": {{valueMid}},
            "valueHigh": {{valueHigh}},
            "marketNarrative": "{{narrative}}",
            "pricingRecommendation": "List at $510,000",
            "leadInsights": "Motivated seller.",
            "conversationStarters": ["How soon are you hoping to move?", "Have you toured comps?"],
            "marketTrend": "{{marketTrend}}",
            "medianDaysOnMarket": {{medianDom}}
        }
        """;

    // ---------------------------------------------------------------------------
    // BuildPrompt
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildPrompt_IncludesAddressAndBedsBathsSqft()
    {
        var lead = MakeLead();
        var prompt = ClaudeCmaAnalyzer.BuildPrompt(lead, []);

        prompt.Should().Contain("123 Main St");
        prompt.Should().Contain("Springfield");
        prompt.Should().Contain("NJ");
        prompt.Should().Contain("07081");
        prompt.Should().Contain("Beds: 3");
        prompt.Should().Contain("Baths: 2");
        prompt.Should().Contain("Sqft: 1800");
    }

    [Fact]
    public void BuildPrompt_IncludesAllComps()
    {
        var lead = MakeLead();
        var comps = new List<Comp>
        {
            MakeComp("456 Oak Ave"),
            MakeComp("789 Pine Rd")
        };

        var prompt = ClaudeCmaAnalyzer.BuildPrompt(lead, comps);

        prompt.Should().Contain("456 Oak Ave");
        prompt.Should().Contain("789 Pine Rd");
        prompt.Should().Contain("Comp 1");
        prompt.Should().Contain("Comp 2");
    }

    [Fact]
    public void BuildPrompt_RecentComp_AnnotatesWithRecentLabel()
    {
        var lead = MakeLead();
        var comp = new Comp
        {
            Address = "456 Oak Ave",
            SalePrice = 520_000m,
            SaleDate = new DateOnly(2025, 2, 10),
            Beds = 3,
            Baths = 2,
            Sqft = 1850,
            DistanceMiles = 0.4,
            Source = CompSource.Zillow,
            IsRecent = true
        };

        var prompt = ClaudeCmaAnalyzer.BuildPrompt(lead, [comp]);

        prompt.Should().Contain("[Recent]");
        prompt.Should().NotContain("[Older sale");
    }

    [Fact]
    public void BuildPrompt_OlderComp_AnnotatesWithMonthsAgoLabel()
    {
        var lead = MakeLead();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var saleDate = today.AddMonths(-9);
        var comp = new Comp
        {
            Address = "456 Oak Ave",
            SalePrice = 520_000m,
            SaleDate = saleDate,
            Beds = 3,
            Baths = 2,
            Sqft = 1850,
            DistanceMiles = 0.4,
            Source = CompSource.RentCast,
            IsRecent = false
        };

        var prompt = ClaudeCmaAnalyzer.BuildPrompt(lead, [comp]);

        prompt.Should().Contain("[Older sale — 9 months ago]");
        prompt.Should().NotContain("[Recent]");
    }

    [Fact]
    public void BuildPrompt_DoesNotIncludeSourceLine()
    {
        var lead = MakeLead();
        var comps = new List<Comp>
        {
            MakeComp("456 Oak Ave"),
            new Comp
            {
                Address = "789 Pine Rd",
                SalePrice = 510_000m,
                SaleDate = new DateOnly(2025, 1, 15),
                Beds = 3,
                Baths = 2,
                Sqft = 1800,
                DistanceMiles = 0.5,
                Source = CompSource.RentCast
            }
        };

        var prompt = ClaudeCmaAnalyzer.BuildPrompt(lead, comps);

        prompt.Should().NotContain("Source:");
        prompt.Should().NotContain("RentCast");
        prompt.Should().NotContain("Zillow");
    }

    // ---------------------------------------------------------------------------
    // ParseResponse — happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseResponse_ValidJson_ReturnsCmaAnalysis()
    {
        var json = MakeValidJson();

        var result = ClaudeCmaAnalyzer.ParseResponse(json);

        result.ValueLow.Should().Be(480_000m);
        result.ValueMid.Should().Be(510_000m);
        result.ValueHigh.Should().Be(540_000m);
        result.MarketTrend.Should().Be("Seller's");
        result.MarketNarrative.Should().Contain("Strong seller");
        result.MedianDaysOnMarket.Should().Be(21);
        result.ConversationStarters.Should().HaveCount(2);
    }

    // ---------------------------------------------------------------------------
    // ParseResponse — validation failures
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseResponse_ValueLowGreaterThanMid_ThrowsJsonException()
    {
        // valueLow (600k) > valueMid (510k) violates ordering constraint
        var json = MakeValidJson(valueLow: 600_000m, valueMid: 510_000m, valueHigh: 540_000m);

        var act = () => ClaudeCmaAnalyzer.ParseResponse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*valueLow <= valueMid*");
    }

    [Fact]
    public void ParseResponse_NegativeValues_ThrowsJsonException()
    {
        var json = MakeValidJson(valueLow: -1m, valueMid: 0m, valueHigh: 0m);

        var act = () => ClaudeCmaAnalyzer.ParseResponse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*>= 0*");
    }

    [Fact]
    public void ParseResponse_InvalidMarketTrend_ThrowsJsonException()
    {
        var json = MakeValidJson(marketTrend: "Booming");

        var act = () => ClaudeCmaAnalyzer.ParseResponse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unrecognized marketTrend*");
    }

    [Fact]
    public void ParseResponse_TruncatesLongNarrative()
    {
        var longNarrative = new string('x', 2500);
        // Use raw JSON construction here to avoid issues with template literal escaping
        var json = $$"""
        {
            "valueLow": 480000,
            "valueMid": 510000,
            "valueHigh": 540000,
            "marketNarrative": "{{longNarrative}}",
            "marketTrend": "Seller's",
            "medianDaysOnMarket": 21
        }
        """;

        var result = ClaudeCmaAnalyzer.ParseResponse(json);

        result.MarketNarrative.Should().HaveLength(2000);
    }

    [Fact]
    public void ParseResponse_NegativeMedianDom_ThrowsJsonException()
    {
        var json = MakeValidJson(medianDom: -5);

        var act = () => ClaudeCmaAnalyzer.ParseResponse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*medianDaysOnMarket*>= 0*");
    }

    // ---------------------------------------------------------------------------
    // ParseResponse — markdown code fence stripping
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseResponse_StripsMarkdownCodeFences_BeforeParsing()
    {
        var inner = MakeValidJson();
        var fenced = $"```json\n{inner}\n```";

        var result = ClaudeCmaAnalyzer.ParseResponse(fenced);

        result.ValueMid.Should().Be(510_000m);
    }

    // ---------------------------------------------------------------------------
    // ParseResponse — missing optional fields
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseResponse_MissingOptionalFields_ReturnsNullsAndEmptyStarters()
    {
        var json = """
        {
            "valueLow": 480000,
            "valueMid": 510000,
            "valueHigh": 540000,
            "marketNarrative": "Strong seller's market.",
            "marketTrend": "Seller's",
            "medianDaysOnMarket": 21
        }
        """;

        var result = ClaudeCmaAnalyzer.ParseResponse(json);

        result.PricingRecommendation.Should().BeNull();
        result.LeadInsights.Should().BeNull();
        result.ConversationStarters.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // ParseResponse — valueMid > valueHigh
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseResponse_ValueMidGreaterThanHigh_ThrowsWithOrderingMessage()
    {
        // valueLow=400k, valueMid=600k, valueHigh=500k → mid > high violates ordering
        var json = MakeValidJson(valueLow: 400_000m, valueMid: 600_000m, valueHigh: 500_000m);

        var act = () => ClaudeCmaAnalyzer.ParseResponse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*valueLow <= valueMid <= valueHigh*");
    }

    // ---------------------------------------------------------------------------
    // ParseResponse — empty narrative string
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseResponse_EmptyNarrative_ThrowsWithNarrativeMessage()
    {
        var json = MakeValidJson(narrative: "");

        var act = () => ClaudeCmaAnalyzer.ParseResponse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*marketNarrative was null or empty*");
    }

    // ---------------------------------------------------------------------------
    // ParseResponse — null items in conversationStarters are skipped
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseResponse_ConversationStartersWithNullItem_SkipsNulls()
    {
        var json = """
        {
            "valueLow": 480000,
            "valueMid": 510000,
            "valueHigh": 540000,
            "marketNarrative": "Strong seller's market.",
            "marketTrend": "Seller's",
            "medianDaysOnMarket": 21,
            "conversationStarters": [null, "How soon are you hoping to move?", null]
        }
        """;

        var result = ClaudeCmaAnalyzer.ParseResponse(json);

        result.ConversationStarters.Should().HaveCount(1);
        result.ConversationStarters[0].Should().Be("How soon are you hoping to move?");
    }

    // ---------------------------------------------------------------------------
    // BuildPrompt — null beds/baths/sqft omitted
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildPrompt_NullBedsBathsSqft_DoesNotIncludeThoseLines()
    {
        var lead = new RealEstateStar.Domain.Leads.Models.Lead
        {
            Id = Guid.NewGuid(),
            AgentId = "test",
            LeadType = LeadType.Seller,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "j@e.com",
            Phone = "555",
            Timeline = "3-6 months",
            SellerDetails = new SellerDetails
            {
                Address = "123 Main St",
                City = "Springfield",
                State = "NJ",
                Zip = "07081",
                Beds = null,
                Baths = null,
                Sqft = null
            }
        };

        var prompt = ClaudeCmaAnalyzer.BuildPrompt(lead, []);

        prompt.Should().NotContain("Beds:");
        prompt.Should().NotContain("Baths:");
        prompt.Should().NotContain("Sqft:");
    }

    // ---------------------------------------------------------------------------
    // BuildPrompt — DaysOnMarket present / absent
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildPrompt_CompWithDaysOnMarket_IncludesDaysOnMarketLine()
    {
        var lead = MakeLead();
        var comp = new Comp
        {
            Address = "456 Oak Ave",
            SalePrice = 520_000m,
            SaleDate = new DateOnly(2025, 2, 10),
            Beds = 3,
            Baths = 2,
            Sqft = 1850,
            DistanceMiles = 0.4,
            Source = CompSource.Zillow,
            DaysOnMarket = 14
        };

        var prompt = ClaudeCmaAnalyzer.BuildPrompt(lead, [comp]);

        prompt.Should().Contain("Days on Market: 14");
    }

    // ---------------------------------------------------------------------------
    // ParseResponse — null marketTrend from JSON (throws the ?? branch)
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseResponse_NullMarketTrend_ThrowsWithNullMessage()
    {
        var json = """
        {
            "valueLow": 480000,
            "valueMid": 510000,
            "valueHigh": 540000,
            "marketNarrative": "Strong seller's market.",
            "marketTrend": null,
            "medianDaysOnMarket": 21
        }
        """;

        var act = () => ClaudeCmaAnalyzer.ParseResponse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*marketTrend was null*");
    }

    // ---------------------------------------------------------------------------
    // ParseResponse — null marketNarrative from JSON (throws the ?? branch)
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseResponse_NullMarketNarrative_ThrowsWithNullMessage()
    {
        var json = """
        {
            "valueLow": 480000,
            "valueMid": 510000,
            "valueHigh": 540000,
            "marketNarrative": null,
            "marketTrend": "Seller's",
            "medianDaysOnMarket": 21
        }
        """;

        var act = () => ClaudeCmaAnalyzer.ParseResponse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*marketNarrative was null or empty*");
    }

    // ---------------------------------------------------------------------------
    // ParseResponse — malformed code fence (no newline after opening ```)
    //   exercises the false branch of: if (firstNewline >= 0 && lastFence > firstNewline)
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseResponse_MalformedCodeFenceNoNewline_StillParsesAsJson()
    {
        // The content after "```" has no newline — firstNewline will be -1,
        // so the fence-stripping inner branch is NOT taken.
        // The JSON extraction fallback finds the '{' and extracts valid JSON.
        var json = "```" + MakeValidJson().Replace('\n', ' ');

        var result = ClaudeCmaAnalyzer.ParseResponse(json);

        // The fallback successfully extracts JSON from the malformed input
        result.Should().NotBeNull();
        result.ValueLow.Should().BeGreaterThan(0);
    }

    [Fact]
    public void BuildPrompt_CompWithNullDaysOnMarket_OmitsDaysOnMarketLine()
    {
        var lead = MakeLead();
        var comp = new Comp
        {
            Address = "456 Oak Ave",
            SalePrice = 520_000m,
            SaleDate = new DateOnly(2025, 2, 10),
            Beds = 3,
            Baths = 2,
            Sqft = 1850,
            DistanceMiles = 0.4,
            Source = CompSource.Zillow,
            DaysOnMarket = null
        };

        var prompt = ClaudeCmaAnalyzer.BuildPrompt(lead, [comp]);

        prompt.Should().NotContain("Days on Market:");
    }
}
