using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealEstateStar.Workers.Lead.CMA.Tests;

public class CompAggregatorTests
{
    private static CompSearchRequest MakeRequest() => new()
    {
        Address = "123 Main St",
        City = "Springfield",
        State = "NJ",
        Zip = "07081",
        Beds = 3,
        Baths = 2,
        SqFt = 1800
    };

    private static Comp MakeComp(
        string address = "456 Oak Ave",
        DateOnly? saleDate = null,
        CompSource source = CompSource.Zillow) => new()
        {
            Address = address,
            SalePrice = 500_000m,
            SaleDate = saleDate ?? new DateOnly(2025, 1, 15),
            Beds = 3,
            Baths = 2,
            Sqft = 1800,
            DistanceMiles = 0.5,
            Source = source
        };

    private static Mock<ICompSource> MakeSource(string name, List<Comp> comps)
    {
        var mock = new Mock<ICompSource>();
        mock.Setup(s => s.Name).Returns(name);
        mock.Setup(s => s.FetchAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comps);
        return mock;
    }

    private static Mock<ICompSource> MakeThrowingSource(string name)
    {
        var mock = new Mock<ICompSource>();
        mock.Setup(s => s.Name).Returns(name);
        mock.Setup(s => s.FetchAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("source down"));
        return mock;
    }

    private static CompAggregator MakeAggregator(IEnumerable<ICompSource> sources) =>
        new(sources, NullLogger<CompAggregator>.Instance);

