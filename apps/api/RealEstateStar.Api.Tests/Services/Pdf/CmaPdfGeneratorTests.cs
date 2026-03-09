using FluentAssertions;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services.Pdf;

namespace RealEstateStar.Api.Tests.Services.Pdf;

public class CmaPdfGeneratorTests
{
    private static AgentConfig CreateTestAgent() => new()
    {
        Id = "test-agent",
        Identity = new AgentIdentity
        {
            Name = "Jane Smith",
            Title = "Realtor",
            Brokerage = "Test Realty LLC",
            BrokerageId = "BR-12345",
            Phone = "(555) 123-4567",
            Email = "jane@testrealty.com",
            Website = "https://testrealty.com",
            Languages = ["English", "Spanish"],
            Tagline = "Your home, your future.",
            LicenseId = "LIC-9999"
        },
        Location = new AgentLocation
        {
            State = "NJ",
            OfficeAddress = "100 Main St, Springfield, NJ 07081",
            ServiceAreas = ["Springfield", "Millburn", "Summit"]
        },
        Branding = new AgentBranding
        {
            PrimaryColor = "#1a3c5e",
            SecondaryColor = "#f5a623",
            AccentColor = "#ffffff"
        }
    };

    private static Lead CreateTestLead() => new()
    {
        FirstName = "John",
        LastName = "Doe",
        Email = "john.doe@example.com",
        Phone = "(555) 987-6543",
        Address = "42 Oak Lane",
        City = "Springfield",
        State = "NJ",
        Zip = "07081",
        Timeline = "ASAP",
        Beds = 4,
        Baths = 3,
        Sqft = 2200
    };

    private static List<Comp> CreateTestComps() =>
    [
        new()
        {
            Address = "10 Maple St, Springfield, NJ",
            SalePrice = 525_000m,
            SaleDate = new DateOnly(2026, 1, 15),
            Beds = 4,
            Baths = 2,
            Sqft = 2100,
            DaysOnMarket = 18,
            DistanceMiles = 0.3,
            Source = CompSource.Mls
        },
        new()
        {
            Address = "22 Elm Ave, Springfield, NJ",
            SalePrice = 550_000m,
            SaleDate = new DateOnly(2026, 2, 1),
            Beds = 4,
            Baths = 3,
            Sqft = 2300,
            DaysOnMarket = 12,
            DistanceMiles = 0.5,
            Source = CompSource.Zillow
        },
        new()
        {
            Address = "35 Pine Rd, Millburn, NJ",
            SalePrice = 575_000m,
            SaleDate = new DateOnly(2025, 12, 20),
            Beds = 3,
            Baths = 2,
            Sqft = 2050,
            DaysOnMarket = 25,
            DistanceMiles = 0.8,
            Source = CompSource.Redfin
        }
    ];

    private static CmaAnalysis CreateTestAnalysis() => new()
    {
        ValueLow = 510_000m,
        ValueMid = 545_000m,
        ValueHigh = 580_000m,
        MarketNarrative = "The Springfield market is experiencing steady demand with limited inventory, "
            + "driving moderate price increases. Properties in the subject's neighborhood are selling "
            + "within 2-3 weeks of listing.",
        PricingRecommendation = "List at $549,900 to attract competitive offers while maximizing value.",
        LeadInsights = "The seller appears motivated with an ASAP timeline.",
        ConversationStarters = ["Your home's location near the park adds value.", "Recent upgrades could push the price higher."],
        MarketTrend = "Appreciating",
        MedianDaysOnMarket = 18
    };

    private static LeadResearch CreateTestResearch() => new()
    {
        Occupation = "Software Engineer",
        Employer = "Tech Corp",
        PurchaseDate = new DateOnly(2018, 6, 15),
        PurchasePrice = 425_000m,
        TaxAssessment = 495_000m,
        AnnualPropertyTax = 11_200m,
        EstimatedEquityLow = 85_000m,
        EstimatedEquityHigh = 155_000m,
        NeighborhoodContext = "Springfield is a family-friendly suburb with top-rated schools, "
            + "easy commute to NYC, and a vibrant downtown area with shops and restaurants.",
        YearBuilt = 1985,
        LotSize = 0.25m,
        LotSizeUnit = "acres"
    };

    [Fact]
    public void Generate_CreatesFileOnDisk()
    {
        var generator = new CmaPdfGenerator();
        var path = Path.Combine(Path.GetTempPath(), $"cma_lean_{Guid.NewGuid()}.pdf");

        try
        {
            generator.Generate(path, CreateTestAgent(), CreateTestLead(),
                CreateTestComps(), CreateTestAnalysis(), CreateTestResearch(),
                ReportType.Lean, CancellationToken.None);

            File.Exists(path).Should().BeTrue("PDF file should be created on disk");
            new FileInfo(path).Length.Should().BeGreaterThan(0, "PDF file should have content");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Generate_ComprehensiveReport_HasMorePages()
    {
        var generator = new CmaPdfGenerator();
        var leanPath = Path.Combine(Path.GetTempPath(), $"cma_lean_{Guid.NewGuid()}.pdf");
        var compPath = Path.Combine(Path.GetTempPath(), $"cma_comp_{Guid.NewGuid()}.pdf");

        try
        {
            var agent = CreateTestAgent();
            var lead = CreateTestLead();
            var comps = CreateTestComps();
            var analysis = CreateTestAnalysis();
            var research = CreateTestResearch();

            generator.Generate(leanPath, agent, lead, comps, analysis, research, ReportType.Lean, CancellationToken.None);
            generator.Generate(compPath, agent, lead, comps, analysis, research, ReportType.Comprehensive, CancellationToken.None);

            var leanSize = new FileInfo(leanPath).Length;
            var compSize = new FileInfo(compPath).Length;

            compSize.Should().BeGreaterThan(leanSize,
                "Comprehensive report should be larger than Lean report due to additional pages");
        }
        finally
        {
            if (File.Exists(leanPath)) File.Delete(leanPath);
            if (File.Exists(compPath)) File.Delete(compPath);
        }
    }
}
