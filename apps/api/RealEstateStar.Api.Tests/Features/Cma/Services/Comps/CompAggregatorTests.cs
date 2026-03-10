using FluentAssertions;
using Moq;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.Services.Comps;

namespace RealEstateStar.Api.Tests.Features.Cma.Services.Comps;

public class CompAggregatorTests
{
    private static readonly CompSearchRequest DefaultRequest = new()
    {
        Address = "123 Main St",
        City = "Anytown",
        State = "NJ",
        Zip = "08901",
        Beds = 3,
        Baths = 2,
        SqFt = 1500
    };

    private static Comp MakeComp(
        string address = "123 Main St",
        decimal salePrice = 350_000m,
        DateOnly? saleDate = null,
        CompSource source = CompSource.Zillow) => new()
    {
        Address = address,
        SalePrice = salePrice,
        SaleDate = saleDate ?? new DateOnly(2026, 1, 15),
        Beds = 3,
        Baths = 2,
        Sqft = 1500,
        DistanceMiles = 0.5,
        Source = source
    };

    [Fact]
    public async Task Aggregate_DeduplicatesByAddressAndSaleDate()
    {
        var comp = MakeComp(source: CompSource.Zillow);
        var duplicate = MakeComp(source: CompSource.Redfin);

        var source1 = new Mock<ICompSource>();
        source1.Setup(s => s.Name).Returns("Zillow");
        source1.Setup(s => s.FetchAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([comp]);

        var source2 = new Mock<ICompSource>();
        source2.Setup(s => s.Name).Returns("Redfin");
        source2.Setup(s => s.FetchAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate]);

        var aggregator = new CompAggregator([source1.Object, source2.Object]);

        var results = await aggregator.FetchCompsAsync(DefaultRequest, CancellationToken.None);

        results.Should().HaveCount(1);
    }

    [Fact]
    public async Task Aggregate_MlsWinsOnConflict()
    {
        var mlsComp = MakeComp(salePrice: 360_000m, source: CompSource.Mls);
        var zillowComp = MakeComp(salePrice: 350_000m, source: CompSource.Zillow);

        var mlsSource = new Mock<ICompSource>();
        mlsSource.Setup(s => s.Name).Returns("MLS");
        mlsSource.Setup(s => s.FetchAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([mlsComp]);

        var zillowSource = new Mock<ICompSource>();
        zillowSource.Setup(s => s.Name).Returns("Zillow");
        zillowSource.Setup(s => s.FetchAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([zillowComp]);

        var aggregator = new CompAggregator([mlsSource.Object, zillowSource.Object]);

        var results = await aggregator.FetchCompsAsync(DefaultRequest, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].SalePrice.Should().Be(360_000m);
        results[0].Source.Should().Be(CompSource.Mls);
    }

    [Fact]
    public async Task Aggregate_ContinuesIfOneSourceFails()
    {
        var comp = MakeComp(source: CompSource.Redfin);

        var failingSource = new Mock<ICompSource>();
        failingSource.Setup(s => s.Name).Returns("Zillow");
        failingSource.Setup(s => s.FetchAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var workingSource = new Mock<ICompSource>();
        workingSource.Setup(s => s.Name).Returns("Redfin");
        workingSource.Setup(s => s.FetchAsync(It.IsAny<CompSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([comp]);

        var aggregator = new CompAggregator([failingSource.Object, workingSource.Object]);

        var results = await aggregator.FetchCompsAsync(DefaultRequest, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].Source.Should().Be(CompSource.Redfin);
    }
}
