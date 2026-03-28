using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.RentCast.Tests;

public class RentCastClientTests
{
    private static RentCastOptions DefaultOptions() => new()
    {
        ApiKey = "test-api-key",
        BaseUrl = "https://api.rentcast.io/v1/avm/value",
        TimeoutSeconds = 30,
        MonthlyLimitWarningPercent = 80
    };

    private static (RentCastClient client, MockHttpMessageHandler handler) BuildClient(
        RentCastOptions? opts = null)
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("RentCast")).Returns(httpClient);

        var options = Options.Create(opts ?? DefaultOptions());
        var client = new RentCastClient(factory.Object, options,
            NullLogger<RentCastClient>.Instance);
        return (client, handler);
    }

    private static string ValidResponseJson(int compCount = 2) =>
        $$"""
        {
          "price": 450000,
          "priceRangeLow": 420000,
          "priceRangeHigh": 480000,
          "comparables": [
            {{string.Join(",\n    ", Enumerable.Range(0, compCount).Select(i => $$"""
            {
              "formattedAddress": "{{i}} Oak Ave, Freehold, NJ 07728",
              "propertyType": "Single Family",
              "bedrooms": 3,
              "bathrooms": 2.0,
              "squareFootage": 1800,
              "price": 430000,
              "listedDate": "2025-01-10T00:00:00Z",
              "removedDate": "2025-02-01T00:00:00Z",
              "daysOnMarket": 22,
              "distance": 0.4,
              "correlation": 0.92,
              "status": "Inactive"
            }
            """))}}
          ]
        }
        """;

    [Fact]
    public async Task GetValuationAsync_SuccessResponse_ReturnsValuation()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidResponseJson(2))
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Price.Should().Be(450000m);
        result.PriceRangeLow.Should().Be(420000m);
        result.PriceRangeHigh.Should().Be(480000m);
        result.Comparables.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetValuationAsync_ApiError_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal Server Error")
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValuationAsync_Unauthorized_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"message\": \"Invalid API key\"}")
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValuationAsync_Timeout_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ExceptionToThrow = new TaskCanceledException("Request timed out");

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValuationAsync_NetworkError_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ExceptionToThrow = new HttpRequestException("Connection refused");

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValuationAsync_EmptyComparables_ReturnsValuationWithEmptyList()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "price": 450000,
                  "priceRangeLow": 420000,
                  "priceRangeHigh": 480000,
                  "comparables": []
                }
                """)
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Comparables.Should().BeEmpty();
    }

    [Fact]
    public async Task GetValuationAsync_NullComparables_ReturnsValuationWithEmptyList()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "price": 450000,
                  "priceRangeLow": 420000,
                  "priceRangeHigh": 480000,
                  "comparables": null
                }
                """)
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Comparables.Should().BeEmpty();
    }

    [Fact]
    public async Task GetValuationAsync_BuildsCorrectRequestUrl()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidResponseJson(0))
        };

        await client.GetValuationAsync("123 Main St, Freehold, NJ 07728", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        var uri = handler.LastRequest!.RequestUri!;
        // Uri.ToString() returns the URI with spaces decoded but other chars percent-encoded
        // Verify the address parameter is present and the full address is encoded in the query
        uri.Query.Should().Contain("address=");
        // Use Uri.UnescapeDataString to compare the decoded query value
        var decodedQuery = Uri.UnescapeDataString(uri.Query);
        decodedQuery.Should().Contain("123 Main St, Freehold, NJ 07728");
    }

    [Fact]
    public async Task GetValuationAsync_SetsApiKeyHeader()
    {
        var opts = new RentCastOptions
        {
            ApiKey = "my-secret-key",
            BaseUrl = "https://api.rentcast.io/v1/avm/value",
            TimeoutSeconds = 30,
            MonthlyLimitWarningPercent = 80
        };
        var (client, handler) = BuildClient(opts);
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidResponseJson(0))
        };

        await client.GetValuationAsync("123 Main St, Freehold, NJ 07728", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.TryGetValues("X-Api-Key", out var values).Should().BeTrue();
        values!.Single().Should().Be("my-secret-key");
    }

    [Fact]
    public async Task GetValuationAsync_MapsAllCompFields()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "price": 450000,
                  "priceRangeLow": 420000,
                  "priceRangeHigh": 480000,
                  "comparables": [
                    {
                      "formattedAddress": "456 Oak Ave, Freehold, NJ 07728",
                      "propertyType": "Single Family",
                      "bedrooms": 4,
                      "bathrooms": 2.5,
                      "squareFootage": 2100,
                      "price": 510000,
                      "listedDate": "2025-01-05T00:00:00Z",
                      "removedDate": "2025-01-20T00:00:00Z",
                      "daysOnMarket": 15,
                      "distance": 0.6,
                      "correlation": 0.88,
                      "status": "Inactive"
                    }
                  ]
                }
                """)
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        var comp = result!.Comparables.Single();
        comp.FormattedAddress.Should().Be("456 Oak Ave, Freehold, NJ 07728");
        comp.PropertyType.Should().Be("Single Family");
        comp.Bedrooms.Should().Be(4);
        comp.Bathrooms.Should().Be(2.5m);
        comp.SquareFootage.Should().Be(2100);
        comp.Price.Should().Be(510000m);
        comp.DaysOnMarket.Should().Be(15);
        comp.Distance.Should().BeApproximately(0.6, 0.001);
        comp.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task GetValuationAsync_InvalidJson_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not valid json {{{")
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValuationAsync_MapsSubjectProperty_WhenPresent()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "price": 450000,
                  "priceRangeLow": 420000,
                  "priceRangeHigh": 480000,
                  "comparables": [],
                  "subjectProperty": {
                    "formattedAddress": "123 Main St, Freehold, NJ 07728",
                    "propertyType": "Single Family",
                    "bedrooms": 4,
                    "bathrooms": 2.5,
                    "squareFootage": 2100,
                    "yearBuilt": 1998,
                    "lotSize": 8500
                  }
                }
                """)
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.SubjectProperty.Should().NotBeNull();
        result.SubjectProperty!.FormattedAddress.Should().Be("123 Main St, Freehold, NJ 07728");
        result.SubjectProperty.PropertyType.Should().Be("Single Family");
        result.SubjectProperty.Bedrooms.Should().Be(4);
        result.SubjectProperty.Bathrooms.Should().Be(2.5m);
        result.SubjectProperty.SquareFootage.Should().Be(2100);
        result.SubjectProperty.YearBuilt.Should().Be(1998);
        result.SubjectProperty.LotSize.Should().Be(8500);
    }

    [Fact]
    public async Task GetValuationAsync_SubjectPropertyIsNull_WhenNotInResponse()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidResponseJson(0))
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.SubjectProperty.Should().BeNull();
    }
}
