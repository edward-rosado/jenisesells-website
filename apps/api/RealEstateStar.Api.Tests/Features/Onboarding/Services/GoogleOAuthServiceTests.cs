using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class GoogleOAuthServiceTests
{
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";
    private const string RedirectUri = "http://localhost:5000/oauth/google/callback";

    [Fact]
    public void BuildAuthorizationUrl_ReturnsGoogleUrl_WithAllScopes()
    {
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handler.Object);
        var service = new GoogleOAuthService(httpClient, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        var url = service.BuildAuthorizationUrl("session123");

        Assert.Contains("accounts.google.com/o/oauth2/v2/auth", url);
        Assert.Contains("client_id=test-client-id", url);
        Assert.Contains("state=session123", url);
        Assert.Contains("gmail.send", url);
        Assert.Contains("drive.file", url);
        Assert.Contains("userinfo.profile", url);
        Assert.Contains("userinfo.email", url);
        Assert.Contains("documents", url);
        Assert.Contains("spreadsheets", url);
        Assert.Contains("calendar.events", url);
        Assert.Contains("access_type=offline", url);
        Assert.Contains("prompt=consent", url);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsGoogleTokens()
    {
        var tokenResponse = new
        {
            access_token = "ya29.test-access",
            refresh_token = "1//test-refresh",
            expires_in = 3600,
            scope = "openid email profile",
            token_type = "Bearer"
        };

        var profileResponse = new
        {
            email = "agent@gmail.com",
            name = "Jane Doe",
        };

        var handler = new Mock<HttpMessageHandler>();
        var callCount = 0;
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var json = callCount == 1
                    ? JsonSerializer.Serialize(tokenResponse)
                    : JsonSerializer.Serialize(profileResponse);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new GoogleOAuthService(httpClient, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        var tokens = await service.ExchangeCodeAsync("auth-code-123", CancellationToken.None);

        Assert.Equal("ya29.test-access", tokens.AccessToken);
        Assert.Equal("1//test-refresh", tokens.RefreshToken);
        Assert.Equal("agent@gmail.com", tokens.GoogleEmail);
        Assert.Equal("Jane Doe", tokens.GoogleName);
        Assert.False(tokens.IsExpired);
    }

    [Fact]
    public async Task ExchangeCodeAsync_WhenTokenEndpointFails_Throws()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\"}")
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new GoogleOAuthService(httpClient, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExchangeCodeAsync("bad-code", CancellationToken.None));
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_UpdatesTokenFields()
    {
        var refreshResponse = new
        {
            access_token = "ya29.new-access",
            expires_in = 3600,
            scope = "openid email profile",
            token_type = "Bearer"
        };

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(refreshResponse),
                    System.Text.Encoding.UTF8,
                    "application/json")
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new GoogleOAuthService(httpClient, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        var tokens = new GoogleTokens
        {
            AccessToken = "ya29.old-access",
            RefreshToken = "1//test-refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            Scopes = ["gmail.send"],
            GoogleEmail = "agent@gmail.com",
            GoogleName = "Jane Doe",
        };

        await service.RefreshAccessTokenAsync(tokens, CancellationToken.None);

        Assert.Equal("ya29.new-access", tokens.AccessToken);
        Assert.False(tokens.IsExpired);
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_WhenRefreshFails_Throws()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\"}")
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new GoogleOAuthService(httpClient, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        var tokens = new GoogleTokens
        {
            AccessToken = "ya29.old",
            RefreshToken = "1//bad-refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            Scopes = ["gmail.send"],
            GoogleEmail = "agent@gmail.com",
            GoogleName = "Jane Doe",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RefreshAccessTokenAsync(tokens, CancellationToken.None));
    }
}
