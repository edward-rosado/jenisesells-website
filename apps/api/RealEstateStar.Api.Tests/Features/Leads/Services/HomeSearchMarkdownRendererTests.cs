using FluentAssertions;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Services;

namespace RealEstateStar.Api.Tests.Features.Leads.Services;

public class HomeSearchMarkdownRendererTests
{
    private static Lead MakeLead() => new()
    {
        Id = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
        AgentId = "test-agent",
        LeadType = LeadType.Buyer,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-1234",
        Timeline = "3-6m",
        BuyerDetails = new BuyerDetails { City = "Springfield", State = "NJ" }
    };

    private static Listing MakeListing(int index, string? whyThisFits = null) =>
        new(
            Address: $"{100 + index} Oak Ave",
            City: "Springfield",
            State: "NJ",
            Zip: "07081",
            Price: 400000 + index * 10000,
            Beds: 3,
            Baths: 2,
            Sqft: 1600,
            WhyThisFits: whyThisFits,
            ListingUrl: null);

    [Fact]
    public void RenderListings_IncludesYamlFrontmatter()
    {
        var lead = MakeLead();
        var listings = new List<Listing> { MakeListing(0) };

        var result = HomeSearchMarkdownRenderer.RenderListings(lead, listings, "Test Agent");

        result.Should().Contain($"leadId: {lead.Id}");
        result.Should().Contain("generatedAt:");
        result.Should().Contain($"listingCount: {listings.Count}");
        result.Should().Contain("agent: Test Agent");
        result.Should().StartWith("---");
    }

    [Fact]
    public void RenderListings_IncludesAllListings()
    {
        var lead = MakeLead();
        var listings = new List<Listing>
        {
            MakeListing(0),
            MakeListing(1),
            MakeListing(2)
        };

        var result = HomeSearchMarkdownRenderer.RenderListings(lead, listings, "Test Agent");

        result.Should().Contain("100 Oak Ave");
        result.Should().Contain("101 Oak Ave");
        result.Should().Contain("102 Oak Ave");
    }

    [Fact]
    public void RenderListings_IncludesWhyThisFits()
    {
        var lead = MakeLead();
        var listings = new List<Listing>
        {
            MakeListing(0, whyThisFits: "Great school district and quiet street")
        };

        var result = HomeSearchMarkdownRenderer.RenderListings(lead, listings, "Test Agent");

        result.Should().Contain("Why this fits:");
        result.Should().Contain("Great school district and quiet street");
    }

    [Fact]
    public void RenderListings_OmitsWhyThisFits_WhenNull()
    {
        var lead = MakeLead();
        var listings = new List<Listing> { MakeListing(0, whyThisFits: null) };

        var result = HomeSearchMarkdownRenderer.RenderListings(lead, listings, "Test Agent");

        result.Should().NotContain("Why this fits:");
    }

    [Fact]
    public void RenderEmailBody_IncludesGreeting()
    {
        var lead = MakeLead();
        var listings = new List<Listing> { MakeListing(0) };

        var result = HomeSearchMarkdownRenderer.RenderEmailBody(lead, listings, "Test Agent");

        result.Should().Contain($"Hi {lead.FirstName},");
    }

    [Fact]
    public void RenderEmailBody_IncludesAllListingAddresses()
    {
        var lead = MakeLead();
        var listings = new List<Listing>
        {
            MakeListing(0),
            MakeListing(1),
            MakeListing(2)
        };

        var result = HomeSearchMarkdownRenderer.RenderEmailBody(lead, listings, "Test Agent");

        result.Should().Contain("100 Oak Ave");
        result.Should().Contain("101 Oak Ave");
        result.Should().Contain("102 Oak Ave");
    }

    [Fact]
    public void RenderEmailBody_IncludesAgentSignoff()
    {
        var lead = MakeLead();
        var listings = new List<Listing> { MakeListing(0) };

        var result = HomeSearchMarkdownRenderer.RenderEmailBody(lead, listings, "Test Agent");

        result.Should().Contain("Test Agent");
        result.Should().Contain("Best,");
    }

    // ---------------------------------------------------------------------------
    // RenderListings — budget / filter branches
    // ---------------------------------------------------------------------------

    private static Lead MakeLeadWithBuyerDetails(
        decimal? minBudget = null,
        decimal? maxBudget = null,
        int? bedrooms = null,
        int? bathrooms = null) => new()
    {
        Id = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
        AgentId = "test-agent",
        LeadType = LeadType.Buyer,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-1234",
        Timeline = "3-6m",
        BuyerDetails = new BuyerDetails
        {
            City = "Springfield",
            State = "NJ",
            MinBudget = minBudget,
            MaxBudget = maxBudget,
            Bedrooms = bedrooms,
            Bathrooms = bathrooms
        }
    };

