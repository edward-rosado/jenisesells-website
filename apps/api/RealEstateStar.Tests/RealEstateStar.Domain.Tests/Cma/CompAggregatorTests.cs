using Microsoft.Extensions.Logging.Abstractions;

namespace RealEstateStar.Domain.Tests.Cma;

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

        result.Should().HaveCount(6);
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
    public void Deduplicate_RemovesDuplicatesByAddressAndDate_KeepsLowerEnumValue()
    {
        // Same address + date from two sources — Zillow (0) wins over Redfin (2)
        var saleDate = new DateOnly(2025, 3, 1);
        var zillowComp = MakeComp("123 Main St", saleDate, CompSource.Zillow);
        var redfinComp = MakeComp("123 Main St", saleDate, CompSource.Redfin);

        var result = CompAggregator.Deduplicate([zillowComp, redfinComp]);

        result.Should().HaveCount(1);
        result[0].Source.Should().Be(CompSource.Zillow);
    }

    [Fact]
    public void Deduplicate_KeepsDifferentDates()
    {
        var comp1 = MakeComp("123 Main St", new DateOnly(2025, 1, 1));
        var comp2 = MakeComp("123 Main St", new DateOnly(2025, 6, 1));

        var result = CompAggregator.Deduplicate([comp1, comp2]);

        result.Should().HaveCount(2);
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
}
