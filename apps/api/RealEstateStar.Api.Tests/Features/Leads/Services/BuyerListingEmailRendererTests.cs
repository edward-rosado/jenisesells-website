using FluentAssertions;
using RealEstateStar.Api.Common;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Services;

namespace RealEstateStar.Api.Tests.Features.Leads.Services;

public class BuyerListingEmailRendererTests
{
    private static AgentConfig MakeAgentConfig(
        string name           = "Jenise Buckalew",
        string phone          = "(732) 555-0100",
        string email          = "jenise@example.com",
        string brokerage      = "Keller Williams",
        string officeAddress  = "100 Broker Lane, Old Bridge, NJ 08857") =>
        new()
        {
            Id       = "jenise-buckalew",
            Identity = new AgentIdentity
            {
                Name      = name,
                Phone     = phone,
                Email     = email,
                Brokerage = brokerage
            },
            Location = new AgentLocation
            {
                State         = "NJ",
                OfficeAddress = officeAddress
            }
        };

    private static Listing MakeListing(
        string address     = "123 Oak St",
        string city        = "Old Bridge",
        string whyThisFits = "Close to top-rated schools",
        string? listingUrl = "https://zillow.com/listing/1") =>
        new(address, city, "NJ", "08857", 425_000m, 3, 2m, 1600, whyThisFits, listingUrl);

    private static LeadEnrichment MakeEnrichment(string motivation = "First-time buyer eager to settle down") =>
        new()
        {
            MotivationCategory   = "first_time_buyer",
            MotivationAnalysis   = motivation,
            ProfessionalBackground = "Software engineer at a local firm",
            FinancialIndicators  = "Pre-approved at $450,000",
            TimelinePressure     = "Looking to close within 60 days",
            ConversationStarters = ["Ask about commute time", "Mention school district rankings"],
            ColdCallOpeners      = ["Hi, I have some great options in your price range"]
        };

    // ─── Subject line ─────────────────────────────────────────────────────────────

    [Fact]
    public void Render_Subject_IncludesListingCount()
    {
        var listings = new[] { MakeListing(), MakeListing("456 Elm St") };
        var (subject, _) = BuyerListingEmailRenderer.Render("Jane", listings, MakeAgentConfig());

        subject.Should().Contain("2");
    }

    [Fact]
    public void Render_Subject_IncludesBuyerFirstName()
    {
        var listings = new[] { MakeListing() };
        var (subject, _) = BuyerListingEmailRenderer.Render("Sarah", listings, MakeAgentConfig());

        subject.Should().Contain("Sarah");
    }

    [Fact]
    public void Render_Subject_UsesSingular_WhenOneHome()
    {
        var listings = new[] { MakeListing() };
        var (subject, _) = BuyerListingEmailRenderer.Render("Jane", listings, MakeAgentConfig());

        subject.Should().Contain("1 Home");
        subject.Should().NotContain("Homes");
    }

    [Fact]
    public void Render_Subject_UsesPlural_WhenMultipleHomes()
    {
        var listings = new[] { MakeListing(), MakeListing("2 Spruce Ave") };
        var (subject, _) = BuyerListingEmailRenderer.Render("Jane", listings, MakeAgentConfig());

        subject.Should().Contain("Homes");
    }

    // ─── Body: personalized intro ─────────────────────────────────────────────────

    [Fact]
    public void Render_Body_IncludesPersonalizedIntro_FromEnrichment()
    {
        var enrichment = MakeEnrichment("First-time buyer eager to settle down");
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [MakeListing()], MakeAgentConfig(), enrichment);

