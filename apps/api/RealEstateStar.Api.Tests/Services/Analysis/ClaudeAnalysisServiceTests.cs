using FluentAssertions;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services.Analysis;

namespace RealEstateStar.Api.Tests.Services.Analysis;

public class ClaudeAnalysisServiceTests
{
    private static Lead MakeLead() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-1234",
        Address = "123 Main St",
        City = "Old Bridge",
        State = "NJ",
        Zip = "08857",
        Timeline = "3-6 months",
        Beds = 3,
        Baths = 2,
        Sqft = 1800
    };

    private static Comp MakeComp(string address = "456 Oak Ave", decimal salePrice = 425_000m) => new()
    {
        Address = address,
        SalePrice = salePrice,
        SaleDate = new DateOnly(2026, 1, 15),
        Beds = 3,
        Baths = 2,
        Sqft = 1600,
        DistanceMiles = 0.8,
        Source = CompSource.Zillow
    };

    [Fact]
    public void BuildPrompt_IncludesAllCompData()
    {
        var lead = MakeLead();
        var comps = new List<Comp> { MakeComp(), MakeComp("789 Elm St", 440_000m) };

        var prompt = ClaudeAnalysisService.BuildPrompt(lead, comps, null, ReportType.Standard);

        prompt.Should().Contain("123 Main St");
        prompt.Should().Contain("456 Oak Ave");
        prompt.Should().Contain("789 Elm St");
        prompt.Should().Contain("425,000");
        prompt.Should().Contain("440,000");
    }

    [Fact]
    public void BuildPrompt_IncludesLeadResearch_WhenAvailable()
    {
        var lead = MakeLead();
        var comps = new List<Comp> { MakeComp() };
        var research = new LeadResearch
        {
            Occupation = "Software Engineer",
            Employer = "Acme Corp",
            PurchaseDate = new DateOnly(2019, 5, 1),
            PurchasePrice = 300_000m
        };

        var prompt = ClaudeAnalysisService.BuildPrompt(lead, comps, research, ReportType.Comprehensive);

        prompt.Should().Contain("Software Engineer");
        prompt.Should().Contain("Acme Corp");
    }

    [Fact]
    public void ParseResponse_ExtractsStructuredAnalysis()
    {
        var json = """
        {
            "valueLow": 420000,
            "valueMid": 445000,
            "valueHigh": 470000,
            "marketNarrative": "The Old Bridge market is strong.",
            "pricingRecommendation": "List at $449,900",
            "leadInsights": "Owned for 7 years, significant equity.",
            "conversationStarters": ["Ask about their equity growth", "Mention school district demand"],
            "marketTrend": "Seller's",
            "medianDaysOnMarket": 18
        }
        """;

        var result = ClaudeAnalysisService.ParseResponse(json);

        result.ValueLow.Should().Be(420_000m);
        result.ValueMid.Should().Be(445_000m);
        result.ValueHigh.Should().Be(470_000m);
        result.MarketNarrative.Should().Be("The Old Bridge market is strong.");
        result.PricingRecommendation.Should().Be("List at $449,900");
        result.LeadInsights.Should().Be("Owned for 7 years, significant equity.");
        result.ConversationStarters.Should().HaveCount(2);
        result.ConversationStarters[0].Should().Be("Ask about their equity growth");
        result.MarketTrend.Should().Be("Seller's");
        result.MedianDaysOnMarket.Should().Be(18);
    }
}
