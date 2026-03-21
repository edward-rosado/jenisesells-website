
namespace RealEstateStar.Domain.Tests.Leads;

public class LeadMarkdownRendererTests
{
    // ─── Shared test data ───────────────────────────────────────────────────────

    private static Lead MakeFullLead() => new()
    {
        Id = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
        AgentId = "jenise-buckalew",
        LeadType = LeadType.Both,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "5551234567",
        Timeline = "1-3months",
        ReceivedAt = new DateTime(2026, 3, 19, 14, 30, 0, DateTimeKind.Utc),
        Status = LeadStatus.Enriched,
        Notes = "Referred by a neighbor.",
        SellerDetails = new SellerDetails
        {
            Address = "123 Main St",
            City = "Springfield",
            State = "NJ",
            Zip = "07081",
            PropertyType = "Single Family",
            Condition = "Good",
            AskingPrice = 300000m
        },
        BuyerDetails = new BuyerDetails
        {
            City = "Kill Devil Hills",
            State = "NC",
            MaxBudget = 500000m,
            Bedrooms = 3,
            Bathrooms = 2,
            PropertyTypes = ["Single Family", "Townhome"],
            MustHaves = ["Pool", "Garage"]
        },
        Enrichment = new LeadEnrichment
        {
            MotivationCategory = "relocating",
            MotivationAnalysis = "Relocating for a new job opportunity.",
            ProfessionalBackground = "Software engineer, stable income.",
            FinancialIndicators = "Pre-approved for $500k.",
            TimelinePressure = "Needs to move within 90 days.",
            ConversationStarters = ["Ask about the new role.", "Discuss commute preferences."],
            ColdCallOpeners = ["Congratulations on the new opportunity!", "I specialize in relocation buyers."]
        },
        Score = new LeadScore
        {
            OverallScore = 82,
            Factors =
            [
                new ScoreFactor { Category = "Timeline", Score = 90, Weight = 0.3m, Explanation = "Very tight timeline." },
                new ScoreFactor { Category = "Budget",   Score = 80, Weight = 0.4m, Explanation = "Pre-approved." }
            ],
            Explanation = "High-quality lead with urgent timeline."
        }
    };

