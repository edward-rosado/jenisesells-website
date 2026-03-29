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
}
