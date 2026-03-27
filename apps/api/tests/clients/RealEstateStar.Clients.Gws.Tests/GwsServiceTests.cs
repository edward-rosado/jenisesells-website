using FluentAssertions;

namespace RealEstateStar.Clients.Gws.Tests;

public class GwsServiceTests
{
    [Fact]
    public void BuildLeadFolderPath_FormatsCorrectly()
    {
        var result = LeadBriefFormatter.BuildLeadFolderPath("Jane Doe", "123 Main St, Edison NJ");

        result.Should().Be("Real Estate Star/1 - Leads/Jane Doe/123 Main St, Edison NJ");
    }

    [Fact]
    public void BuildLeadBriefContent_IncludesAllSections()
    {
        var content = LeadBriefFormatter.BuildLeadBriefContent(new LeadBriefData
        {
            LeadName = "Jane Doe",
            Address = "123 Main St, Edison NJ 08817",
            Timeline = "ASAP",
            SubmittedAt = new DateTime(2026, 3, 9, 14, 30, 0),
            Occupation = "Software Engineer",
            Employer = "Acme Corp",
            PurchaseDate = new DateOnly(2018, 6, 15),
            PurchasePrice = 350_000m,
            OwnershipDuration = "~8 years",
            EquityRange = "$100k\u2013$150k",
            LifeEvent = "Relocating for new job",
            Beds = 3,
            Baths = 2,
            Sqft = 1800,
            YearBuilt = 1995,
            LotSize = "0.25 acres",
            TaxAssessment = 380_000m,
            AnnualTax = 8_500m,
            CompCount = 12,
            SearchRadius = "0.5 miles",
            ValueRange = "$375,000\u2013$425,000",
            MedianDom = 21,
            MarketTrend = "seller's",
            ConversationStarters = ["They may be feeling the pressure of a tight relocation timeline", "Their equity position gives them strong negotiating power"],
            LeadEmail = "jane.doe@example.com",
            LeadPhone = "(555) 123-4567",
            PdfLink = "https://storage.example.com/cma/jane-doe.pdf"
        });

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
        var content = LeadBriefFormatter.BuildLeadBriefContent(new LeadBriefData
        {
            LeadName = "Bob Smith",
            Address = "456 Oak Ave, Edison NJ 08817",
            Timeline = "1-3 months",
            SubmittedAt = new DateTime(2026, 3, 9, 14, 30, 0),
            CompCount = 5,
            SearchRadius = "1 mile",
            ValueRange = "$300,000-$350,000",
            MedianDom = 30,
            MarketTrend = "buyer's",
            ConversationStarters = [],
            LeadEmail = "bob@example.com",
            LeadPhone = "(555) 999-0000",
            PdfLink = "https://storage.example.com/cma/bob.pdf"
        });

        content.Should().Contain("Call within 2 hours");
        content.Should().Contain("serious seller");
    }

    [Fact]
    public void BuildLeadBriefContent_UsesDefaultTimeline()
    {
        var content = LeadBriefFormatter.BuildLeadBriefContent(new LeadBriefData
        {
            LeadName = "Alice Jones",
            Address = "789 Elm St, Edison NJ 08817",
            Timeline = "Just curious",
            SubmittedAt = new DateTime(2026, 1, 1),
            CompCount = 3,
            SearchRadius = "2 miles",
            ValueRange = "$250,000-$280,000",
            MedianDom = 45,
            MarketTrend = "neutral",
            ConversationStarters = [],
            LeadEmail = "alice@example.com",
            LeadPhone = "(555) 111-2222",
            PdfLink = "https://storage.example.com/cma/alice.pdf"
        });

        content.Should().Contain("Call within 24 hours");
        content.Should().Contain("build the relationship");
    }

    [Fact]
    public void BuildLeadBriefContent_OmitsNullOptionalSections()
    {
        var content = LeadBriefFormatter.BuildLeadBriefContent(new LeadBriefData
        {
            LeadName = "Test User",
            Address = "100 Test St",
            Timeline = "ASAP",
            SubmittedAt = new DateTime(2026, 1, 1),
            CompCount = 0,
            SearchRadius = "1 mile",
            ValueRange = "$0",
            MedianDom = 0,
            MarketTrend = "neutral",
            ConversationStarters = [],
            LeadEmail = "test@test.com",
            LeadPhone = "555-0000",
            PdfLink = "link"
        });

        content.Should().NotContain("Purchased");
        content.Should().NotContain("Owned for");
        content.Should().NotContain("Estimated equity");
        content.Should().NotContain("Lot:");
        content.Should().NotContain("Current tax assessment");
        content.Should().NotContain("Annual property taxes");
    }

