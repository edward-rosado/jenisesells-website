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
    private static IHttpClientFactory CreateMockFactory(HttpStatusCode status, string content)
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
        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return factory.Object;
    }

    private static IHttpClientFactory CreateThrowingFactory()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return factory.Object;
    }

    private static IDnsResolver CreateMockDnsResolver(params IPAddress[] addresses)
    {
        var mock = new Mock<IDnsResolver>();
        mock.Setup(d => d.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(addresses);
        return mock.Object;
    }

    private static IDnsResolver CreateThrowingDnsResolver()
    {
        var mock = new Mock<IDnsResolver>();
        mock.Setup(d => d.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Sockets.SocketException(11001));
        return mock.Object;
    }

    private static readonly IPAddress PublicIp = IPAddress.Parse("104.18.32.7");

    [Fact]
    public async Task ScrapeAsync_FetchFailure_ReturnsNull()
    {
        var factory = CreateThrowingFactory();
        var dns = CreateMockDnsResolver(PublicIp);
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/nobody", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_EmptyPage_ReturnsNull()
    {
        var factory = CreateMockFactory(HttpStatusCode.OK, "<html><body></body></html>");
        var dns = CreateMockDnsResolver(PublicIp);
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/empty", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_ValidPage_FallsBackOnClaudeFailure()
    {
        // When Claude API call fails (bad key), scraper returns partial profile as fallback
        var html = "<html><body><h1>Jane Doe</h1><p>RE/MAX agent serving New Jersey with 15 years experience and 200 homes sold in the tri-state area.</p></body></html>";
        var factory = CreateMockFactory(HttpStatusCode.OK, html);
        var dns = CreateMockDnsResolver(PublicIp);
        var scraper = new ProfileScraperService(factory, "invalid-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/jane-doe", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("zillow.com", result!.Bio);
    }

    // --- DNS rebinding prevention tests ---

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToLoopback_ReturnsNull()
    {
        var factory = CreateMockFactory(HttpStatusCode.OK, "should not reach");
        var dns = CreateMockDnsResolver(IPAddress.Parse("127.0.0.1"));
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToPrivate10_ReturnsNull()
    {
        var factory = CreateMockFactory(HttpStatusCode.OK, "should not reach");
        var dns = CreateMockDnsResolver(IPAddress.Parse("10.0.0.1"));
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToLinkLocal169_ReturnsNull()
    {
        var factory = CreateMockFactory(HttpStatusCode.OK, "should not reach");
        var dns = CreateMockDnsResolver(IPAddress.Parse("169.254.169.254"));
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToPrivate172_ReturnsNull()
    {
        var factory = CreateMockFactory(HttpStatusCode.OK, "should not reach");
        var dns = CreateMockDnsResolver(IPAddress.Parse("172.16.0.1"));
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToPrivate192_ReturnsNull()
    {
        var factory = CreateMockFactory(HttpStatusCode.OK, "should not reach");
        var dns = CreateMockDnsResolver(IPAddress.Parse("192.168.1.1"));
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToIpv6Loopback_ReturnsNull()
    {
        var factory = CreateMockFactory(HttpStatusCode.OK, "should not reach");
        var dns = CreateMockDnsResolver(IPAddress.IPv6Loopback); // ::1
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToIpv6UniqueLocal_ReturnsNull()
    {
        var factory = CreateMockFactory(HttpStatusCode.OK, "should not reach");
        var dns = CreateMockDnsResolver(IPAddress.Parse("fd00::1"));
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToIpv6LinkLocal_ReturnsNull()
    {
        var factory = CreateMockFactory(HttpStatusCode.OK, "should not reach");
        var dns = CreateMockDnsResolver(IPAddress.Parse("fe80::1"));
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToIpv4MappedPrivate_ReturnsNull()
    {
        var factory = CreateMockFactory(HttpStatusCode.OK, "should not reach");
        // ::ffff:10.0.0.1 — IPv4-mapped IPv6 with private IPv4
        var dns = CreateMockDnsResolver(IPAddress.Parse("10.0.0.1").MapToIPv6());
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToPublicIp_Proceeds()
    {
        // Valid page but Claude call fails — should still reach the fallback, proving DNS check passed
        var html = "<html><body><h1>Jane Doe</h1><p>RE/MAX agent serving New Jersey with 15 years experience and 200 homes sold in the tri-state area.</p></body></html>";
        var factory = CreateMockFactory(HttpStatusCode.OK, html);
        var dns = CreateMockDnsResolver(PublicIp);
        var scraper = new ProfileScraperService(factory, "invalid-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToMixedPublicAndPrivate_ReturnsNull()
    {
        // If any resolved address is private, block the request
        var factory = CreateMockFactory(HttpStatusCode.OK, "should not reach");
        var dns = CreateMockDnsResolver(PublicIp, IPAddress.Parse("10.0.0.1"));
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolutionFails_ReturnsNull()
    {
        var factory = CreateMockFactory(HttpStatusCode.OK, "should not reach");
        var dns = CreateThrowingDnsResolver();
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ScrapeAsync_DnsResolvesToNoAddresses_ReturnsNull()
    {
        var factory = CreateMockFactory(HttpStatusCode.OK, "should not reach");
        var dns = CreateMockDnsResolver(); // empty array
        var scraper = new ProfileScraperService(factory, "test-key", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ScrapeAsync("https://zillow.com/profile/test", CancellationToken.None);

        Assert.Null(result);
    }

    // --- IsPrivateIp direct tests for branch coverage ---

    [Theory]
    [InlineData("10.0.0.1", true)]
    [InlineData("10.255.255.255", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.255", true)]
    [InlineData("172.15.0.1", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.0.1", true)]
    [InlineData("192.169.0.1", false)]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.255.255.255", true)]
    [InlineData("169.254.0.1", true)]
    [InlineData("169.253.0.1", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("104.18.32.7", false)]
    [InlineData("1.1.1.1", false)]
    public void IsPrivateIp_IPv4(string ipStr, bool expected)
    {
        var ip = IPAddress.Parse(ipStr);
        Assert.Equal(expected, ProfileScraperService.IsPrivateIp(ip));
    }

    [Theory]
    [InlineData("fd00::1", true)]       // unique local
    [InlineData("fdff::1", true)]       // unique local range
    [InlineData("fe80::1", true)]       // link-local
    [InlineData("fe80::abcd", true)]    // link-local
    [InlineData("::1", false)]          // loopback — handled by IPAddress.IsLoopback, not IsPrivateIp
    [InlineData("2001:db8::1", false)]  // documentation range, but public
    [InlineData("2607:f8b0:4004:800::200e", false)] // Google public
    public void IsPrivateIp_IPv6(string ipStr, bool expected)
    {
        var ip = IPAddress.Parse(ipStr);
        Assert.Equal(expected, ProfileScraperService.IsPrivateIp(ip));
    }

    // --- ValidateUrl static tests ---

    [Theory]
    [InlineData("not-a-url", "Invalid URL format")]
    [InlineData("http://zillow.com/profile/test", "Only HTTPS URLs are allowed")]
    [InlineData("https://evil.com/profile/test", "Domain 'evil.com' is not in the allowed list")]
    public void ValidateUrl_InvalidInputs(string url, string expectedError)
    {
        var result = ProfileScraperService.ValidateUrl(url);
        Assert.NotNull(result);
        Assert.Contains(expectedError, result);
    }

    [Fact]
    public void ValidateUrl_ValidUrl_ReturnsNull()
    {
        var result = ProfileScraperService.ValidateUrl("https://zillow.com/profile/test");
        Assert.Null(result);
    }

    // --- ValidateDnsResolutionAsync direct tests ---

    [Fact]
    public async Task ValidateDnsResolutionAsync_PublicIp_ReturnsNull()
    {
        var dns = CreateMockDnsResolver(PublicIp);
        var factory = CreateMockFactory(HttpStatusCode.OK, "");
        var scraper = new ProfileScraperService(factory, "k", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ValidateDnsResolutionAsync("zillow.com", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateDnsResolutionAsync_PrivateIp_ReturnsError()
    {
        var dns = CreateMockDnsResolver(IPAddress.Parse("10.0.0.1"));
        var factory = CreateMockFactory(HttpStatusCode.OK, "");
        var scraper = new ProfileScraperService(factory, "k", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ValidateDnsResolutionAsync("zillow.com", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("private/loopback", result);
    }

    [Fact]
    public async Task ValidateDnsResolutionAsync_DnsFailure_ReturnsError()
    {
        var dns = CreateThrowingDnsResolver();
        var factory = CreateMockFactory(HttpStatusCode.OK, "");
        var scraper = new ProfileScraperService(factory, "k", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ValidateDnsResolutionAsync("nonexistent.example", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("DNS resolution failed", result);
    }

    [Fact]
    public async Task ValidateDnsResolutionAsync_EmptyAddresses_ReturnsError()
    {
        var dns = CreateMockDnsResolver();
        var factory = CreateMockFactory(HttpStatusCode.OK, "");
        var scraper = new ProfileScraperService(factory, "k", null, dns, NullLogger<ProfileScraperService>.Instance);

        var result = await scraper.ValidateDnsResolutionAsync("zillow.com", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("no addresses", result);
    }
}
