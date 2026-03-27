using FluentAssertions;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Leads;

namespace RealEstateStar.Workers.Leads.Tests;

public class LeadEmailTemplateTests
{
    private static readonly AgentNotificationConfig DefaultAgent = new()
    {
        AgentId = "jenise-buckalew",
        Handle = "jenise",
        Name = "Jenise Buckalew",
        FirstName = "Jenise",
        Email = "jenise@example.com",
        Phone = "555-123-4567",
        LicenseNumber = "NJ-123456",
        BrokerageName = "Coldwell Banker",
        BrokerageLogo = "https://cdn.example.com/logo.png",
        PrimaryColor = "#1a73e8",
        AccentColor = "#f4b400",
        State = "NJ",
        Bio = "15 years helping NJ families.",
        Specialties = ["First-time buyers"],
        Testimonials = ["Jenise is amazing!"]
    };

    private static Lead MakeSellerLead() => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "jenise-buckalew",
        LeadType = LeadType.Seller,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-000-1111",
        Timeline = "1-3months",
        Status = LeadStatus.Received,
        ReceivedAt = DateTime.UtcNow,
        SellerDetails = new SellerDetails
        {
            Address = "123 Oak Ave",
            City = "Springfield",
            State = "NJ",
            Zip = "07081",
            Beds = 3,
            Baths = 2,
            Sqft = 1800
        }
    };

    private static Lead MakeBuyerLead() => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "jenise-buckalew",
        LeadType = LeadType.Buyer,
        FirstName = "John",
        LastName = "Smith",
        Email = "john@example.com",
        Phone = "555-000-2222",
        Timeline = "asap",
        Status = LeadStatus.Received,
        ReceivedAt = DateTime.UtcNow,
        BuyerDetails = new BuyerDetails
        {
            City = "Springfield",
            State = "NJ",
            MinBudget = 300_000m,
            MaxBudget = 450_000m
        }
    };

    private static LeadScore MakeScore() => new()
    {
        OverallScore = 75,
        Factors = [],
        Explanation = "Hot lead"
    };

    private static CmaWorkerResult MakeCmaResult(string leadId) => new(
        leadId, true, null,
        EstimatedValue: 450_000m,
        PriceRangeLow: 430_000m,
        PriceRangeHigh: 470_000m,
        Comps:
        [
            new CompSummary("100 Elm St", 440_000m, 3, 2, 1750, 12, 0.3),
            new CompSummary("200 Maple Ave", 460_000m, 4, 2, 1900, 8, 0.5)
        ],
        MarketAnalysis: "Seller's market with low inventory.");

    private static HomeSearchWorkerResult MakeHomeSearchResult(string leadId) => new(
        leadId, true, null,
        Listings:
        [
            new ListingSummary("10 Pine Rd", 350_000m, 3, 2, 1500, "Active", "https://example.com/1"),
            new ListingSummary("20 Cedar Dr", 410_000m, 4, 2, 1800, "Active", "https://example.com/2")
        ],
        AreaSummary: "Great school districts and low crime.");

    // -----------------------------------------------------------------------
    // Branding header
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_WithBrokerageLogo_IncludesImgTag()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("<img");
        html.Should().Contain("cdn.example.com/logo.png");
    }

    [Fact]
    public void Render_WithoutBrokerageLogo_IncludesAgentNameFallback()
    {
        var agentWithoutLogo = DefaultAgent with { BrokerageLogo = null };
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, agentWithoutLogo,
            string.Empty, string.Empty, null);

        html.Should().Contain("Jenise Buckalew");
        html.Should().NotContain("<img");
    }

    [Fact]
    public void Render_AppliesPrimaryColorToBrandingHeader()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("#1a73e8");
    }

    // -----------------------------------------------------------------------
    // Personalized paragraph and agent pitch
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_WithPersonalizedParagraph_IncludesItInBody()
    {
        var personalized = "Jane, your property on Oak Ave is well-positioned.";
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            personalized, string.Empty, null);

        html.Should().Contain("Jane, your property on Oak Ave is well-positioned.");
    }

    [Fact]
    public void Render_WithAgentPitch_IncludesItInBody()
    {
        var pitch = "With 15 years in NJ real estate, I bring unmatched expertise.";
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, pitch, null);

        html.Should().Contain("With 15 years in NJ real estate, I bring unmatched expertise.");
    }

    [Fact]
    public void Render_WithEmptyPersonalizedParagraph_IncludesFallbackGreeting()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("Thank you for reaching out");
    }

    // -----------------------------------------------------------------------
    // CMA summary section (sellers)
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_WithSuccessfulCmaResult_IncludesCmaSummarySection()
    {
        var lead = MakeSellerLead();
        var cmaResult = MakeCmaResult(lead.Id.ToString());

        var html = LeadEmailTemplate.Render(
            lead, MakeScore(), cmaResult, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("Comparative Market Analysis");
        html.Should().Contain("450");    // estimated value 450,000
        html.Should().Contain("430");    // range low 430,000
        html.Should().Contain("470");    // range high 470,000
    }

    [Fact]
    public void Render_WithCmaResultAndComps_ListsComparableSales()
    {
        var lead = MakeSellerLead();
        var cmaResult = MakeCmaResult(lead.Id.ToString());

        var html = LeadEmailTemplate.Render(
            lead, MakeScore(), cmaResult, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("100 Elm St");
        html.Should().Contain("200 Maple Ave");
    }

    [Fact]
    public void Render_WithCmaResultAndPdfDownloadUrl_IncludesDownloadLink()
    {
        var lead = MakeSellerLead();
        var cmaResult = MakeCmaResult(lead.Id.ToString());
        var pdfUrl = "https://blob.example.com/cma-report.pdf";

        var html = LeadEmailTemplate.Render(
            lead, MakeScore(), cmaResult, null, DefaultAgent,
            string.Empty, string.Empty, pdfUrl);

        html.Should().Contain(pdfUrl);
        html.Should().Contain("Download");
    }

    [Fact]
    public void Render_NoCmaResult_DoesNotIncludeCmaSection()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().NotContain("Comparative Market Analysis");
    }

    // -----------------------------------------------------------------------
    // Listing highlights (buyers)
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_WithSuccessfulHomeSearchResult_IncludesListingHighlights()
    {
        var lead = MakeBuyerLead();
        var homeResult = MakeHomeSearchResult(lead.Id.ToString());

        var html = LeadEmailTemplate.Render(
            lead, MakeScore(), null, homeResult, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("10 Pine Rd");
        html.Should().Contain("20 Cedar Dr");
        html.Should().Contain("Homes Matching Your Criteria");
    }

    [Fact]
    public void Render_WithHomeSearchAreaSummary_IncludesAreaSummary()
    {
        var lead = MakeBuyerLead();
        var homeResult = MakeHomeSearchResult(lead.Id.ToString());

        var html = LeadEmailTemplate.Render(
            lead, MakeScore(), null, homeResult, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("Great school districts");
    }

    [Fact]
    public void Render_ForBuyerWithNoHomeSearchResult_DoesNotIncludeListingSection()
    {
        var html = LeadEmailTemplate.Render(
            MakeBuyerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().NotContain("Homes Matching Your Criteria");
    }

    // -----------------------------------------------------------------------
    // Legal footer
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_IncludesLicenseNumberInFooter()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("NJ-123456");
        html.Should().Contain("License #");
    }

    [Fact]
    public void Render_IncludesBrokerageNameInFooter()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("Coldwell Banker");
    }

    [Fact]
    public void Render_IncludesOptOutLink()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("opt-out");
        html.Should().Contain("Unsubscribe");
    }

    [Fact]
    public void Render_IncludesCcpaLink()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("ccpa");
        html.Should().Contain("CCPA");
    }

    [Fact]
    public void Render_IncludesPrivacyPolicyLink()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("privacy");
        html.Should().Contain("Privacy Policy");
    }

    [Fact]
    public void Render_PrivacyLinksContainSignedToken()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        // Token is in query string and contains a dot (hash.signature)
        html.Should().Contain("token=");
        // The token query param should NOT contain the raw email
        html.Should().NotContain("jane@example.com");
    }

    // -----------------------------------------------------------------------
    // Powered by footnote
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_IncludesPoweredByRealEstarFootnote()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("Powered by Real Estate Star");
    }

    // -----------------------------------------------------------------------
    // Mobile-responsive structure
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_IncludesMetaViewport()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("viewport");
        html.Should().Contain("width=device-width");
    }

    [Fact]
    public void Render_IncludesMaxWidthStyle()
    {
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("max-width");
        html.Should().Contain("600");
    }

    // -----------------------------------------------------------------------
    // Privacy token signing
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildPrivacyToken_ContainsDotSeparatingHashAndSignature()
    {
        var token = LeadEmailTemplate.BuildPrivacyToken("test@example.com", "agent-id");
        token.Should().Contain(".");
        var parts = token.Split('.');
        parts.Should().HaveCount(2);
        parts[0].Should().NotBeEmpty(); // email hash
        parts[1].Should().NotBeEmpty(); // HMAC signature
    }

    [Fact]
    public void BuildPrivacyToken_DoesNotContainRawEmail()
    {
        var token = LeadEmailTemplate.BuildPrivacyToken("jane@example.com", "jenise-buckalew");
        token.Should().NotContain("jane@example.com");
    }

    [Fact]
    public void BuildPrivacyToken_SameInputProducesSameTokenWithinSameDay()
    {
        var token1 = LeadEmailTemplate.BuildPrivacyToken("same@example.com", "agent-x");
        var token2 = LeadEmailTemplate.BuildPrivacyToken("same@example.com", "agent-x");
        token1.Should().Be(token2);
    }

    [Fact]
    public void BuildPrivacyToken_DifferentEmailsProduceDifferentTokens()
    {
        var token1 = LeadEmailTemplate.BuildPrivacyToken("alice@example.com", "agent-x");
        var token2 = LeadEmailTemplate.BuildPrivacyToken("bob@example.com", "agent-x");
        token1.Should().NotBe(token2);
    }

    // -----------------------------------------------------------------------
    // Missing optional fields (graceful degradation)
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_WithoutBio_DoesNotThrow()
    {
        var agentWithoutBio = DefaultAgent with { Bio = null };
        var act = () => LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, agentWithoutBio,
            string.Empty, string.Empty, null);

        act.Should().NotThrow();
    }

    [Fact]
    public void Render_WithoutBrokerageLogo_RendersAgentNameFallback()
    {
        var agentWithoutLogo = DefaultAgent with { BrokerageLogo = null };
        var html = LeadEmailTemplate.Render(
            MakeSellerLead(), MakeScore(), null, null, agentWithoutLogo,
            string.Empty, string.Empty, null);

        html.Should().Contain("Jenise Buckalew");
    }

    [Fact]
    public void Render_WithSuccessfulCmaButNoComps_DoesNotThrow()
    {
        var lead = MakeSellerLead();
        var cmaResult = new CmaWorkerResult(
            lead.Id.ToString(), true, null,
            450_000m, 430_000m, 470_000m,
            Comps: null,
            MarketAnalysis: null);

        var act = () => LeadEmailTemplate.Render(
            lead, MakeScore(), cmaResult, null, DefaultAgent,
            string.Empty, string.Empty, null);

        act.Should().NotThrow();
    }

    [Fact]
    public void Render_WithFailedCmaResult_OmitsCmaSection()
    {
        var lead = MakeSellerLead();
        var cmaResult = new CmaWorkerResult(
            lead.Id.ToString(), false, "CMA timed out",
            null, null, null, null, null);

        var html = LeadEmailTemplate.Render(
            lead, MakeScore(), cmaResult, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().NotContain("Comparative Market Analysis");
    }

    [Fact]
    public void Render_WithFailedHomeSearchResult_OmitsListingSection()
    {
        var lead = MakeBuyerLead();
        var homeResult = new HomeSearchWorkerResult(
            lead.Id.ToString(), false, "Search timed out", null, null);

        var html = LeadEmailTemplate.Render(
            lead, MakeScore(), null, homeResult, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().NotContain("Homes Matching Your Criteria");
    }

    // -----------------------------------------------------------------------
    // HtmlEncode helper
    // -----------------------------------------------------------------------

    [Fact]
    public void HtmlEncode_EncodesSpecialCharacters()
    {
        var encoded = LeadEmailTemplate.HtmlEncode("<script>alert('xss')</script>");
        encoded.Should().NotContain("<script>");
        encoded.Should().Contain("&lt;");
        encoded.Should().Contain("&gt;");
    }

    [Fact]
    public void HtmlEncode_NullOrEmptyReturnsEmpty()
    {
        LeadEmailTemplate.HtmlEncode(null).Should().BeEmpty();
        LeadEmailTemplate.HtmlEncode(string.Empty).Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // CMA section: null estimated value and price range (—fallback)
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_CmaResultWithNullValues_ShowsDashFallbacks()
    {
        var lead = MakeSellerLead();
        var cmaResult = new CmaWorkerResult(
            lead.Id.ToString(), true, null,
            EstimatedValue: null,
            PriceRangeLow: null,
            PriceRangeHigh: null,
            Comps: null,
            MarketAnalysis: null);

        var html = LeadEmailTemplate.Render(
            lead, MakeScore(), cmaResult, null, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().Contain("—");
    }

    // -----------------------------------------------------------------------
    // Listing section: listings with null beds/baths/sqft and null URL
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_ListingWithNoBedsOrBathsOrSqftOrUrl_DoesNotThrow()
    {
        var lead = MakeBuyerLead();
        var homeResult = new HomeSearchWorkerResult(
            lead.Id.ToString(), true, null,
            Listings:
            [
                new ListingSummary("5 Hollow Rd", 320_000m,
                    Beds: null, Baths: null, Sqft: null,
                    Status: "Active", Url: null)
            ],
            AreaSummary: null);

        var act = () => LeadEmailTemplate.Render(
            lead, MakeScore(), null, homeResult, DefaultAgent,
            string.Empty, string.Empty, null);

        act.Should().NotThrow();
    }

    [Fact]
    public void Render_ListingWithNullUrl_OmitsViewListingLink()
    {
        var lead = MakeBuyerLead();
        var homeResult = new HomeSearchWorkerResult(
            lead.Id.ToString(), true, null,
            Listings:
            [
                new ListingSummary("5 Hollow Rd", 320_000m, 3, 2, 1500, "Active", Url: null)
            ],
            AreaSummary: null);

        var html = LeadEmailTemplate.Render(
            lead, MakeScore(), null, homeResult, DefaultAgent,
            string.Empty, string.Empty, null);

        html.Should().NotContain("View Listing");
    }

    // -----------------------------------------------------------------------
    // Buyer with no budget in user message (coverage for min/max branch)
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_BuyerLeadWithNoBudget_DoesNotThrow()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            AgentId = "jenise-buckalew",
            LeadType = LeadType.Buyer,
            FirstName = "Bob",
            LastName = "Jones",
            Email = "bob@example.com",
            Phone = "555-000-5555",
            Timeline = "asap",
            Status = LeadStatus.Received,
            ReceivedAt = DateTime.UtcNow,
            BuyerDetails = new BuyerDetails
            {
                City = "Springfield",
                State = "NJ",
                MinBudget = null,
                MaxBudget = null
            }
        };

        var act = () => LeadEmailTemplate.Render(
            lead, MakeScore(), null, null, DefaultAgent,
            string.Empty, string.Empty, null);

        act.Should().NotThrow();
    }
}