    [Fact]
    public void RenderListings_WithBothBudgets_ShowsPriceRange()
    {
        var lead = MakeLeadWithBuyerDetails(minBudget: 200_000m, maxBudget: 500_000m);
        var listings = new List<Listing> { MakeListing(0) };

        var result = HomeSearchMarkdownRenderer.RenderListings(lead, listings, "Test Agent");

        result.Should().Contain("**Price range:**");
        result.Should().Contain("$200,000");
        result.Should().Contain("$500,000");
    }

    [Fact]
    public void RenderListings_WithOnlyMinBudget_ShowsAnyForMax()
    {
        var lead = MakeLeadWithBuyerDetails(minBudget: 200_000m, maxBudget: null);
        var listings = new List<Listing> { MakeListing(0) };

        var result = HomeSearchMarkdownRenderer.RenderListings(lead, listings, "Test Agent");

        result.Should().Contain("**Price range:**");
        result.Should().Contain("$200,000");
        result.Should().Contain("any");
    }

    [Fact]
    public void RenderListings_WithBedrooms_ShowsBedroomLine()
    {
        var lead = MakeLeadWithBuyerDetails(bedrooms: 3);
        var listings = new List<Listing> { MakeListing(0) };

        var result = HomeSearchMarkdownRenderer.RenderListings(lead, listings, "Test Agent");

        result.Should().Contain("**Bedrooms:** 3+");
    }

    [Fact]
    public void RenderListings_WithBathrooms_ShowsBathroomLine()
    {
        var lead = MakeLeadWithBuyerDetails(bathrooms: 2);
        var listings = new List<Listing> { MakeListing(0) };

        var result = HomeSearchMarkdownRenderer.RenderListings(lead, listings, "Test Agent");

        result.Should().Contain("**Bathrooms:** 2+");
    }

    [Fact]
    public void RenderListings_WithSqft_ShowsSqftLine()
    {
        var listing = new Listing("100 Oak Ave", "Springfield", "NJ", "07081", 400_000m, 3, 2, Sqft: 1600, null, null);
        var lead = MakeLead();

        var result = HomeSearchMarkdownRenderer.RenderListings(lead, [listing], "Test Agent");

        result.Should().Contain("**Sqft:**");
    }

    [Fact]
    public void RenderListings_WithListingUrl_ShowsListingLine()
    {
        var listing = new Listing("100 Oak Ave", "Springfield", "NJ", "07081", 400_000m, 3, 2, null, null, "https://example.com/listing/1");
        var lead = MakeLead();

        var result = HomeSearchMarkdownRenderer.RenderListings(lead, [listing], "Test Agent");

        result.Should().Contain("**Listing:**");
        result.Should().Contain("https://example.com/listing/1");
    }

    // ---------------------------------------------------------------------------
    // RenderEmailBody — optional field branches
    // ---------------------------------------------------------------------------

    [Fact]
    public void RenderEmailBody_WithWhyThisFits_ShowsQuotedText()
    {
        var listing = new Listing("100 Oak Ave", "Springfield", "NJ", "07081", 400_000m, 3, 2, 1600, "Great school district", null);
        var lead = MakeLead();

        var result = HomeSearchMarkdownRenderer.RenderEmailBody(lead, [listing], "Test Agent");

        result.Should().Contain("→");
        result.Should().Contain("Great school district");
    }

    [Fact]
    public void RenderEmailBody_WithNoWhyThisFits_NoArrowLine()
    {
        var listing = new Listing("100 Oak Ave", "Springfield", "NJ", "07081", 400_000m, 3, 2, 1600, null, null);
        var lead = MakeLead();

        var result = HomeSearchMarkdownRenderer.RenderEmailBody(lead, [listing], "Test Agent");

        result.Should().NotContain("→");
    }

    [Fact]
    public void RenderEmailBody_WithSqft_IncludesSqftInPriceLine()
    {
        var listing = new Listing("100 Oak Ave", "Springfield", "NJ", "07081", 400_000m, 3, 2, 1600, null, null);
        var lead = MakeLead();

        var result = HomeSearchMarkdownRenderer.RenderEmailBody(lead, [listing], "Test Agent");

        result.Should().Contain("sqft");
    }

    [Fact]
    public void RenderEmailBody_WithNoSqft_DoesNotIncludeSqft()
    {
        var listing = new Listing("100 Oak Ave", "Springfield", "NJ", "07081", 400_000m, 3, 2, null, null, null);
        var lead = MakeLead();

        var result = HomeSearchMarkdownRenderer.RenderEmailBody(lead, [listing], "Test Agent");

        result.Should().NotContain("sqft");
    }

    [Fact]
    public void RenderEmailBody_WithListingUrl_ShowsViewLine()
    {
        var listing = new Listing("100 Oak Ave", "Springfield", "NJ", "07081", 400_000m, 3, 2, null, null, "https://example.com/listing/1");
        var lead = MakeLead();

        var result = HomeSearchMarkdownRenderer.RenderEmailBody(lead, [listing], "Test Agent");

        result.Should().Contain("View:");
        result.Should().Contain("https://example.com/listing/1");
    }
}
