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
}
