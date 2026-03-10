using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class ProfileScraperTests
{
    private static HttpClient CreateMockHttpClient(HttpStatusCode status, string content)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(content),
            });
        return new HttpClient(handler.Object);
    }

    [Fact]
    public async Task ScrapeAsync_FetchFailure_ReturnsNull()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var client = new HttpClient(handler.Object);
        var scraper = new ProfileScraperService(client, "test-key", NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/nobody", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_EmptyPage_ReturnsNull()
    {
        var client = CreateMockHttpClient(HttpStatusCode.OK, "<html><body></body></html>");
        var scraper = new ProfileScraperService(client, "test-key", NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://example.com/empty", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_ValidPage_FallsBackOnClaudeFailure()
    {
        // When Claude API call fails (bad key), scraper returns partial profile as fallback
        var html = "<html><body><h1>Jane Doe</h1><p>RE/MAX agent serving New Jersey with 15 years experience and 200 homes sold in the tri-state area.</p></body></html>";
        var client = CreateMockHttpClient(HttpStatusCode.OK, html);
        var scraper = new ProfileScraperService(client, "invalid-key", NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/jane-doe", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("zillow.com", result!.Bio);
    }
}