    // ---------------------------------------------------------------------------
    // FetchCompsAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FetchCompsAsync_RunsAllSourcesInParallel()
    {
        var compsA = new List<Comp> { MakeComp("1 A St"), MakeComp("2 A St") };
        var compsB = new List<Comp> { MakeComp("1 B St"), MakeComp("2 B St") };
        var compsC = new List<Comp> { MakeComp("1 C St"), MakeComp("2 C St") };

        var sourceA = MakeSource("Zillow", compsA);
        var sourceB = MakeSource("RealtorCom", compsB);
        var sourceC = MakeSource("Redfin", compsC);

        var aggregator = MakeAggregator([sourceA.Object, sourceB.Object, sourceC.Object]);

        var result = await aggregator.FetchCompsAsync(MakeRequest(), CancellationToken.None);

        // All 6 raw comps are deduplicated and ranked; FilterAndRankComps caps at 5
        result.Count.Should().BeGreaterThan(0);
        result.Count.Should().BeLessThanOrEqualTo(6);
        sourceA.Verify(s => s.FetchAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        sourceB.Verify(s => s.FetchAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        sourceC.Verify(s => s.FetchAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FetchCompsAsync_ContinuesWhenOneSourceFails()
    {
        var compsB = new List<Comp> { MakeComp("1 B St"), MakeComp("2 B St") };
        var compsC = new List<Comp> { MakeComp("1 C St"), MakeComp("2 C St") };

        var failingSource = MakeThrowingSource("Zillow");
        var sourceB = MakeSource("RealtorCom", compsB);
        var sourceC = MakeSource("Redfin", compsC);

        var aggregator = MakeAggregator([failingSource.Object, sourceB.Object, sourceC.Object]);

        var result = await aggregator.FetchCompsAsync(MakeRequest(), CancellationToken.None);

        result.Should().HaveCount(4);
    }

    [Fact]
    public async Task FetchCompsAsync_ReturnsEmptyWhenAllSourcesFail()
    {
        var sourceA = MakeThrowingSource("Zillow");
        var sourceB = MakeThrowingSource("RealtorCom");
        var sourceC = MakeThrowingSource("Redfin");

        var aggregator = MakeAggregator([sourceA.Object, sourceB.Object, sourceC.Object]);

        var result = await aggregator.FetchCompsAsync(MakeRequest(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // Deduplicate
    // ---------------------------------------------------------------------------

    [Fact]
    public void Deduplicate_RemovesDuplicatesByAddress_KeepsLowerEnumValue()
    {
        // Same address from two sources — Zillow (0) wins over Redfin (2)
        var saleDate = new DateOnly(2025, 3, 1);
        var zillowComp = MakeComp("123 Main St", saleDate, CompSource.Zillow);
        var redfinComp = MakeComp("123 Main St", saleDate, CompSource.Redfin);

        var result = CompAggregator.Deduplicate([zillowComp, redfinComp]);

        result.Should().HaveCount(1);
        result[0].Source.Should().Be(CompSource.Zillow);
    }

    [Fact]
    public void Deduplicate_SameAddressDifferentSaleDates_DedupsToOne()
    {
        // RentCast can return the same property with different sale dates for the same
        // underlying transaction — street+zip dedup collapses them to one record.
        var comp1 = MakeComp("123 Main St", new DateOnly(2025, 1, 1));
        var comp2 = MakeComp("123 Main St", new DateOnly(2025, 6, 1));

        var result = CompAggregator.Deduplicate([comp1, comp2]);

        result.Should().HaveCount(1);
    }

    // ---------------------------------------------------------------------------
    // NormalizeAddress
    // ---------------------------------------------------------------------------

    [Fact]
    public void NormalizeAddress_StripsDotsCommasExtraSpaces()
    {
        var result = CompAggregator.NormalizeAddress("123 Main St., Suite 4");

        result.Should().Be("123 MAIN ST SUITE 4");
    }

    // ---------------------------------------------------------------------------
    // Comp.PricePerSqft
    // ---------------------------------------------------------------------------

    [Fact]
    public void Comp_PricePerSqft_WhenSqftIsZero_ReturnsZero()
    {
        var comp = new Comp
        {
            Address = "1 Test St",
            SalePrice = 500_000m,
            SaleDate = new DateOnly(2025, 1, 1),
            Beds = 3,
            Baths = 2,
            Sqft = 0,
            DistanceMiles = 0.5,
            Source = CompSource.Zillow
        };

        comp.PricePerSqft.Should().Be(0m);
    }

    [Fact]
    public void Comp_PricePerSqft_WhenSqftIsPositive_ReturnsSalePriceDividedBySqft()
    {
        var comp = new Comp
        {
            Address = "1 Test St",
            SalePrice = 360_000m,
            SaleDate = new DateOnly(2025, 1, 1),
            Beds = 3,
            Baths = 2,
            Sqft = 1800,
            DistanceMiles = 0.5,
            Source = CompSource.Zillow
        };

        comp.PricePerSqft.Should().Be(200m);
    }

    // ---------------------------------------------------------------------------
    // FilterAndRankComps
    // ---------------------------------------------------------------------------

    private static Comp MakeCompWithZip(
        string address,
        double distanceMiles = 0.5,
        double? correlation = null) => new()
        {
            Address = address,
            SalePrice = 500_000m,
            SaleDate = new DateOnly(2025, 1, 15),
            Beds = 3,
            Baths = 2,
            Sqft = 1800,
            DistanceMiles = distanceMiles,
            Source = CompSource.RentCast,
            Correlation = correlation
        };

    [Fact]
    public void FilterAndRankComps_PrefersSameZipCode()
    {
        // 3 comps in subject zip 07081, 3 in a different zip
        var sameZip = new[]
        {
            MakeCompWithZip("100 A St, Springfield, NJ 07081"),
            MakeCompWithZip("101 B St, Springfield, NJ 07081"),
            MakeCompWithZip("102 C St, Springfield, NJ 07081")
        };
        var diffZip = new[]
        {
            MakeCompWithZip("200 X St, Maplewood, NJ 07040"),
            MakeCompWithZip("201 Y St, Maplewood, NJ 07040"),
            MakeCompWithZip("202 Z St, Maplewood, NJ 07040")
        };

        var result = CompAggregator.FilterAndRankComps([.. sameZip, .. diffZip], "07081");

        // All 3 same-zip comps should appear before any different-zip comps
        var addresses = result.Select(c => c.Address).ToList();
        var lastSameZipIndex = addresses
            .Select((a, i) => (a, i))
            .Where(x => sameZip.Any(s => s.Address == x.a))
            .Max(x => x.i);
        var firstDiffZipIndex = addresses
            .Select((a, i) => (a, i))
            .Where(x => diffZip.Any(d => d.Address == x.a))
            .Min(x => x.i);

        lastSameZipIndex.Should().BeLessThan(firstDiffZipIndex);
    }

    [Fact]
    public void FilterAndRankComps_DeduplicatesSameStreetDifferentMunicipality()
    {
        // Same street number + name + zip, different municipality labels
        var comp1 = MakeCompWithZip("123 Main St, Springfield, NJ 07081");
        var comp2 = MakeCompWithZip("123 Main St, Springfield Township, NJ 07081");

        var deduplicated = CompAggregator.Deduplicate([comp1, comp2]);

        deduplicated.Should().HaveCount(1);
    }

    [Fact]
    public void FilterAndRankComps_DeprioritizesDistantComps()
    {
        // Distant comp (15 miles) vs close comp (2 miles), both in same zip
        var closeComp = MakeCompWithZip("100 A St, Springfield, NJ 07081", distanceMiles: 2.0);
        var farComp = MakeCompWithZip("200 B St, Springfield, NJ 07081", distanceMiles: 15.0);

        var result = CompAggregator.FilterAndRankComps([farComp, closeComp], "07081");

        // Close comp should come before distant comp
        var closeIndex = result.FindIndex(c => c.Address == closeComp.Address);
        var farIndex = result.FindIndex(c => c.Address == farComp.Address);

        closeIndex.Should().BeLessThan(farIndex);
    }

    [Fact]
    public void FilterAndRankComps_BackfillsFromAdjacentZip()
    {
        // Only 2 same-zip comps, 3 adjacent-zip comps (same first 3 digits)
        var sameZip = new[]
        {
            MakeCompWithZip("100 A St, Springfield, NJ 07081"),
            MakeCompWithZip("101 B St, Springfield, NJ 07081")
        };
        var adjacentZip = new[]
        {
            MakeCompWithZip("200 X St, Maplewood, NJ 07082"),
            MakeCompWithZip("201 Y St, Maplewood, NJ 07083"),
            MakeCompWithZip("202 Z St, Maplewood, NJ 07084")
        };

        var result = CompAggregator.FilterAndRankComps([.. sameZip, .. adjacentZip], "07081");

        result.Should().HaveCount(5);
        result.Should().Contain(c => c.Address == sameZip[0].Address);
        result.Should().Contain(c => c.Address == sameZip[1].Address);
    }

    [Fact]
    public void FilterAndRankComps_LimitsToFive()
    {
        var comps = Enumerable.Range(1, 8)
            .Select(i => MakeCompWithZip($"10{i} Test St, Springfield, NJ 07081"))
            .ToList();

        var result = CompAggregator.FilterAndRankComps(comps, "07081");

        result.Should().HaveCount(5);
    }

    [Fact]
    public void FilterAndRankComps_ReturnsEmpty_WhenInputIsEmpty()
    {
        var result = CompAggregator.FilterAndRankComps([], "07081");

        result.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeAddressForDedup_SameStreetDifferentMunicipality_ReturnsSameKey()
    {
        var key1 = CompAggregator.NormalizeAddressForDedup("123 Main St, Springfield, NJ 07081");
        var key2 = CompAggregator.NormalizeAddressForDedup("123 Main St, Springfield Township, NJ 07081");

        key1.Should().Be(key2);
    }

    [Fact]
    public void ExtractZip_ExtractsZipFromAddress()
    {
        var zip = CompAggregator.ExtractZip("123 Main St, Springfield, NJ 07081");

        zip.Should().Be("07081");
    }

    [Fact]
    public void ExtractZip_ReturnsEmpty_WhenNoZip()
    {
        var zip = CompAggregator.ExtractZip("123 Main St, Springfield, NJ");

        zip.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // Property type filtering (AGG-009)
    // ---------------------------------------------------------------------------

    [Fact]
    public void FilterAndRankComps_ExcludesCompsWithMismatchedPropertyType()
    {
        // Subject is Single Family — commercial/multi-family comps should be excluded
        var residential = new Comp
        {
            Address = "100 Oak Ave, Keansburg, NJ 07734",
            SalePrice = 300_000m,
            SaleDate = new DateOnly(2024, 10, 1),
            Beds = 3, Baths = 2, Sqft = 1400,
            DistanceMiles = 0.3,
            Source = CompSource.RentCast,
            PropertyType = "Single Family"
        };
        var commercial = new Comp
        {
            Address = "200 Commerce Dr, Keansburg, NJ 07734",
            SalePrice = 500_000m,
            SaleDate = new DateOnly(2024, 10, 1),
            Beds = 0, Baths = 0, Sqft = 3000,
            DistanceMiles = 0.5,
            Source = CompSource.RentCast,
            PropertyType = "Commercial"
        };

        var request = new CompSearchRequest
        {
            Address = "50 Main St",
            City = "Keansburg",
            State = "NJ",
            Zip = "07734",
            PropertyType = "Single Family"
        };

        var result = CompAggregator.FilterAndRankComps(
            [residential, commercial], "07734", null, request);

        result.Should().NotContain(c => c.Address == commercial.Address);
        result.Should().Contain(c => c.Address == residential.Address);
    }

    [Fact]
    public void FilterAndRankComps_KeepsCompsWithUnknownPropertyType_WhenSubjectTypeIsKnown()
    {
        // Comps with null/empty PropertyType are kept even when subject type is set —
        // permissive fallback for data gaps.
        var unknownType = new Comp
        {
            Address = "300 Pine St, Keansburg, NJ 07734",
            SalePrice = 310_000m,
            SaleDate = new DateOnly(2024, 10, 1),
            Beds = 3, Baths = 2, Sqft = 1300,
            DistanceMiles = 0.4,
            Source = CompSource.RentCast,
            PropertyType = null
        };

        var request = new CompSearchRequest
        {
            Address = "50 Main St",
            City = "Keansburg",
            State = "NJ",
            Zip = "07734",
            PropertyType = "Single Family"
        };

        var result = CompAggregator.FilterAndRankComps(
            [unknownType], "07734", null, request);

        result.Should().Contain(c => c.Address == unknownType.Address);
    }

    [Fact]
    public void FilterAndRankComps_SkipsPropertyTypeFilter_WhenSubjectTypeIsNull()
    {
        // When request.PropertyType is null, all comps pass regardless of their type.
        var commercial = new Comp
        {
            Address = "200 Commerce Dr, Keansburg, NJ 07734",
            SalePrice = 500_000m,
            SaleDate = new DateOnly(2024, 10, 1),
            Beds = 0, Baths = 0, Sqft = 3000,
            DistanceMiles = 0.5,
            Source = CompSource.RentCast,
            PropertyType = "Commercial"
        };

        var request = new CompSearchRequest
        {
            Address = "50 Main St",
            City = "Keansburg",
            State = "NJ",
            Zip = "07734",
            PropertyType = null
        };

        var result = CompAggregator.FilterAndRankComps(
            [commercial], "07734", null, request);

        result.Should().Contain(c => c.Address == commercial.Address);
    }

    // ---------------------------------------------------------------------------
    // Cross-zip dedup (Problem 2)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Deduplicate_SameUnitDifferentZips_DedupsToOne()
    {
        // "49 Middlesex Rd, Unit B" appears twice — once with Matawan zip, once with Old Bridge zip.
        // These are the same physical unit; only one should survive dedup.
        var matawan = new Comp
        {
            Address = "49 Middlesex Rd, Unit B, Matawan, NJ 07747",
            SalePrice = 310_000m,
            SaleDate = new DateOnly(2024, 9, 1),
            Beds = 2,
            Baths = 1,
            Sqft = 950,
            DistanceMiles = 1.2,
            Source = CompSource.RentCast
        };
        var oldBridge = new Comp
        {
            Address = "49 Middlesex Rd, Unit B, Old Bridge, NJ 08857",
            SalePrice = 310_000m,
            SaleDate = new DateOnly(2024, 9, 1),
            Beds = 2,
            Baths = 1,
            Sqft = 950,
            DistanceMiles = 1.2,
            Source = CompSource.RentCast
        };

        var result = CompAggregator.Deduplicate([matawan, oldBridge]);

        result.Should().HaveCount(1);
    }

    // ---------------------------------------------------------------------------
    // IQR outlier removal (Problem 3)
    // ---------------------------------------------------------------------------

    [Fact]
    public void FilterAndRankComps_ExcludesPricePerSqftOutliers()
    {
        // Real data from failed 308 Myrtle St CMA:
        // $275K for 5,000 sqft = $55/sqft — clearly outlier vs others at $294-$566/sqft.
        var outlier = new Comp
        {
            Address = "1 Outlier Ln, Keansburg, NJ 07734",
            SalePrice = 275_000m,
            SaleDate = new DateOnly(2024, 10, 1),
            Beds = 3,
            Baths = 2,
            Sqft = 5000,  // $55/sqft
            DistanceMiles = 0.8,
            Source = CompSource.RentCast
        };
        var comp1 = new Comp
        {
            Address = "100 A St, Keansburg, NJ 07734",
            SalePrice = 280_000m,
            SaleDate = new DateOnly(2024, 10, 1),
            Beds = 3,
            Baths = 2,
            Sqft = 950,   // $294/sqft
            DistanceMiles = 0.3,
            Source = CompSource.RentCast
        };
        var comp2 = new Comp
        {
            Address = "200 B St, Keansburg, NJ 07734",
            SalePrice = 320_000m,
            SaleDate = new DateOnly(2024, 9, 1),
            Beds = 3,
            Baths = 2,
            Sqft = 1100,  // $291/sqft
            DistanceMiles = 0.5,
            Source = CompSource.RentCast
        };
        var comp3 = new Comp
        {
            Address = "300 C St, Keansburg, NJ 07734",
            SalePrice = 310_000m,
            SaleDate = new DateOnly(2024, 8, 1),
            Beds = 3,
            Baths = 2,
            Sqft = 1000,  // $310/sqft
            DistanceMiles = 0.6,
            Source = CompSource.RentCast
        };
        var comp4 = new Comp
        {
            Address = "400 D St, Keansburg, NJ 07734",
            SalePrice = 350_000m,
            SaleDate = new DateOnly(2024, 7, 1),
            Beds = 3,
            Baths = 2,
            Sqft = 1200,  // $292/sqft
            DistanceMiles = 0.7,
            Source = CompSource.RentCast
        };

        var result = CompAggregator.FilterAndRankComps(
            [outlier, comp1, comp2, comp3, comp4], "07734");

        result.Should().NotContain(c => c.Address == outlier.Address);
        result.Should().Contain(c => c.Address == comp1.Address);
    }

    // ---------------------------------------------------------------------------
    // NormalizeStreetOnly
    // ---------------------------------------------------------------------------

    [Fact]
    public void NormalizeStreetOnly_ExtractsStreetWithUnit()
    {
        // "49 Middlesex Rd Unit B, Matawan, NJ 07747" → "49 MIDDLESEX RD UNIT B"
        var result = CompAggregator.NormalizeStreetOnly("49 Middlesex Rd Unit B, Matawan, NJ 07747");

        result.Should().Contain("49");
        result.Should().Contain("MIDDLESEX");
        result.Should().Contain("RD");
        // City name should not appear in the result
        result.Should().NotContain("MATAWAN");
        result.Should().NotContain("07747");
    }

    [Fact]
    public void NormalizeStreetOnly_SameStreetDifferentCities_ReturnSameKey()
    {
        var key1 = CompAggregator.NormalizeStreetOnly("49 Middlesex Rd Unit B, Matawan, NJ 07747");
        var key2 = CompAggregator.NormalizeStreetOnly("49 Middlesex Rd Unit B, Old Bridge, NJ 08857");

        key1.Should().Be(key2);
    }

    // ---------------------------------------------------------------------------
    // Subject property self-sale filtering
    // ---------------------------------------------------------------------------

    [Fact]
    public void FilterAndRankComps_KeepsRecentSubjectSelfSale()
    {
        // A flip — the subject sold 2 months ago. Keep it as a comp.
        var recentSelfSale = MakeComp("308 Myrtle St, Cliffwood, NJ 07721", 625_000m,
            saleDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)), distance: 0.0);
        var otherComp = MakeComp("100 Oak Ave, Aberdeen, NJ 07721", 550_000m, distance: 0.5);

        var result = CompAggregator.FilterAndRankComps(
            [recentSelfSale, otherComp], "07721");

        result.Should().Contain(c => c.DistanceMiles <= 0.01,
            "recent self-sale (< 6 months) should be kept as a valid comp");
    }

    [Fact]
    public void FilterAndRankComps_ExcludesStaleSubjectSelfSale()
    {
        // The subject sold 2 years ago — not useful as a comp.
        var staleSelfSale = MakeComp("308 Myrtle St, Cliffwood, NJ 07721", 400_000m,
            saleDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-24)), distance: 0.0);
        var otherComp = MakeComp("100 Oak Ave, Aberdeen, NJ 07721", 550_000m, distance: 0.5);

        var result = CompAggregator.FilterAndRankComps(
            [staleSelfSale, otherComp], "07721");

        result.Should().NotContain(c => c.DistanceMiles <= 0.01,
            "stale self-sale (> 6 months) should be excluded");
        result.Should().HaveCount(1);
    }

    private static Comp MakeComp(string address, decimal price,
        DateOnly? saleDate = null, double distance = 1.0) => new()
    {
        Address = address,
        SalePrice = price,
        SaleDate = saleDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
        Beds = 3,
        Baths = 2,
        Sqft = 1500,
        DaysOnMarket = 30,
        DistanceMiles = distance,
        Correlation = 0.85,
        IsRecent = true,
        Source = Domain.Cma.Models.CompSource.RentCast
    };
}
