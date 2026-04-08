using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace RealEstateStar.Clients.Zillow.Tests;

public class ZillowReviewsClientTests
{
    private const string AgentId = "test-agent";
    private const string AgentEmail = "agent@example.com";

    private static ZillowReviewsClient BuildClient(
        Dictionary<string, HttpResponseMessage>? urlResponses = null,
        string apiToken = "test-token")
    {
        var handler = new MockZillowHttpHandler(urlResponses ?? []);
        var httpClient = new HttpClient(handler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory
            .Setup(f => f.CreateClient("ZillowAPI"))
            .Returns(httpClient);

        var options = Options.Create(new ZillowOptions
        {
            ApiToken = apiToken,
            BaseUrl = "https://api.bridgedataoutput.com/api/v2/OData/reviews",
            TimeoutSeconds = 15,
        });

        return new ZillowReviewsClient(
            mockFactory.Object,
            options,
            NullLogger<ZillowReviewsClient>.Instance);
    }

    // ──────────────────────────────────────────────────────────
    // IsAvailable
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void IsAvailable_True_WhenTokenConfigured()
    {
        var client = BuildClient(apiToken: "my-token");
        client.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_False_WhenTokenEmpty()
    {
        var client = BuildClient(apiToken: "");
        client.IsAvailable.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────
    // GetReviewsByEmailAsync
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReviewsByEmailAsync_ReturnsReviews_WhenApiSucceeds()
    {
        var json = """
        {
            "value": [{
                "RevieweeKey": "abc123",
                "Reviews": [
                    {
                        "ReviewerFullName": "John Doe",
                        "Rating": 5,
                        "Description": "Excellent agent, very professional!",
                        "ReviewDate": "2025-06-15T00:00:00Z"
                    },
                    {
                        "ReviewerFullName": "Jane Smith",
                        "Rating": 4,
                        "Description": "Great experience overall.",
                        "ReviewDate": "2025-05-20T00:00:00Z"
                    }
                ]
            }]
        }
        """;

        var responses = new Dictionary<string, HttpResponseMessage>
        {
            ["bridgedataoutput.com"] = new(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) }
        };

        var client = BuildClient(responses);
        var result = await client.GetReviewsByEmailAsync(AgentEmail, AgentId, CancellationToken.None);

        result.TotalReviewCount.Should().Be(2);
        result.Reviews.Should().HaveCount(2);
        result.Reviews[0].Text.Should().Be("Excellent agent, very professional!");
        result.Reviews[0].Rating.Should().Be(5);
        result.Reviews[0].Reviewer.Should().Be("John Doe");
        result.Reviews[0].Source.Should().Be("Zillow");
        result.AverageRating.Should().Be(4.5);
        result.RevieweeKey.Should().Be("abc123");
    }

    [Fact]
    public async Task GetReviewsByEmailAsync_ReturnsEmpty_WhenNoReviewee()
    {
        var json = """{ "value": [] }""";
        var responses = new Dictionary<string, HttpResponseMessage>
        {
            ["bridgedataoutput.com"] = new(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) }
        };

        var client = BuildClient(responses);
        var result = await client.GetReviewsByEmailAsync(AgentEmail, AgentId, CancellationToken.None);

        result.TotalReviewCount.Should().Be(0);
        result.Reviews.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReviewsByEmailAsync_ReturnsEmpty_WhenUnauthorized()
    {
        var responses = new Dictionary<string, HttpResponseMessage>
        {
            ["bridgedataoutput.com"] = new(System.Net.HttpStatusCode.Unauthorized)
        };

        var client = BuildClient(responses);
        var result = await client.GetReviewsByEmailAsync(AgentEmail, AgentId, CancellationToken.None);

        result.TotalReviewCount.Should().Be(0);
    }

    [Fact]
    public async Task GetReviewsByEmailAsync_ReturnsEmpty_WhenRateLimited()
    {
        var responses = new Dictionary<string, HttpResponseMessage>
        {
            ["bridgedataoutput.com"] = new(System.Net.HttpStatusCode.TooManyRequests)
        };

        var client = BuildClient(responses);
        var result = await client.GetReviewsByEmailAsync(AgentEmail, AgentId, CancellationToken.None);

        result.TotalReviewCount.Should().Be(0);
    }

    [Fact]
    public async Task GetReviewsByEmailAsync_ReturnsEmpty_WhenApiTokenMissing()
    {
        var client = BuildClient(apiToken: "");
        var result = await client.GetReviewsByEmailAsync(AgentEmail, AgentId, CancellationToken.None);

        result.TotalReviewCount.Should().Be(0);
    }

    // ──────────────────────────────────────────────────────────
    // ParseODataResponse
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseODataResponse_HandlesAnonymousReviewer()
    {
        var json = """
        {
            "value": [{
                "RevieweeKey": "key1",
                "Reviews": [{
                    "Description": "No name given",
                    "Rating": 3
                }]
            }]
        }
        """;

        var result = ZillowReviewsClient.ParseODataResponse(json, AgentId);

        result.Reviews.Should().ContainSingle();
        result.Reviews[0].Reviewer.Should().Be("Anonymous");
    }

    [Fact]
    public void ParseODataResponse_SkipsEmptyDescriptions()
    {
        var json = """
        {
            "value": [{
                "RevieweeKey": "key1",
                "Reviews": [
                    { "Description": "", "Rating": 5, "ReviewerFullName": "User1" },
                    { "Description": "Valid review", "Rating": 4, "ReviewerFullName": "User2" }
                ]
            }]
        }
        """;

        var result = ZillowReviewsClient.ParseODataResponse(json, AgentId);

        result.Reviews.Should().ContainSingle();
        result.Reviews[0].Text.Should().Be("Valid review");
    }

    [Fact]
    public void ParseODataResponse_HandlesMalformedJson()
    {
        var result = ZillowReviewsClient.ParseODataResponse("NOT JSON", AgentId);

        result.TotalReviewCount.Should().Be(0);
        result.Reviews.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────
    // EscapeODataString
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void EscapeODataString_DoublesQuotes()
    {
        var result = ZillowReviewsClient.EscapeODataString("O'Brien");
        result.Should().Be("O''Brien");
    }

    [Fact]
    public void EscapeODataString_NoChange_WhenNoQuotes()
    {
        var result = ZillowReviewsClient.EscapeODataString("Jane Doe");
        result.Should().Be("Jane Doe");
    }
}

/// <summary>
/// Test HTTP handler that returns configured responses per URL substring.
/// </summary>
internal sealed class MockZillowHttpHandler(Dictionary<string, HttpResponseMessage> responses)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? string.Empty;

        foreach (var (prefix, response) in responses)
        {
            if (url.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(response);
        }

        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }
}