    private static Lead MakeBuyerOnlyLead() => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "jenise-buckalew",
        LeadType = LeadType.Buyer,
        FirstName = "Bob",
        LastName = "Smith",
        Email = "bob@example.com",
        Phone = "555-987-6543",
        Timeline = "3-6months",
        ReceivedAt = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
        Status = LeadStatus.Received,
        BuyerDetails = new BuyerDetails
        {
            City = "Raleigh",
            State = "NC",
            MaxBudget = 400000m,
            Bedrooms = 2,
            Bathrooms = 2,
            PropertyTypes = ["Condo"],
            MustHaves = ["Gym"]
        }
    };

    private static Lead MakeSellerOnlyLead() => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "jenise-buckalew",
        LeadType = LeadType.Seller,
        FirstName = "Alice",
        LastName = "Jones",
        Email = "alice@example.com",
        Phone = "(444) 222-3333",
        Timeline = "asap",
        ReceivedAt = new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc),
        Status = LeadStatus.Received,
        SellerDetails = new SellerDetails
        {
            Address = "99 Oak Ave",
            City = "Newark",
            State = "NJ",
            Zip = "07102",
            AskingPrice = 250000m
        }
    };

    // ─── RenderLeadProfile ───────────────────────────────────────────────────────

    [Fact]
    public void RenderLeadProfile_YamlContainsLeadId()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains($"leadId: {lead.Id}", result);
    }

    [Fact]
    public void RenderLeadProfile_YamlContainsStatus()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("status: Enriched", result);
    }

    [Fact]
    public void RenderLeadProfile_YamlContainsLeadTypes()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("leadType: Both", result);
    }

    [Fact]
    public void RenderLeadProfile_BodyContainsFullNameHeading()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("# Jane Doe", result);
    }

    [Fact]
    public void RenderLeadProfile_BodyContainsContactSection()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("## Contact", result);
    }

    [Fact]
    public void RenderLeadProfile_FormatsPhoneFromDigitsOnly()
    {
        var lead = MakeFullLead(); // phone = "5551234567"
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("(555) 123-4567", result);
    }

    [Fact]
    public void RenderLeadProfile_FormatsPhoneFromDashFormat()
    {
        var lead = MakeBuyerOnlyLead(); // phone = "555-987-6543"
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("(555) 987-6543", result);
    }

    [Fact]
    public void RenderLeadProfile_PassesThroughAlreadyFormattedPhone()
    {
        var lead = MakeSellerOnlyLead(); // phone = "(444) 222-3333"
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("(444) 222-3333", result);
    }

    [Fact]
    public void RenderLeadProfile_FormatsTimeline_1To3Months()
    {
        var lead = MakeFullLead(); // timeline = "1-3months"
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("1–3 months", result);
    }

    [Fact]
    public void RenderLeadProfile_FormatsTimeline_3To6Months()
    {
        var lead = MakeBuyerOnlyLead(); // timeline = "3-6months"
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("3–6 months", result);
    }

    [Fact]
    public void RenderLeadProfile_FormatsTimeline_Asap()
    {
        var lead = MakeSellerOnlyLead(); // timeline = "asap"
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("ASAP", result);
    }

    [Fact]
    public void RenderLeadProfile_FormatsTimeline_JustLooking()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(), AgentId = "test", LeadType = LeadType.Buyer,
            FirstName = "T", LastName = "T", Email = "t@t.com", Phone = "5550000000",
            Timeline = "justlooking",
            BuyerDetails = new BuyerDetails { City = "NYC", State = "NY" }
        };
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("Just looking", result);
    }

    [Fact]
    public void RenderLeadProfile_FormatsTimeline_6To12Months()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(), AgentId = "test", LeadType = LeadType.Buyer,
            FirstName = "T", LastName = "T", Email = "t@t.com", Phone = "5550000000",
            Timeline = "6-12months",
            BuyerDetails = new BuyerDetails { City = "NYC", State = "NY" }
        };
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("6–12 months", result);
    }

    [Fact]
    public void RenderLeadProfile_SellingSection_PresentWhenSellerDetailsExist()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("## Selling", result);
        Assert.Contains("123 Main St", result);
        Assert.Contains("$300,000", result);
    }

    [Fact]
    public void RenderLeadProfile_BuyerOnly_OmitsSellingSection()
    {
        var lead = MakeBuyerOnlyLead();
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.DoesNotContain("## Selling", result);
    }

    [Fact]
    public void RenderLeadProfile_BuyingSection_PresentWhenBuyerDetailsExist()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("## Buying", result);
        Assert.Contains("Kill Devil Hills, NC", result);
        Assert.Contains("$500,000", result);
    }

    [Fact]
    public void RenderLeadProfile_SellerOnly_OmitsBuyingSection()
    {
        var lead = MakeSellerOnlyLead();
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.DoesNotContain("## Buying", result);
    }

    [Fact]
    public void RenderLeadProfile_NotesRenderedWhenPresent()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.Contains("## Notes", result);
        Assert.Contains("Referred by a neighbor.", result);
    }

    [Fact]
    public void RenderLeadProfile_NotesOmittedWhenNull()
    {
        var lead = MakeBuyerOnlyLead(); // Notes is null
        var result = LeadMarkdownRenderer.RenderLeadProfile(lead);
        Assert.DoesNotContain("## Notes", result);
    }

    // ─── RenderResearchInsights ──────────────────────────────────────────────────

    [Fact]
    public void RenderResearchInsights_ContainsScoreDisplay()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderResearchInsights(lead);
        Assert.Contains("## Lead Score: 82 / 100", result);
    }

    [Fact]
    public void RenderResearchInsights_ContainsScoreBreakdownTable()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderResearchInsights(lead);
        Assert.Contains("| Timeline |", result);
        Assert.Contains("| Budget |", result);
        Assert.Contains("Very tight timeline.", result);
    }

    [Fact]
    public void RenderResearchInsights_ContainsMotivationSection()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderResearchInsights(lead);
        Assert.Contains("## Motivation Analysis", result);
        Assert.Contains("Relocating for a new job opportunity.", result);
    }

    [Fact]
    public void RenderResearchInsights_ContainsProfessionalBackground()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderResearchInsights(lead);
        Assert.Contains("## Professional Background", result);
        Assert.Contains("Software engineer, stable income.", result);
    }

    [Fact]
    public void RenderResearchInsights_ContainsConversationStarters()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderResearchInsights(lead);
        Assert.Contains("## Conversation Starters", result);
        Assert.Contains("- Ask about the new role.", result);
        Assert.Contains("- Discuss commute preferences.", result);
    }

    [Fact]
    public void RenderResearchInsights_ContainsColdCallOpeners()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderResearchInsights(lead);
        Assert.Contains("## Cold Call Openers", result);
        Assert.Contains("- Congratulations on the new opportunity!", result);
    }

    [Fact]
    public void RenderResearchInsights_YamlContainsOverallScore()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderResearchInsights(lead);
        Assert.Contains("overallScore: 82", result);
    }

    [Fact]
    public void RenderResearchInsights_YamlContainsMotivationCategory()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderResearchInsights(lead);
        Assert.Contains("motivationCategory: relocating", result);
    }

    [Fact]
    public void RenderResearchInsights_EmptyEnrichment_ProducesMinimalDoc()
    {
        var lead = MakeFullLead();
        lead.Enrichment = null;
        lead.Score = null;
        var result = LeadMarkdownRenderer.RenderResearchInsights(lead);
        Assert.Contains("Enrichment pending", result);
        Assert.DoesNotContain("## Motivation Analysis", result);
    }

    [Fact]
    public void RenderResearchInsights_EmptyEnrichment_DefaultScore50InYaml()
    {
        var lead = MakeFullLead();
        lead.Enrichment = null;
        lead.Score = null;
        var result = LeadMarkdownRenderer.RenderResearchInsights(lead);
        Assert.Contains("overallScore: 50", result);
    }

    // ─── RenderHomeSearchResults ─────────────────────────────────────────────────

    private static List<Listing> MakeListings() =>
    [
        new Listing("10 Beach Rd", "Kill Devil Hills", "NC", "27948", 480000m, 3, 2, 1800, "Great ocean views.", "https://example.com/1"),
        new Listing("22 Coastal Ave", "Kill Devil Hills", "NC", "27948", 450000m, 4, 2.5m, null, null, null)
    ];

    [Fact]
    public void RenderHomeSearchResults_ContainsSearchCriteriaHeader()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderHomeSearchResults(lead, MakeListings());
        Assert.Contains("Kill Devil Hills, NC", result);
        Assert.Contains("$500,000", result);
        Assert.Contains("3+ bed", result);
        Assert.Contains("2+ bath", result);
    }

    [Fact]
    public void RenderHomeSearchResults_ContainsListingCards()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderHomeSearchResults(lead, MakeListings());
        Assert.Contains("10 Beach Rd", result);
        Assert.Contains("22 Coastal Ave", result);
        Assert.Contains("Great ocean views.", result);
    }

    [Fact]
    public void RenderHomeSearchResults_ContainsListingNumberHeadings()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderHomeSearchResults(lead, MakeListings());
        Assert.Contains("## 1.", result);
        Assert.Contains("## 2.", result);
    }

    [Fact]
    public void RenderHomeSearchResults_ContainsListingUrl()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderHomeSearchResults(lead, MakeListings());
        Assert.Contains("[View Listing](https://example.com/1)", result);
    }

    [Fact]
    public void RenderHomeSearchResults_OmitsListingUrlWhenNull()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderHomeSearchResults(lead, MakeListings());
        // The second listing has no URL — just verify no broken markdown link
        var lines = result.Split('\n');
        var viewListingLines = lines.Where(l => l.Contains("[View Listing]")).ToList();
        Assert.Single(viewListingLines); // only one listing has a URL
    }

    [Fact]
    public void RenderHomeSearchResults_ContainsSqftWhenPresent()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderHomeSearchResults(lead, MakeListings());
        Assert.Contains("1,800 sqft", result);
    }

    [Fact]
    public void RenderHomeSearchResults_OmitsSqftWhenNull()
    {
        var lead = MakeFullLead();
        var listings = new List<Listing>
        {
            new("5 Test Ln", "Kill Devil Hills", "NC", "27948", 400000m, 3, 2, null, null, null)
        };
        var result = LeadMarkdownRenderer.RenderHomeSearchResults(lead, listings);
        Assert.DoesNotContain("sqft", result);
    }

    [Fact]
    public void RenderHomeSearchResults_EmptyList_ProducesNoListingsMessage()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderHomeSearchResults(lead, []);
        Assert.Contains("No listings found", result);
    }

    [Fact]
    public void RenderHomeSearchResults_YamlContainsListingCount()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderHomeSearchResults(lead, MakeListings());
        Assert.Contains("listingCount: 2", result);
    }

    [Fact]
    public void RenderHomeSearchResults_YamlContainsLeadId()
    {
        var lead = MakeFullLead();
        var result = LeadMarkdownRenderer.RenderHomeSearchResults(lead, MakeListings());
        Assert.Contains($"leadId: {lead.Id}", result);
    }
}
