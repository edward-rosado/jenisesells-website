using FluentAssertions;
using Moq;
using Moq.Protected;
using System.Net;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.Services.Research;

namespace RealEstateStar.Api.Tests.Features.Cma.Services.Research;

public class LeadResearchServiceTests
{
    private static Lead MakeLead() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-1234",
        Address = "123 Main St",
        City = "Old Bridge",
        State = "NJ",
        Zip = "08857",
        Timeline = "3-6 months",
        Beds = 3,
        Baths = 2,
        Sqft = 1800
    };

    private static Mock<HttpMessageHandler> MakeHandler(HttpStatusCode statusCode, string content = "{}")
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
        return handler;
    }

    // --- ResearchAsync ---

    [Fact]
    public async Task Research_ReturnsNonNullResult_WhenAllSourcesFail()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handler.Object);
        var service = new LeadResearchService(httpClient);

        var result = await service.ResearchAsync(MakeLead(), CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Research_ReturnsNonNullResult_WhenAllSourcesSucceed()
    {
        var handler = MakeHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler.Object);
        var service = new LeadResearchService(httpClient);

        var result = await service.ResearchAsync(MakeLead(), CancellationToken.None);

        result.Should().NotBeNull();
        result.Occupation.Should().BeNull();
        result.PurchasePrice.Should().BeNull();
    }

    [Fact]
    public async Task Research_CallsAllThreeEndpoints()
    {
        var urls = new List<string>();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                lock (urls) urls.Add(req.RequestUri!.Host);
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new LeadResearchService(httpClient);

        await service.ResearchAsync(MakeLead(), CancellationToken.None);

        urls.Should().Contain("api.publicrecords.example.com");
        urls.Should().Contain("api.linkedin.example.com");
        urls.Should().Contain("api.neighborhood.example.com");
    }

    [Fact]
    public async Task Research_ReturnsResult_WhenOneSourceFails()
    {
        var callCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var count = Interlocked.Increment(ref callCount);
                if (count == 1)
                    throw new HttpRequestException("First source failed");
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                };
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new LeadResearchService(httpClient);

        var result = await service.ResearchAsync(MakeLead(), CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Research_DoesNotCalculateEquity_WhenPurchasePriceMissing()
    {
        var handler = MakeHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler.Object);
        var service = new LeadResearchService(httpClient);

        var result = await service.ResearchAsync(MakeLead(), CancellationToken.None);

        result.EstimatedEquityLow.Should().BeNull();
        result.EstimatedEquityHigh.Should().BeNull();
    }

    // --- CalculateOwnershipDuration ---

    [Fact]
    public void CalculateOwnershipDuration_ReturnsYears()
    {
        var result = LeadResearchService.CalculateOwnershipDuration(new DateOnly(2019, 3, 15));

        result.Should().Contain("year");
    }

    [Fact]
    public void CalculateOwnershipDuration_ReturnsSingular_ForOneYear()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var oneYearAgo = today.AddYears(-1);

        var result = LeadResearchService.CalculateOwnershipDuration(oneYearAgo);

        result.Should().Be("1 year");
    }

    [Fact]
    public void CalculateOwnershipDuration_ReturnsPlural_ForMultipleYears()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fiveYearsAgo = today.AddYears(-5);

        var result = LeadResearchService.CalculateOwnershipDuration(fiveYearsAgo);

        result.Should().Be("5 years");
    }

    [Fact]
    public void CalculateOwnershipDuration_AdjustsDown_WhenAnniversaryNotReached()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Set purchase to next month of 3 years ago — anniversary hasn't happened yet this year
        var futureMonth = today.Month == 12 ? 1 : today.Month + 1;
        var yearOffset = today.Month == 12 ? 3 : 3;
        var purchaseDate = new DateOnly(today.Year - yearOffset, futureMonth, 1);
        var expected = today.Month == 12 ? "3 years" : "2 years";

        var result = LeadResearchService.CalculateOwnershipDuration(purchaseDate);

        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateOwnershipDuration_AdjustsDown_WhenSameMonthButDayNotReached()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Same month but future day — if today is the 1st, use day 28; otherwise use today.Day + a safe offset
        int futureDay;
        if (today.Day < 28)
            futureDay = 28;
        else
        {
            // If we're on the 28th or later, use month+1 instead
            var result2 = LeadResearchService.CalculateOwnershipDuration(
                new DateOnly(today.Year - 2, today.Month == 12 ? 1 : today.Month + 1, 1));
            result2.Should().Contain("year");
            return;
        }

        var purchaseDate = new DateOnly(today.Year - 2, today.Month, futureDay);
        var result = LeadResearchService.CalculateOwnershipDuration(purchaseDate);

        result.Should().Be("1 year");
    }

    // --- EstimateEquity ---

    [Fact]
    public void EstimateEquity_CalculatesFromPurchaseAndCurrentValue()
    {
        var (low, high) = LeadResearchService.EstimateEquity(350_000m, 450_000m);

        low.Should().BeGreaterThan(0);
        high.Should().BeGreaterThan(low);
    }

    [Fact]
    public void EstimateEquity_ReturnsSymmetricRange()
    {
        var (low, high) = LeadResearchService.EstimateEquity(200_000m, 400_000m);

        // equity = 400000 - (200000 * 0.65) = 270000
        // variance = 270000 * 0.15 = 40500
        low.Should().Be(229_500m);
        high.Should().Be(310_500m);
    }

    [Fact]
    public void EstimateEquity_CanReturnNegativeEquity()
    {
        var (low, _) = LeadResearchService.EstimateEquity(500_000m, 200_000m);

        low.Should().BeLessThan(0);
    }
}