    [Fact]
    public void BuildLeadBriefContent_IncludesOccupationOnly_WhenEmployerIsNull()
    {
        var content = LeadBriefFormatter.BuildLeadBriefContent(new LeadBriefData
        {
            LeadName = "Solo Worker",
            Address = "100 Test St",
            Timeline = "ASAP",
            SubmittedAt = new DateTime(2026, 1, 1),
            Occupation = "Freelancer",
            Employer = null,
            CompCount = 0,
            SearchRadius = "1 mile",
            ValueRange = "$0",
            MedianDom = 0,
            MarketTrend = "neutral",
            ConversationStarters = [],
            LeadEmail = "test@test.com",
            LeadPhone = "555-0000",
            PdfLink = "link"
        });

        content.Should().Contain("Freelancer");
    }

    [Fact]
    public void BuildLeadBriefContent_IncludesEmployerOnly_WhenOccupationIsNull()
    {
        var content = LeadBriefFormatter.BuildLeadBriefContent(new LeadBriefData
        {
            LeadName = "Corp Person",
            Address = "100 Test St",
            Timeline = "ASAP",
            SubmittedAt = new DateTime(2026, 1, 1),
            Occupation = null,
            Employer = "BigCorp",
            CompCount = 0,
            SearchRadius = "1 mile",
            ValueRange = "$0",
            MedianDom = 0,
            MarketTrend = "neutral",
            ConversationStarters = [],
            LeadEmail = "test@test.com",
            LeadPhone = "555-0000",
            PdfLink = "link"
        });

        content.Should().Contain("BigCorp");
    }

    [Fact]
    public void BuildLeadBriefContent_OmitsPropertyLine_WhenBedsOrBathsNull()
    {
        var content = LeadBriefFormatter.BuildLeadBriefContent(new LeadBriefData
        {
            LeadName = "Test User",
            Address = "100 Test St",
            Timeline = "6-12 months",
            SubmittedAt = new DateTime(2026, 1, 1),
            Beds = 3,
            Baths = null,
            Sqft = 1800,
            YearBuilt = 2000,
            CompCount = 0,
            SearchRadius = "1 mile",
            ValueRange = "$0",
            MedianDom = 0,
            MarketTrend = "neutral",
            ConversationStarters = [],
            LeadEmail = "test@test.com",
            LeadPhone = "555-0000",
            PdfLink = "link"
        });

        content.Should().NotContain("bed /");
    }

    [Fact]
    public void BuildLeadBriefContent_IncludesPropertyLine_WhenAllPropertyFieldsPresent()
    {
        var content = LeadBriefFormatter.BuildLeadBriefContent(new LeadBriefData
        {
            LeadName = "Test User",
            Address = "100 Test St",
            Timeline = "ASAP",
            SubmittedAt = new DateTime(2026, 1, 1),
            Beds = 4,
            Baths = 3,
            Sqft = 2500,
            YearBuilt = 2005,
            CompCount = 0,
            SearchRadius = "1 mile",
            ValueRange = "$0",
            MedianDom = 0,
            MarketTrend = "neutral",
            ConversationStarters = [],
            LeadEmail = "test@test.com",
            LeadPhone = "555-0000",
            PdfLink = "link"
        });

        content.Should().Contain("4 bed / 3 bath / 2,500 sqft, built 2005");
    }

    [Fact]
    public void BuildLeadBriefContent_OmitsPurchaseLine_WhenPurchaseDateOnly()
    {
        var content = LeadBriefFormatter.BuildLeadBriefContent(new LeadBriefData
        {
            LeadName = "Test User",
            Address = "100 Test St",
            Timeline = "ASAP",
            SubmittedAt = new DateTime(2026, 1, 1),
            PurchaseDate = new DateOnly(2020, 1, 1),
            PurchasePrice = null,
            CompCount = 0,
            SearchRadius = "1 mile",
            ValueRange = "$0",
            MedianDom = 0,
            MarketTrend = "neutral",
            ConversationStarters = [],
            LeadEmail = "test@test.com",
            LeadPhone = "555-0000",
            PdfLink = "link"
        });

        content.Should().NotContain("Purchased");
    }

    [Fact]
    public void BuildLeadBriefContent_IncludesTaxAssessmentAndAnnualTax()
    {
        var content = LeadBriefFormatter.BuildLeadBriefContent(new LeadBriefData
        {
            LeadName = "Test User",
            Address = "100 Test St",
            Timeline = "ASAP",
            SubmittedAt = new DateTime(2026, 1, 1),
            TaxAssessment = 400_000m,
            AnnualTax = 9_000m,
            CompCount = 0,
            SearchRadius = "1 mile",
            ValueRange = "$0",
            MedianDom = 0,
            MarketTrend = "neutral",
            ConversationStarters = [],
            LeadEmail = "test@test.com",
            LeadPhone = "555-0000",
            PdfLink = "link"
        });

        content.Should().Contain("Current tax assessment: $400,000");
        content.Should().Contain("Annual property taxes: $9,000");
    }
}