        body.Should().Contain("First-time buyer eager to settle down");
    }

    [Fact]
    public void Render_Body_OmitsEnrichmentSentence_WhenEnrichmentIsNull()
    {
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [MakeListing()], MakeAgentConfig(), enrichment: null);

        body.Should().NotContain("Based on what you've shared");
    }

    [Fact]
    public void Render_Body_OmitsEnrichmentSentence_WhenMotivationIsUnknown()
    {
        var enrichment = MakeEnrichment("unknown");
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [MakeListing()], MakeAgentConfig(), enrichment);

        body.Should().NotContain("Based on what you've shared");
    }

    [Fact]
    public void Render_Body_IncludesBuyerFirstNameInGreeting()
    {
        var (_, body) = BuyerListingEmailRenderer.Render("Marcus", [MakeListing()], MakeAgentConfig());

        body.Should().Contain("Hi Marcus");
    }

    // ─── Body: listing cards ──────────────────────────────────────────────────────

    [Fact]
    public void Render_Body_IncludesListingAddress()
    {
        var listing = MakeListing("789 Pine Ct");
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [listing], MakeAgentConfig());

        body.Should().Contain("789 Pine Ct");
    }

    [Fact]
    public void Render_Body_IncludesListingPrice()
    {
        var listing = MakeListing();
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [listing], MakeAgentConfig());

        body.Should().Contain("425,000");
    }

    [Fact]
    public void Render_Body_IncludesBedsAndBaths()
    {
        var listing = MakeListing();
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [listing], MakeAgentConfig());

        body.Should().Contain("3 bed");
        body.Should().Contain("2 bath");
    }

    [Fact]
    public void Render_Body_IncludesWhyThisFitsNote()
    {
        var listing = MakeListing(whyThisFits: "Near top-rated schools and commuter rail");
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [listing], MakeAgentConfig());

        body.Should().Contain("Near top-rated schools and commuter rail");
    }

    [Fact]
    public void Render_Body_IncludesListingLink_WhenPresent()
    {
        var listing = MakeListing(listingUrl: "https://zillow.com/my-listing");
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [listing], MakeAgentConfig());

        body.Should().Contain("https://zillow.com/my-listing");
    }

    [Fact]
    public void Render_Body_OmitsListingLink_WhenAbsent()
    {
        var listing = MakeListing(listingUrl: null);
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [listing], MakeAgentConfig());

        body.Should().NotContain("View listing:");
    }

    [Fact]
    public void Render_Body_NumbersListingsSequentially()
    {
        var listings = new[]
        {
            MakeListing("1 First Ave"),
            MakeListing("2 Second Ave"),
            MakeListing("3 Third Ave")
        };
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", listings, MakeAgentConfig());

        body.Should().Contain("1.");
        body.Should().Contain("2.");
        body.Should().Contain("3.");
    }

    // ─── Body: agent sign-off ─────────────────────────────────────────────────────

    [Fact]
    public void Render_Body_IncludesAgentName()
    {
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [MakeListing()], MakeAgentConfig(name: "Alex Agent"));

        body.Should().Contain("Alex Agent");
    }

    [Fact]
    public void Render_Body_IncludesAgentPhone()
    {
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [MakeListing()], MakeAgentConfig(phone: "(732) 555-9876"));

        body.Should().Contain("(732) 555-9876");
    }

    [Fact]
    public void Render_Body_IncludesAgentEmail()
    {
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [MakeListing()], MakeAgentConfig(email: "alex@realty.com"));

        body.Should().Contain("alex@realty.com");
    }

    [Fact]
    public void Render_Body_IncludesBrokerage()
    {
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [MakeListing()], MakeAgentConfig(brokerage: "Century 21 Realty"));

        body.Should().Contain("Century 21 Realty");
    }

    // ─── Body: CAN-SPAM footer ─────────────────────────────────────────────────────

    [Fact]
    public void Render_Body_IncludesOptOutInstruction()
    {
        var (_, body) = BuyerListingEmailRenderer.Render("Jane", [MakeListing()], MakeAgentConfig());

        body.Should().ContainAny("UNSUBSCRIBE", "unsubscribe", "opt out");
    }

    [Fact]
    public void Render_Body_IncludesAgentOfficeAddress()
    {
        var (_, body) = BuyerListingEmailRenderer.Render(
            "Jane", [MakeListing()], MakeAgentConfig(officeAddress: "999 Commerce Blvd, Suite 100, NJ 07001"));

        body.Should().Contain("999 Commerce Blvd, Suite 100, NJ 07001");
    }

    [Fact]
    public void Render_Body_OmitsOfficeAddressLine_WhenNotSet()
    {
        var config = new AgentConfig
        {
            Id       = "test-agent",
            Identity = new AgentIdentity { Name = "Test Agent", Phone = "", Email = "" },
            Location = new AgentLocation { State = "NJ" } // no OfficeAddress
        };

        var act = () => BuyerListingEmailRenderer.Render("Jane", [MakeListing()], config);

        act.Should().NotThrow();
    }

    // ─── Edge cases ───────────────────────────────────────────────────────────────

    [Fact]
    public void Render_ReturnsEmptyListingSection_WhenNoListings()
    {
        var (subject, body) = BuyerListingEmailRenderer.Render("Jane", [], MakeAgentConfig());

        subject.Should().Contain("0");
        body.Should().Contain("Hi Jane");
        // Should still include footer
        body.Should().ContainAny("UNSUBSCRIBE", "unsubscribe", "opt out");
    }

    [Fact]
    public void Render_HandlesMissingIdentity_Gracefully()
    {
        var config = new AgentConfig { Id = "no-identity" }; // Identity is null

        var act = () => BuyerListingEmailRenderer.Render("Jane", [MakeListing()], config);

        act.Should().NotThrow();
    }
}
