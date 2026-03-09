using FluentAssertions;
using Moq;
using Moq.Protected;
using System.Net;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services.Research;

namespace RealEstateStar.Api.Tests.Services.Research;

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

        var result = await service.ResearchAsync(MakeLead());

        result.Should().NotBeNull();
    }

    [Fact]
    public void CalculateOwnershipDuration_ReturnsYears()
    {
        var result = LeadResearchService.CalculateOwnershipDuration(new DateOnly(2019, 3, 15));

        result.Should().Contain("year");
    }

    [Fact]
    public void EstimateEquity_CalculatesFromPurchaseAndCurrentValue()
    {
        var (low, high) = LeadResearchService.EstimateEquity(350_000m, 450_000m);

        low.Should().BeGreaterThan(0);
        high.Should().BeGreaterThan(low);
    }
}
