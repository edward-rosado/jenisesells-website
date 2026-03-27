using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Workers.Leads;

namespace RealEstateStar.Workers.Leads.Tests;

public class CmaPdfGeneratorTests
{
    // ---------------------------------------------------------------------------
    // Test data helpers
    // ---------------------------------------------------------------------------

    internal static Lead MakeLead(SellerDetails? sellerDetails = null) => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "jenise-buckalew",
        LeadType = LeadType.Seller,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-000-0000",
        Timeline = "3-6 months",
        SellerDetails = sellerDetails ?? MakeSellerDetails()
    };

    internal static SellerDetails MakeSellerDetails(
        int? beds = 3,
        int? baths = 2,
        int? sqft = 1800) => new()
    {
        Address = "123 Oak Ave",
        City = "Springfield",
        State = "NJ",
        Zip = "07081",
        Beds = beds,
        Baths = baths,
        Sqft = sqft
    };

    internal static CmaAnalysis MakeAnalysis(string? pricingRecommendation = null) => new()
    {
        ValueLow = 480_000m,
        ValueMid = 510_000m,
        ValueHigh = 540_000m,
        MarketNarrative = "The market is competitive.",
        MarketTrend = "Seller's Market",
        MedianDaysOnMarket = 14,
        PricingRecommendation = pricingRecommendation
    };

    internal static List<Comp> MakeComps(int count = 2) =>
        Enumerable.Range(1, count).Select(i => new Comp
        {
            Address = $"{i * 100} Elm St",
            SalePrice = 500_000m + i * 10_000,
            SaleDate = new DateOnly(2025, 1, i),
            Beds = 3,
            Baths = 2,
            Sqft = 1800,
            DistanceMiles = 0.3 * i,
            Source = CompSource.Zillow
        }).ToList();

    internal static AccountConfig MakeAgentConfig(
        string? title = "REALTOR®",
        string? brokerageName = "Keller Williams",
        string? tagline = "Helping families find home.",
        List<string>? serviceAreas = null,
        List<string>? languages = null) => new()
    {
        Handle = "jenise-buckalew",
        Agent = new AccountAgent
        {
            Name = "Jenise Buckalew",
            Title = title,
            Phone = "555-123-4567",
            Email = "jenise@example.com",
            Tagline = tagline,
            Languages = languages ?? ["English", "Spanish"]
        },
        Brokerage = brokerageName is not null
            ? new AccountBrokerage { Name = brokerageName }
            : null,
        Location = new AccountLocation
        {
            State = "NJ",
            ServiceAreas = serviceAreas ?? ["Springfield", "Millburn"]
        }
    };

    private static CmaPdfGenerator MakeGenerator(out Mock<ILogger<CmaPdfGenerator>> loggerMock)
    {
        loggerMock = new Mock<ILogger<CmaPdfGenerator>>();
        return new CmaPdfGenerator(loggerMock.Object);
    }

    // ---------------------------------------------------------------------------
    // BuildFullAddress
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildFullAddress_NullSellerDetails_ReturnsAddressNotProvided()
    {
        var result = CmaPdfGenerator.BuildFullAddress(null);

        result.Should().Be("Address not provided");
    }

    [Fact]
    public void BuildFullAddress_WithSellerDetails_ReturnsFormattedAddress()
    {
        var sd = MakeSellerDetails();

        var result = CmaPdfGenerator.BuildFullAddress(sd);

        result.Should().Be("123 Oak Ave, Springfield, NJ 07081");
    }

    // ---------------------------------------------------------------------------
    // FormatCurrency / FormatPricePerSqft
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatCurrency_FormatsWithDollarSignAndNoDecimals()
    {
        var result = CmaPdfGenerator.FormatCurrency(510_000m);

        result.Should().Be("$510,000");
    }

    [Fact]
    public void FormatPricePerSqft_FormatsWithTwoDecimalPlaces()
    {
        var result = CmaPdfGenerator.FormatPricePerSqft(277.78m);

        result.Should().Be("$277.78");
    }

    // ---------------------------------------------------------------------------
    // GenerateAsync — happy path (Lean, Standard, Comprehensive)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_Lean_CreatesPdfFileOnDisk()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig();
        var ct = CancellationToken.None;

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Lean, ct);

        try
        {
            File.Exists(path).Should().BeTrue();
            new FileInfo(path).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GenerateAsync_Standard_CreatesPdfFileOnDisk()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig();
        var ct = CancellationToken.None;

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Standard, ct);

        try
        {
            File.Exists(path).Should().BeTrue();
            new FileInfo(path).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GenerateAsync_Comprehensive_CreatesPdfFileOnDisk()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead();
        var analysis = MakeAnalysis(pricingRecommendation: "List at $505,000 to attract multiple offers.");
        var comps = MakeComps();
        var agent = MakeAgentConfig();
        var ct = CancellationToken.None;

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Comprehensive, ct);

        try
        {
            File.Exists(path).Should().BeTrue();
            new FileInfo(path).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GenerateAsync_Comprehensive_WithoutPricingRecommendation_CreatesPdfFileOnDisk()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead();
        var analysis = MakeAnalysis(pricingRecommendation: null);
        var comps = MakeComps();
        var agent = MakeAgentConfig();
        var ct = CancellationToken.None;

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Comprehensive, ct);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ---------------------------------------------------------------------------
    // GenerateAsync — error path (CMA-PDF-003)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_WhenGenerationThrows_LogsErrorAndRethrows()
    {
        var loggerMock = new Mock<ILogger<CmaPdfGenerator>>();
        var generator = new CmaPdfGenerator(loggerMock.Object);

        // Pass a lead with null SellerDetails so BuildFullAddress returns "Address not provided"
        // but the cancellation token is already cancelled, forcing Task.Run to throw.
        var lead = MakeLead(sellerDetails: null);
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Lean, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[CMA-PDF-003]")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ---------------------------------------------------------------------------
    // AddCoverPage — with/without title and brokerage
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_Lean_WithoutAgentTitle_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig(title: null);

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Lean, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GenerateAsync_Lean_WithoutBrokerage_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig(brokerageName: null);

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Lean, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ---------------------------------------------------------------------------
    // AddPropertyOverview — beds/baths/sqft null vs present
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_Standard_WithNullBedsAndBaths_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead(MakeSellerDetails(beds: null, baths: null, sqft: null));
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig();

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Standard, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ---------------------------------------------------------------------------
    // AddPricePerSqftAnalysis — empty comps list, sqft null, sqft = 0
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_Comprehensive_WithEmptyComps_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = new List<Comp>();
        var agent = MakeAgentConfig();

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Comprehensive, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GenerateAsync_Comprehensive_WithNullSqft_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead(MakeSellerDetails(sqft: null));
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig();

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Comprehensive, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GenerateAsync_Comprehensive_WithZeroSqft_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead(MakeSellerDetails(sqft: 0));
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig();

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Comprehensive, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ---------------------------------------------------------------------------
    // AddAboutAgent — tagline, serviceAreas, languages conditionals
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_Lean_WithoutTagline_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig(tagline: null);

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Lean, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GenerateAsync_Lean_WithEmptyServiceAreas_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig(serviceAreas: []);

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Lean, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GenerateAsync_Lean_WithEmptyLanguages_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig(languages: []);

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Lean, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GenerateAsync_Lean_WithNullSellerDetails_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead(sellerDetails: null);
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig();

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Lean, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ---------------------------------------------------------------------------
    // AddPropertyOverview — sd == null (Standard/Comprehensive with null SellerDetails)
    // Exercises the sd?.Beds?.ToString() ?? "—" path where sd itself is null
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_Standard_WithNullSellerDetails_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead(sellerDetails: null);
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig();

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Standard, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ---------------------------------------------------------------------------
    // AddPricePerSqftAnalysis — sd == null with comps present
    // Exercises the sd?.Sqft is { } sqft path where sd itself is null
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_Comprehensive_WithNullSellerDetails_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead(sellerDetails: null);
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = MakeAgentConfig();

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Comprehensive, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ---------------------------------------------------------------------------
    // AddAboutAgent — null Agent (exercises agent.Agent?.Languages ?? [] and
    // agent.Location?.ServiceAreas ?? [] null-coalescing branches)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_Lean_WithNullAgent_CreatesPdfSuccessfully()
    {
        var generator = MakeGenerator(out _);
        var lead = MakeLead();
        var analysis = MakeAnalysis();
        var comps = MakeComps();
        var agent = new AccountConfig
        {
            Handle = "bare",
            Agent = null,
            Brokerage = null,
            Location = null
        };

        var path = await generator.GenerateAsync(lead, analysis, comps, agent, ReportType.Lean, CancellationToken.None);

        try
        {
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}

// ---------------------------------------------------------------------------
// Comp — PricePerSqft property
// ---------------------------------------------------------------------------

public class CompTests
{
    [Fact]
    public void PricePerSqft_WhenSqftGreaterThanZero_ReturnsSalePriceDividedBySqft()
    {
        var comp = new Comp
        {
            Address = "123 Main St",
            SalePrice = 500_000m,
            SaleDate = new DateOnly(2025, 1, 15),
            Beds = 3,
            Baths = 2,
            Sqft = 2000,
            DistanceMiles = 0.5,
            Source = CompSource.Zillow
        };

        comp.PricePerSqft.Should().Be(250m);
    }

    [Fact]
    public void PricePerSqft_WhenSqftIsZero_ReturnsZero()
    {
        var comp = new Comp
        {
            Address = "456 Oak Ave",
            SalePrice = 400_000m,
            SaleDate = new DateOnly(2025, 3, 10),
            Beds = 3,
            Baths = 2,
            Sqft = 0,
            DistanceMiles = 0.3,
            Source = CompSource.Redfin
        };

        comp.PricePerSqft.Should().Be(0m);
    }
}
