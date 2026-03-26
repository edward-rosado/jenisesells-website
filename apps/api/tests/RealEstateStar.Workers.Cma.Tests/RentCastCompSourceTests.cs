using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Workers.Cma;

namespace RealEstateStar.Workers.Cma.Tests;

public class RentCastCompSourceTests
{
    private static CompSearchRequest MakeRequest(
        string address = "123 Main St",
        string city = "Freehold",
        string state = "NJ",
        string zip = "07728",
        int? beds = 3,
        int? baths = 2,
        int? sqft = 1800) => new()
    {
        Address = address,
        City = city,
        State = state,
        Zip = zip,
        Beds = beds,
        Baths = baths,
        SqFt = sqft
    };

    private static RentCastComp MakeComp(
        string address = "456 Oak Ave, Freehold, NJ 07728",
        decimal price = 430_000m,
        int sqft = 1750,
        string? status = "Inactive",
        DateTimeOffset? listedDate = null,
        DateTimeOffset? removedDate = null,
        string? propertyType = "Single Family",
        int? bedrooms = 3,
        decimal? bathrooms = 2.0m,
        int? daysOnMarket = 22,
        double? distance = 0.4) => new()
    {
        FormattedAddress = address,
        Price = price,
        SquareFootage = sqft,
        Status = status,
        ListedDate = listedDate ?? new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero),
        RemovedDate = removedDate ?? new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero),
        PropertyType = propertyType,
        Bedrooms = bedrooms,
        Bathrooms = bathrooms,
        DaysOnMarket = daysOnMarket,
        Distance = distance
    };

    private static RentCastValuation MakeValuation(
        IReadOnlyList<RentCastComp>? comparables = null) => new()
    {
        Price = 450_000m,
        PriceRangeLow = 420_000m,
        PriceRangeHigh = 480_000m,
        Comparables = comparables ?? []
    };

    // ---------------------------------------------------------------------------
    // MapComps — pure static method, tested directly without mocking
    // ---------------------------------------------------------------------------

    [Fact]
    public void MapComps_ValidComps_MapsAllFields()
    {
        var request = MakeRequest();
        var comp = MakeComp(
            address: "456 Oak Ave, Freehold, NJ 07728",
            price: 430_000m,
            sqft: 1750,
            status: "Inactive",
            removedDate: new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero),
            propertyType: "Single Family",
            bedrooms: 3,
            bathrooms: 2.0m,
            daysOnMarket: 22,
            distance: 0.4);
        var valuation = MakeValuation([comp]);

        var result = RentCastCompSource.MapComps(valuation.Comparables, request,
            NullLogger.Instance);

        result.Should().HaveCount(1);
        var mapped = result[0];
        mapped.Address.Should().Be("456 Oak Ave, Freehold, NJ 07728");
        mapped.SalePrice.Should().Be(430_000m);
        mapped.SaleDate.Should().Be(new DateOnly(2025, 2, 1));
        mapped.Beds.Should().Be(3);
        mapped.Baths.Should().Be(2);
        mapped.Sqft.Should().Be(1750);
        mapped.DaysOnMarket.Should().Be(22);
        mapped.DistanceMiles.Should().BeApproximately(0.4, 0.001);
        mapped.Source.Should().Be(CompSource.RentCast);
    }

    [Fact]
    public void MapComps_InactiveStatus_UsesRemovedDateAsSaleDate()
    {
        var removed = new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var listed = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var comp = MakeComp(status: "Inactive", removedDate: removed, listedDate: listed);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result[0].SaleDate.Should().Be(new DateOnly(2025, 3, 15));
    }

    [Fact]
    public void MapComps_ActiveStatus_UsesListedDateAsSaleDate()
    {
        var listed = new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var comp = MakeComp(status: "Active", listedDate: listed, removedDate: null);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result[0].SaleDate.Should().Be(new DateOnly(2025, 1, 10));
    }

    [Fact]
    public void MapComps_NoBothDates_SkipsComp()
    {
        var comp = MakeComp(status: "Unknown") with { ListedDate = null, RemovedDate = null };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_ZeroPrice_SkipsComp()
    {
        var comp = MakeComp(price: 0m);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_NullPrice_SkipsComp()
    {
        var comp = MakeComp() with { Price = null };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_ZeroSquareFootage_SkipsComp()
    {
        var comp = MakeComp(sqft: 0);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_NullSquareFootage_SkipsComp()
    {
        var comp = MakeComp() with { SquareFootage = null };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_WhitespaceAddress_SkipsComp()
    {
        var comp = MakeComp(address: "   ");

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_NullBathrooms_DefaultsToZero()
    {
        var comp = MakeComp() with { Bathrooms = null };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Baths.Should().Be(0);
    }

    [Fact]
    public void MapComps_FractionalBathrooms_RoundsToInt()
    {
        var comp = MakeComp() with { Bathrooms = 2.5m };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Baths.Should().Be(3);
    }

    [Fact]
    public void MapComps_MultiFamilyPropertyType_FilteredWhenSubjectHasSqFt()
    {
        var request = MakeRequest(sqft: 1800); // non-null sqft signals single-family subject
        var comp = MakeComp(propertyType: "Multi Family");

        var result = RentCastCompSource.MapComps([comp], request, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Apartment")]
    [InlineData("Condominium")]
    [InlineData("Townhouse")]
    public void MapComps_ExcludedPropertyTypes_FilteredWhenSubjectHasSqFt(string propertyType)
    {
        var request = MakeRequest(sqft: 1800);
        var comp = MakeComp(propertyType: propertyType);

        var result = RentCastCompSource.MapComps([comp], request, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_NullPropertyType_NotFiltered()
    {
        var request = MakeRequest(sqft: 1800);
        var comp = MakeComp() with { PropertyType = null };

        var result = RentCastCompSource.MapComps([comp], request, NullLogger.Instance);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void MapComps_EmptyList_ReturnsEmpty()
    {
        var result = RentCastCompSource.MapComps([], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // FetchAsync — uses mock IRentCastClient
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FetchAsync_ClientReturnsNull_ReturnsEmpty()
    {
        var mockClient = new Mock<IRentCastClient>();
        mockClient
            .Setup(c => c.GetValuationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RentCastValuation?)null);

        var source = new RentCastCompSource(mockClient.Object,
            NullLogger<RentCastCompSource>.Instance);

        var result = await source.FetchAsync(MakeRequest(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAsync_ClientReturnsValuation_ReturnsMappedComps()
    {
        var comp = MakeComp();
        var valuation = MakeValuation([comp]);

        var mockClient = new Mock<IRentCastClient>();
        mockClient
            .Setup(c => c.GetValuationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(valuation);

        var source = new RentCastCompSource(mockClient.Object,
            NullLogger<RentCastCompSource>.Instance);

        var result = await source.FetchAsync(MakeRequest(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Source.Should().Be(CompSource.RentCast);
    }

    [Fact]
    public async Task FetchAsync_BuildsCorrectFullAddress()
    {
        string? capturedAddress = null;
        var mockClient = new Mock<IRentCastClient>();
        mockClient
            .Setup(c => c.GetValuationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((addr, _) => capturedAddress = addr)
            .ReturnsAsync((RentCastValuation?)null);

        var source = new RentCastCompSource(mockClient.Object,
            NullLogger<RentCastCompSource>.Instance);

        var request = MakeRequest("123 Main St", "Freehold", "NJ", "07728");
        await source.FetchAsync(request, CancellationToken.None);

        capturedAddress.Should().Be("123 Main St, Freehold, NJ 07728");
    }
}
