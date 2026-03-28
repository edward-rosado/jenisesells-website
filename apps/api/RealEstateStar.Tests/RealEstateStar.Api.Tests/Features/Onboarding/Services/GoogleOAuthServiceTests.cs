using Xunit;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Workers.Onboarding;
using RealEstateStar.Workers.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Notifications.WhatsApp;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class GoogleOAuthServiceTests
{
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";
    private const string RedirectUri = "http://localhost:5000/oauth/google/callback";

    private static IHttpClientFactory CreateFactory(Mock<HttpMessageHandler> handler)
    {
        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return factory.Object;
    }

    [Fact]
    public void BuildAuthorizationUrl_ReturnsGoogleUrl_WithAllScopes()
    {
        var handler = new Mock<HttpMessageHandler>();
        var factory = CreateFactory(handler);
        var service = new GoogleOAuthService(factory, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        var (url, _) = service.BuildAuthorizationUrl("session123");

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

        var factory = CreateFactory(handler);
        var service = new GoogleOAuthService(factory, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        var tokens = await service.ExchangeCodeAsync("auth-code-123", CancellationToken.None);

        Assert.Equal("ya29.test-access", tokens.AccessToken);
        Assert.Equal("1//test-refresh", tokens.RefreshToken);
        Assert.Equal("agent@gmail.com", tokens.Email);
        Assert.Equal("Jane Doe", tokens.Name);
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

        var factory = CreateFactory(handler);
        var service = new GoogleOAuthService(factory, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

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

        var factory = CreateFactory(handler);
        var service = new GoogleOAuthService(factory, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        var tokens = new OAuthCredential
        {
            AccessToken = "ya29.old-access",
            RefreshToken = "1//test-refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            Scopes = ["gmail.send"],
            Email = "agent@gmail.com",
            Name = "Jane Doe",
        };

        var refreshed = await service.RefreshAccessTokenAsync(tokens, CancellationToken.None);

        Assert.Equal("ya29.new-access", refreshed.AccessToken);
        Assert.False(refreshed.IsExpired);
    }

    [Fact]
    public void HashEmail_NullEmail_ReturnsNull()
    {
        var result = GoogleOAuthService.HashEmail(null);
        Assert.Equal("null", result);
    }

    [Fact]
    public void HashEmail_WhitespaceEmail_ReturnsNull()
    {
        var result = GoogleOAuthService.HashEmail("   ");
        Assert.Equal("null", result);
    }

    [Fact]
    public void HashEmail_EmptyEmail_ReturnsNull()
    {
        var result = GoogleOAuthService.HashEmail("");
        Assert.Equal("null", result);
    }

    [Fact]
    public void HashEmail_ValidEmail_ReturnsHashString()
    {
        var result = GoogleOAuthService.HashEmail("test@example.com");
        Assert.NotEqual("null", result);
        Assert.Equal(12, result.Length); // First 12 hex chars
    }

    [Fact]
    public void HashEmail_IsCaseInsensitive()
    {
        var lower = GoogleOAuthService.HashEmail("Test@Example.COM");
        var upper = GoogleOAuthService.HashEmail("test@example.com");
        Assert.Equal(lower, upper);
    }

    [Fact]
    public void HashEmail_TrimsWhitespace()
    {
        var trimmed = GoogleOAuthService.HashEmail("test@example.com");
        var untrimmed = GoogleOAuthService.HashEmail("  test@example.com  ");
        Assert.Equal(trimmed, untrimmed);
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

        var factory = CreateFactory(handler);
        var service = new GoogleOAuthService(factory, ClientId, ClientSecret, RedirectUri, NullLogger<GoogleOAuthService>.Instance);

        var tokens = new OAuthCredential
        {
            AccessToken = "ya29.old",
            RefreshToken = "1//bad-refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            Scopes = ["gmail.send"],
            Email = "agent@gmail.com",
            Name = "Jane Doe",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RefreshAccessTokenAsync(tokens, CancellationToken.None));
    }
}
