using FluentAssertions;
using RealEstateStar.Api.Services.Gws;

namespace RealEstateStar.Api.Tests.Services.Gws;

public class GwsServiceTests
{
    [Fact]
    public void BuildLeadFolderPath_FormatsCorrectly()
    {
        var result = GwsService.BuildLeadFolderPath("Jane Doe", "123 Main St, Edison NJ");

        result.Should().Be("Real Estate Star/1 - Leads/Jane Doe/123 Main St, Edison NJ");
    }

    [Fact]
    public void BuildLeadBriefContent_IncludesAllSections()
    {
        var content = GwsService.BuildLeadBriefContent(
            leadName: "Jane Doe",
            address: "123 Main St, Edison NJ 08817",
            timeline: "ASAP",
            submittedAt: new DateTime(2026, 3, 9, 14, 30, 0),
            occupation: "Software Engineer",
            employer: "Acme Corp",
            purchaseDate: new DateOnly(2018, 6, 15),
            purchasePrice: 350_000m,
            ownershipDuration: "~8 years",
            equityRange: "$100k–$150k",
            lifeEvent: "Relocating for new job",
            beds: 3,
            baths: 2,
            sqft: 1800,
            yearBuilt: 1995,
            lotSize: "0.25 acres",
            taxAssessment: 380_000m,
            annualTax: 8_500m,
            compCount: 12,
            searchRadius: "0.5 miles",
            valueRange: "$375,000–$425,000",
            medianDom: 21,
            marketTrend: "seller's",
            conversationStarters: ["They may be feeling the pressure of a tight relocation timeline", "Their equity position gives them strong negotiating power"],
            leadEmail: "jane.doe@example.com",
            leadPhone: "(555) 123-4567",
            pdfLink: "https://storage.example.com/cma/jane-doe.pdf");

        content.Should().Contain("Jane Doe");
        content.Should().Contain("Software Engineer");
        content.Should().Contain("Acme Corp");
        content.Should().Contain("ASAP");
        content.Should().Contain("Conversation Starters");
        content.Should().Contain("tight relocation timeline");
        content.Should().Contain("strong negotiating power");
        content.Should().Contain("123 Main St, Edison NJ 08817");
        content.Should().Contain("Call within 1 hour");
        content.Should().Contain("jane.doe@example.com");
        content.Should().Contain("(555) 123-4567");
        content.Should().Contain("jane-doe.pdf");
        content.Should().Contain("$350,000");
        content.Should().Contain("3 bed / 2 bath / 1,800 sqft");
        content.Should().Contain("12 comparable sales");
        content.Should().Contain("Market trending: seller's market");
    }

    [Fact]
    public void BuildLeadBriefContent_Uses1To3MonthsTimeline()
    {
        var content = GwsService.BuildLeadBriefContent(
            leadName: "Bob Smith",
            address: "456 Oak Ave, Edison NJ 08817",
            timeline: "1-3 months",
            submittedAt: new DateTime(2026, 3, 9, 14, 30, 0),
            occupation: null,
            employer: null,
            purchaseDate: null,
            purchasePrice: null,
            ownershipDuration: null,
            equityRange: null,
            lifeEvent: null,
            beds: null,
            baths: null,
            sqft: null,
            yearBuilt: null,
            lotSize: null,
            taxAssessment: null,
            annualTax: null,
            compCount: 5,
            searchRadius: "1 mile",
            valueRange: "$300,000-$350,000",
            medianDom: 30,
            marketTrend: "buyer's",
            conversationStarters: [],
            leadEmail: "bob@example.com",
            leadPhone: "(555) 999-0000",
            pdfLink: "https://storage.example.com/cma/bob.pdf");

        content.Should().Contain("Call within 2 hours");
        content.Should().Contain("serious seller");
    }

    [Fact]
    public void BuildLeadBriefContent_UsesDefaultTimeline()
    {
        var content = GwsService.BuildLeadBriefContent(
            leadName: "Alice Jones",
            address: "789 Elm St, Edison NJ 08817",
            timeline: "Just curious",
            submittedAt: new DateTime(2026, 3, 9, 14, 30, 0),
            occupation: null,
            employer: null,
            purchaseDate: null,
            purchasePrice: null,
            ownershipDuration: null,
            equityRange: null,
            lifeEvent: null,
            beds: null,
            baths: null,
            sqft: null,
            yearBuilt: null,
            lotSize: null,
            taxAssessment: null,
            annualTax: null,
            compCount: 3,
            searchRadius: "2 miles",
            valueRange: "$250,000-$280,000",
            medianDom: 45,
            marketTrend: "neutral",
            conversationStarters: [],
            leadEmail: "alice@example.com",
            leadPhone: "(555) 111-2222",
            pdfLink: "https://storage.example.com/cma/alice.pdf");

        content.Should().Contain("Call within 24 hours");
        content.Should().Contain("build the relationship");
    }

    [Fact]
    public void BuildLeadBriefContent_OmitsNullOptionalSections()
    {
        var content = GwsService.BuildLeadBriefContent(
            leadName: "Test User",
            address: "100 Test St",
            timeline: "ASAP",
            submittedAt: new DateTime(2026, 1, 1),
            occupation: null,
            employer: null,
            purchaseDate: null,
            purchasePrice: null,
            ownershipDuration: null,
            equityRange: null,
            lifeEvent: null,
            beds: null,
            baths: null,
            sqft: null,
            yearBuilt: null,
            lotSize: null,
            taxAssessment: null,
            annualTax: null,
            compCount: 0,
            searchRadius: "1 mile",
            valueRange: "$0",
            medianDom: 0,
            marketTrend: "neutral",
            conversationStarters: [],
            leadEmail: "test@test.com",
            leadPhone: "555-0000",
            pdfLink: "link");

        // Should not contain optional fields that are null
        content.Should().NotContain("Purchased");
        content.Should().NotContain("Owned for");
        content.Should().NotContain("Estimated equity");
        content.Should().NotContain("Lot:");
        content.Should().NotContain("Current tax assessment");
        content.Should().NotContain("Annual property taxes");
    }
}
