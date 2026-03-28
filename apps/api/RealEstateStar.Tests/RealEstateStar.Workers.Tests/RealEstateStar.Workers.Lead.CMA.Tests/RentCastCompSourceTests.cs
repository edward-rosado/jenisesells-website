using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;

namespace RealEstateStar.Workers.Lead.CMA.Tests;

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
            null, NullLogger.Instance);

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

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), null, NullLogger.Instance);

        result[0].SaleDate.Should().Be(new DateOnly(2025, 3, 15));
    }

    [Fact]
    public void MapComps_ActiveStatus_UsesListedDateAsSaleDate()
    {
        var listed = new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var comp = MakeComp(status: "Active", listedDate: listed, removedDate: null);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), null, NullLogger.Instance);

        result[0].SaleDate.Should().Be(new DateOnly(2025, 1, 10));
    }

    [Fact]
    public void MapComps_NoBothDates_SkipsComp()
    {
        var comp = MakeComp(status: "Unknown") with { ListedDate = null, RemovedDate = null };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), null, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_ZeroPrice_SkipsComp()
    {
        var comp = MakeComp(price: 0m);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), null, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_NullPrice_SkipsComp()
    {
        var comp = MakeComp() with { Price = null };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), null, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_ZeroSquareFootage_SkipsComp()
    {
        var comp = MakeComp(sqft: 0);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), null, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_NullSquareFootage_SkipsComp()
    {
        var comp = MakeComp() with { SquareFootage = null };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), null, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_WhitespaceAddress_SkipsComp()
    {
        var comp = MakeComp(address: "   ");

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), null, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_NullBathrooms_DefaultsToZero()
    {
        var comp = MakeComp() with { Bathrooms = null };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), null, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Baths.Should().Be(0);
    }

    [Fact]
    public void MapComps_FractionalBathrooms_RoundsToInt()
    {
        var comp = MakeComp() with { Bathrooms = 2.5m };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), null, NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Baths.Should().Be(3);
    }

    [Fact]
    public void MapComps_MultiFamilyPropertyType_FilteredWhenSubjectIsSingleFamily()
    {
        var comp = MakeComp(propertyType: "Multi Family");

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), "Single Family", NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Apartment")]
    [InlineData("Condominium")]
    [InlineData("Townhouse")]
    public void MapComps_ExcludedPropertyTypes_FilteredWhenSubjectIsSingleFamily(string propertyType)
    {
        var comp = MakeComp(propertyType: propertyType);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), "Single Family", NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_NullPropertyType_NotFiltered()
    {
        var request = MakeRequest(sqft: 1800);
        var comp = MakeComp() with { PropertyType = null };

        var result = RentCastCompSource.MapComps([comp], request, null, NullLogger.Instance);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void MapComps_EmptyList_ReturnsEmpty()
    {
        var result = RentCastCompSource.MapComps([], MakeRequest(), null, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // MapComps — tiered comp selection (5-comp target, 6-month recency preference)
    // ---------------------------------------------------------------------------

    [Fact]
    public void MapComps_FiveOrMoreRecentComps_TakesOnlyRecent()
    {
        var today = new DateTimeOffset(2026, 3, 26, 0, 0, 0, TimeSpan.Zero);
        var recentDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); // within 6 months

        // 7 recent comps, correlations 0.9 down to 0.3
        var comps = Enumerable.Range(1, 7)
            .Select(i => MakeComp(
                address: $"{i} Recent St, Freehold, NJ 07728",
                removedDate: recentDate) with
            { Correlation = 1.0 - (i * 0.1) })
            .ToList();

        var result = RentCastCompSource.MapComps(comps, MakeRequest(), null, NullLogger.Instance, today);

        result.Should().HaveCount(5);
        result.Should().AllSatisfy(c => c.IsRecent.Should().BeTrue());
    }

    [Fact]
    public void MapComps_FewerThanFiveRecent_BackfillsWithOlder()
    {
        var today = new DateTimeOffset(2026, 3, 26, 0, 0, 0, TimeSpan.Zero);
        var recentDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);  // within 6 months
        var oldDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);    // > 6 months ago

        var recentComps = Enumerable.Range(1, 2)
            .Select(i => MakeComp(
                address: $"{i} Recent St, Freehold, NJ 07728",
                removedDate: recentDate) with
            { Correlation = 0.9 - (i * 0.05) })
            .ToList();

        var olderComps = Enumerable.Range(1, 5)
            .Select(i => MakeComp(
                address: $"{i} Old St, Freehold, NJ 07728",
                removedDate: oldDate) with
            { Correlation = 0.7 - (i * 0.05) })
            .ToList();

        var result = RentCastCompSource.MapComps(
            [.. recentComps, .. olderComps], MakeRequest(), null, NullLogger.Instance, today);

        result.Should().HaveCount(5);
        result.Count(c => c.IsRecent).Should().Be(2);
        result.Count(c => !c.IsRecent).Should().Be(3);
    }

    [Fact]
    public void MapComps_NoRecentComps_UsesOlderSortedByCorrelation()
    {
        var today = new DateTimeOffset(2026, 3, 26, 0, 0, 0, TimeSpan.Zero);
        var oldDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // 7 older comps with varied correlations
        var comps = Enumerable.Range(1, 7)
            .Select(i => MakeComp(
                address: $"{i} Old St, Freehold, NJ 07728",
                removedDate: oldDate) with
            { Correlation = i * 0.1 })
            .ToList();

        var result = RentCastCompSource.MapComps(comps, MakeRequest(), null, NullLogger.Instance, today);

        result.Should().HaveCount(5);
        result.Should().AllSatisfy(c => c.IsRecent.Should().BeFalse());
    }

    [Fact]
    public void MapComps_FewerThanFiveTotal_ReturnsAll()
    {
        var today = new DateTimeOffset(2026, 3, 26, 0, 0, 0, TimeSpan.Zero);
        var recentDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var comps = Enumerable.Range(1, 3)
            .Select(i => MakeComp(
                address: $"{i} Short St, Freehold, NJ 07728",
                removedDate: recentDate))
            .ToList();

        var result = RentCastCompSource.MapComps(comps, MakeRequest(), null, NullLogger.Instance, today);

        result.Should().HaveCount(3);
    }

    [Fact]
    public void MapComps_CorrelationSortOrder_HighestFirst()
    {
        var today = new DateTimeOffset(2026, 3, 26, 0, 0, 0, TimeSpan.Zero);
        var recentDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // 6 recent comps with correlations in ascending order
        var comps = new[]
        {
            MakeComp(address: "1 Low St, Freehold, NJ 07728", removedDate: recentDate) with { Correlation = 0.3 },
            MakeComp(address: "2 Med St, Freehold, NJ 07728", removedDate: recentDate) with { Correlation = 0.7 },
            MakeComp(address: "3 High St, Freehold, NJ 07728", removedDate: recentDate) with { Correlation = 0.95 },
            MakeComp(address: "4 MedHi St, Freehold, NJ 07728", removedDate: recentDate) with { Correlation = 0.85 },
            MakeComp(address: "5 MedLo St, Freehold, NJ 07728", removedDate: recentDate) with { Correlation = 0.5 },
            MakeComp(address: "6 Dropped St, Freehold, NJ 07728", removedDate: recentDate) with { Correlation = 0.2 },
        };

        var result = RentCastCompSource.MapComps(comps, MakeRequest(), null, NullLogger.Instance, today);

        result.Should().HaveCount(5);
        result[0].Correlation.Should().Be(0.95);
        result[1].Correlation.Should().Be(0.85);
        result[2].Correlation.Should().Be(0.7);
        result[3].Correlation.Should().Be(0.5);
        result[4].Correlation.Should().Be(0.3);
        // 0.2 (lowest) should be dropped
        result.Should().NotContain(c => c.Correlation == 0.2);
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

    [Fact]
    public async Task FetchAsync_StoresLastValuation_AfterSuccessfulFetch()
    {
        var valuation = MakeValuation([MakeComp()]);
        var mockClient = new Mock<IRentCastClient>();
        mockClient
            .Setup(c => c.GetValuationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(valuation);

        var source = new RentCastCompSource(mockClient.Object,
            NullLogger<RentCastCompSource>.Instance);

        await source.FetchAsync(MakeRequest(), CancellationToken.None);

        source.LastValuation.Should().BeSameAs(valuation);
    }

    [Fact]
    public async Task FetchAsync_StoresNullLastValuation_WhenClientReturnsNull()
    {
        var mockClient = new Mock<IRentCastClient>();
        mockClient
            .Setup(c => c.GetValuationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RentCastValuation?)null);

        var source = new RentCastCompSource(mockClient.Object,
            NullLogger<RentCastCompSource>.Instance);

        await source.FetchAsync(MakeRequest(), CancellationToken.None);

        source.LastValuation.Should().BeNull();
    }
}
