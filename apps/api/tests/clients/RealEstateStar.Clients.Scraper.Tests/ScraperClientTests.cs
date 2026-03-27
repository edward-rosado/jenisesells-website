using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.Scraper.Tests;

public class ScraperClientTests
{
    private static ScraperOptions DefaultOptions(bool renderJs = false, int circuitBreakerResetSeconds = 999999) => new()
    {
        ApiKey = "test-key",
        BaseUrl = "https://api.scraperapi.com",
        RenderJavaScript = renderJs,
        TimeoutSeconds = 10,
        CircuitBreakerResetSeconds = circuitBreakerResetSeconds
    };

    private static (ScraperClient client, MockHttpMessageHandler handler, Mock<IHttpClientFactory> factory) BuildClient(
        ScraperOptions? opts = null)
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("ScraperAPI")).Returns(httpClient);

        var options = Options.Create(opts ?? DefaultOptions());
        var client = new ScraperClient(factory.Object, options, NullLogger<ScraperClient>.Instance);
        return (client, handler, factory);
    }

    [Fact]
    public async Task FetchAsync_ReturnsHtml_WhenSuccessful()
    {
        var (client, handler, _) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>hello</html>")
        };

        var result = await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        result.Should().Be("<html>hello</html>");
    }

    [Fact]
    public async Task FetchAsync_ReturnsNull_WhenRateLimited()
    {
        var (client, handler, _) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        var result = await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_SetsIsAvailableFalse_WhenRateLimited()
    {
        var (client, handler, _) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        client.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task FetchAsync_ReturnsNull_WhenUnavailable()
    {
        var (client, handler, factory) = BuildClient();
        // First call triggers rate limit
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        // Second call should short-circuit without HTTP
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>should not get here</html>")
        };
        var result = await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        result.Should().BeNull();
        // Only 1 HTTP request should have been made (the first one that got rate-limited)
        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task FetchAsync_ReturnsNull_OnTimeout()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(new TimeoutThrowingHandler());
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("ScraperAPI")).Returns(httpClient);

        var options = Options.Create(DefaultOptions());
        var client = new ScraperClient(factory.Object, options, NullLogger<ScraperClient>.Instance);

        var result = await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        result.Should().BeNull();
        client.IsAvailable.Should().BeTrue(); // timeout does not disable
    }

    [Fact]
    public async Task FetchAsync_ReturnsNull_OnHttpError()
    {
        var httpClient = new HttpClient(new HttpErrorThrowingHandler());
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("ScraperAPI")).Returns(httpClient);

        var options = Options.Create(DefaultOptions());
        var client = new ScraperClient(factory.Object, options, NullLogger<ScraperClient>.Instance);

        var result = await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        result.Should().BeNull();
        client.IsAvailable.Should().BeTrue(); // HTTP error does not disable
    }

    [Fact]
    public async Task FetchAsync_UsesConfiguredBaseUrl()
    {
        var opts = new ScraperOptions
        {
            ApiKey = "my-key",
            BaseUrl = "https://custom.scraper.io",
            RenderJavaScript = false,
            TimeoutSeconds = 10
        };
        var (client, handler, _) = BuildClient(opts);
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html/>")
        };

        await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        handler.LastRequest!.RequestUri!.ToString().Should().StartWith("https://custom.scraper.io");
    }

    [Fact]
    public async Task FetchAsync_AppendsRenderTrue_WhenRenderJavaScriptEnabled()
    {
        var opts = DefaultOptions(renderJs: true);
        var (client, handler, _) = BuildClient(opts);
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html/>")
        };

        await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        handler.LastRequest!.RequestUri!.ToString().Should().Contain("render=true");
    }

    [Fact]
    public async Task FetchAsync_OmitsRender_WhenRenderJavaScriptDisabled()
    {
        var opts = DefaultOptions(renderJs: false);
        var (client, handler, _) = BuildClient(opts);
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html/>")
        };

        await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        handler.LastRequest!.RequestUri!.ToString().Should().NotContain("render=true");
    }

    [Fact]
    public async Task IsAvailable_ReturnsTrueAfterReset()
    {
        // Arrange: use 0s reset so the cooldown expires immediately
        var opts = DefaultOptions(circuitBreakerResetSeconds: 0);
        var (client, handler, _) = BuildClient(opts);
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        // Act: with 0s reset, IsAvailable should immediately flip back to true
        client.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task FetchAsync_ResetsAfterCooldown()
    {
        // Arrange: use 0s reset so the cooldown expires immediately
        var opts = DefaultOptions(circuitBreakerResetSeconds: 0);
        var handler = new MockHttpMessageHandler();

        // Use a fresh HttpClient for each factory call so Timeout can be set again
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("ScraperAPI"))
               .Returns(() => new HttpClient(handler));

        var options = Options.Create(opts);
        var client = new ScraperClient(factory.Object, options, NullLogger<ScraperClient>.Instance);

        // First call triggers 429
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        // Second call after 0s cooldown — should make a real HTTP request (not short-circuit)
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>back online</html>")
        };
        var result = await client.FetchAsync("https://example.com", "source1", "agent1", CancellationToken.None);

        result.Should().Be("<html>back online</html>");
        // Two HTTP requests: 429 + the reset request
        handler.Requests.Should().HaveCount(2);
    }

    // Helper handlers for exception scenarios
    private sealed class TimeoutThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new TaskCanceledException("Simulated timeout");
    }

    private sealed class HttpErrorThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new HttpRequestException("Simulated HTTP error");
    }
}
