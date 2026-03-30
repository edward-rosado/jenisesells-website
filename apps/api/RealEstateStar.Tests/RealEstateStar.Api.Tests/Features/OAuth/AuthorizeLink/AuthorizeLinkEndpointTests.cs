using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.OAuth.AuthorizeLink;
using RealEstateStar.Api.Features.OAuth.Services;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using System.Threading.Channels;

namespace RealEstateStar.Api.Tests.Features.OAuth.AuthorizeLink;

public class AuthorizeLinkEndpointTests
{
    private const string Secret = "test-secret-32-bytes-long-enough!";

    private static AuthorizationLinkService CreateLinkService() =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OAuthLink:Secret"] = Secret,
                    ["OAuthLink:ExpirationHours"] = "24",
                    ["Api:BaseUrl"] = "https://api.real-estate-star.com",
                })
                .Build(),
            NullLogger<AuthorizationLinkService>.Instance);

    private static (string accountId, string agentId, string email, long exp, string sig)
        GenerateValidParams(AuthorizationLinkService svc)
    {
        var url = svc.GenerateLink("acct-1", "agent-1", "agent@example.com");
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return (
            query["accountId"]!,
            query["agentId"]!,
            query["email"]!,
            long.Parse(query["exp"]!),
            query["sig"]!
        );
    }

    // ─── AuthorizeLinkEndpoint (GET landing page) ────────────────────────────────

    [Fact]
    public async Task AuthorizeLink_ValidSignature_ReturnsHtmlLandingPage()
    {
        var svc = CreateLinkService();
        var (accountId, agentId, email, exp, sig) = GenerateValidParams(svc);

        var result = await AuthorizeLinkEndpoint.Handle(
            accountId, agentId, email, exp, sig, svc,
            NullLogger<AuthorizeLinkEndpoint>.Instance, CancellationToken.None);

        var html = Assert.IsType<ContentHttpResult>(result);
        Assert.Equal("text/html", html.ContentType);
        Assert.Contains("Connect Your Business Google Account", html.ResponseContent);
        Assert.Contains("Connect with Google", html.ResponseContent);
    }

    [Fact]
    public async Task AuthorizeLink_InvalidSignature_Returns401()
    {
        var svc = CreateLinkService();
        var (accountId, agentId, email, exp, _) = GenerateValidParams(svc);

        var result = await AuthorizeLinkEndpoint.Handle(
            accountId, agentId, email, exp, "badsignature00000000000000000000000000000000000000000000000000000",
            svc, NullLogger<AuthorizeLinkEndpoint>.Instance, CancellationToken.None);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task AuthorizeLink_ExpiredSignature_Returns410()
    {
        var svc = CreateLinkService();
        var (accountId, agentId, email, _, _) = GenerateValidParams(svc);
        // Compute sig over expired exp
        var expiredExp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var key = System.Text.Encoding.UTF8.GetBytes(Secret);
        var payload = System.Text.Encoding.UTF8.GetBytes($"{accountId}.{agentId}.{email}.{expiredExp}");
        var sigBytes = System.Security.Cryptography.HMACSHA256.HashData(key, payload);
        var expiredSig = Convert.ToHexString(sigBytes).ToLowerInvariant();

        var result = await AuthorizeLinkEndpoint.Handle(
            accountId, agentId, email, expiredExp, expiredSig,
            svc, NullLogger<AuthorizeLinkEndpoint>.Instance, CancellationToken.None);

        Assert.IsType<StatusCodeHttpResult>(result);
        var sc = (StatusCodeHttpResult)result;
        Assert.Equal(410, sc.StatusCode);
    }

    [Fact]
    public async Task AuthorizeLink_HtmlEncodesMaliciousEmail()
    {
        // The email in the landing page is HTML-encoded — XSS should not be possible
        var svc = CreateLinkService();
        var url = svc.GenerateLink("acct-1", "agent-1", "<script>alert(1)</script>@evil.com");
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        var result = await AuthorizeLinkEndpoint.Handle(
            query["accountId"]!,
            query["agentId"]!,
            query["email"]!,
            long.Parse(query["exp"]!),
            query["sig"]!,
            svc, NullLogger<AuthorizeLinkEndpoint>.Instance, CancellationToken.None);

        var html = Assert.IsType<ContentHttpResult>(result);
        Assert.DoesNotContain("<script>alert(1)</script>", html.ResponseContent);
        Assert.Contains("&lt;script&gt;", html.ResponseContent);
    }

    // ─── ConnectEndpoint (POST /oauth/google/authorize/connect) ─────────────────

    private static (RealEstateStar.Api.Features.Onboarding.Services.GoogleOAuthService oauthSvc, IConfiguration config) CreateConnectDeps()
    {
        var factoryMock = new Mock<System.Net.Http.IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new System.Net.Http.HttpClient());
        var oauthSvc = new Mock<RealEstateStar.Api.Features.Onboarding.Services.GoogleOAuthService>(
            factoryMock.Object, "client-id", "client-secret", "https://example.com/onboard/callback",
            NullLogger<RealEstateStar.Api.Features.Onboarding.Services.GoogleOAuthService>.Instance);
        oauthSvc.CallBase = true;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:AuthorizeLinkRedirectUri"] = "https://api.real-estate-star.com/oauth/google/authorize/callback",
            })
            .Build();
        return (oauthSvc.Object, config);
    }

    [Fact]
    public async Task Connect_ValidSignature_RedirectsToGoogle()
    {
        var svc = CreateLinkService();
        var (accountId, agentId, email, exp, sig) = GenerateValidParams(svc);
        var request = new ConnectRequest(accountId, agentId, email, exp, sig);
        var (oauthSvc, config) = CreateConnectDeps();

        var result = await ConnectEndpoint.Handle(
            request, svc, oauthSvc, config,
            NullLogger<ConnectEndpoint>.Instance, CancellationToken.None);

        Assert.IsType<RedirectHttpResult>(result);
        var redirect = (RedirectHttpResult)result;
        Assert.StartsWith("https://accounts.google.com", redirect.Url);
    }

    [Fact]
    public async Task Connect_InvalidSignature_Returns401()
    {
        var svc = CreateLinkService();
        var (accountId, agentId, email, exp, _) = GenerateValidParams(svc);
        var request = new ConnectRequest(accountId, agentId, email, exp, "badbadbadbadbadbadbadbadbadbadbadbadbadbadbadbadbadbadbadbadbadbad");
        var (oauthSvc, config) = CreateConnectDeps();

        var result = await ConnectEndpoint.Handle(
            request, svc, oauthSvc, config,
            NullLogger<ConnectEndpoint>.Instance, CancellationToken.None);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Connect_StoresNonceAndEmbedsStateInGoogleUrl()
    {
        var svc = CreateLinkService();
        var (accountId, agentId, email, exp, sig) = GenerateValidParams(svc);
        var request = new ConnectRequest(accountId, agentId, email, exp, sig);
        var (oauthSvc, config) = CreateConnectDeps();

        var result = await ConnectEndpoint.Handle(
            request, svc, oauthSvc, config,
            NullLogger<ConnectEndpoint>.Instance, CancellationToken.None);

        var redirect = Assert.IsType<RedirectHttpResult>(result);
        var redirectUri = new Uri(redirect.Url!);
        var qs = System.Web.HttpUtility.ParseQueryString(redirectUri.Query);
        var state = qs["state"];
        // State is the nonce — 32 lowercase hex chars; identity is bound inside via AuthorizationLinkState
        Assert.NotNull(state);
        Assert.Matches("^[0-9a-f]{32}$", state);
    }
}

public class AuthorizeLinkCallbackEndpointTests
{
    private static AuthorizationLinkService CreateLinkService() =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OAuthLink:Secret"] = "test-secret-32-bytes-long-enough!",
                    ["OAuthLink:ExpirationHours"] = "24",
                    ["Api:BaseUrl"] = "https://api.real-estate-star.com",
                })
                .Build(),
            NullLogger<AuthorizationLinkService>.Instance);

    private static Mock<RealEstateStar.Api.Features.Onboarding.Services.GoogleOAuthService> CreateOAuthMock(OAuthCredential? tokens = null)
    {
        var factoryMock = new Mock<System.Net.Http.IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new System.Net.Http.HttpClient());
        var mock = new Mock<RealEstateStar.Api.Features.Onboarding.Services.GoogleOAuthService>(
            factoryMock.Object, "client-id", "client-secret", "https://example.com/callback",
            NullLogger<RealEstateStar.Api.Features.Onboarding.Services.GoogleOAuthService>.Instance);

        if (tokens is not null)
            mock.Setup(o => o.ExchangeCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokens);
        return mock;
    }

    private static OAuthCredential MakeTokens(string email = "agent@example.com") => new()
    {
        AccessToken = "ya29.test",
        RefreshToken = "1//test",
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        Scopes = ["gmail.send"],
        Email = email,
        Name = "Test Agent",
    };

    private static (ChannelWriter<ActivationRequest> writer, Channel<ActivationRequest> channel) CreateChannel()
    {
        var channel = Channel.CreateUnbounded<ActivationRequest>();
        return (channel.Writer, channel);
    }

    // ─── Callback happy path ──────────────────────────────────────────────────

    [Fact]
    public async Task Callback_ValidCodeAndState_StoresTokensAndEnqueues()
    {
        var svc = CreateLinkService();
        var nonce = svc.GenerateNonce("acct-1", "agent-1", "agent@example.com");
        var tokens = MakeTokens("agent@example.com");
        var oauthMock = CreateOAuthMock(tokens);
        var tokenStoreMock = new Mock<ITokenStore>();
        tokenStoreMock.Setup(t => t.SaveAsync(It.IsAny<OAuthCredential>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var (writer, channel) = CreateChannel();

        var result = await AuthorizeLinkCallbackEndpoint.Handle(
            "auth-code", nonce, null,
            svc, oauthMock.Object, tokenStoreMock.Object, writer,
            NullLogger<AuthorizeLinkCallbackEndpoint>.Instance, CancellationToken.None);

        // Should return success HTML
        var html = Assert.IsType<ContentHttpResult>(result);
        Assert.Equal("text/html", html.ContentType);
        Assert.Contains("Google account connected", html.ResponseContent, StringComparison.OrdinalIgnoreCase);

        // Token store should have been called
        tokenStoreMock.Verify(t => t.SaveAsync(
            It.Is<OAuthCredential>(c => c.AccountId == "acct-1" && c.AgentId == "agent-1"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Channel should have a message
        Assert.True(channel.Reader.TryRead(out var req));
        Assert.Equal("acct-1", req.AccountId);
        Assert.Equal("agent-1", req.AgentId);
    }

    [Fact]
    public async Task Callback_ErrorParam_ReturnsErrorPage()
    {
        var svc = CreateLinkService();
        var oauthMock = CreateOAuthMock();
        var tokenStoreMock = new Mock<ITokenStore>();
        var (writer, _) = CreateChannel();

        var result = await AuthorizeLinkCallbackEndpoint.Handle(
            null, "0000000000000000ffffffffffffffff", "access_denied",
            svc, oauthMock.Object, tokenStoreMock.Object, writer,
            NullLogger<AuthorizeLinkCallbackEndpoint>.Instance, CancellationToken.None);

        var html = Assert.IsType<ContentHttpResult>(result);
        Assert.Contains("Something Went Wrong", html.ResponseContent);
    }

    [Fact]
    public async Task Callback_InvalidStateFormat_ReturnsErrorPage()
    {
        var svc = CreateLinkService();
        var oauthMock = CreateOAuthMock();
        var tokenStoreMock = new Mock<ITokenStore>();
        var (writer, _) = CreateChannel();

        var result = await AuthorizeLinkCallbackEndpoint.Handle(
            "auth-code", "no-colons-at-all", null,
            svc, oauthMock.Object, tokenStoreMock.Object, writer,
            NullLogger<AuthorizeLinkCallbackEndpoint>.Instance, CancellationToken.None);

        var html = Assert.IsType<ContentHttpResult>(result);
        Assert.Contains("Something Went Wrong", html.ResponseContent);
    }

    [Fact]
    public async Task Callback_ExpiredNonce_ReturnsExpiredPage()
    {
        var svc = CreateLinkService();
        var nonce = svc.GenerateNonce("acct-1", "agent-1", "agent@example.com");
        svc.ForceExpireNonce(nonce);
        var oauthMock = CreateOAuthMock();
        var tokenStoreMock = new Mock<ITokenStore>();
        var (writer, _) = CreateChannel();

        var result = await AuthorizeLinkCallbackEndpoint.Handle(
            "auth-code", nonce, null,
            svc, oauthMock.Object, tokenStoreMock.Object, writer,
            NullLogger<AuthorizeLinkCallbackEndpoint>.Instance, CancellationToken.None);

        var html = Assert.IsType<ContentHttpResult>(result);
        Assert.Contains("Please request a new one", html.ResponseContent);
    }

    [Fact]
    public async Task Callback_UnknownNonce_ReturnsExpiredPage()
    {
        var svc = CreateLinkService();
        var oauthMock = CreateOAuthMock();
        var tokenStoreMock = new Mock<ITokenStore>();
        var (writer, _) = CreateChannel();

        var result = await AuthorizeLinkCallbackEndpoint.Handle(
            "auth-code", "0000000000000000ffffffffffffffff", null,
            svc, oauthMock.Object, tokenStoreMock.Object, writer,
            NullLogger<AuthorizeLinkCallbackEndpoint>.Instance, CancellationToken.None);

        var html = Assert.IsType<ContentHttpResult>(result);
        Assert.Contains("Please request a new one", html.ResponseContent);
    }

    [Fact]
    public async Task Callback_EmailMismatch_ReturnsMismatchPage()
    {
        var svc = CreateLinkService();
        var nonce = svc.GenerateNonce("acct-1", "agent-1", "expected@example.com");
        var tokens = MakeTokens("wrong@example.com"); // different email
        var oauthMock = CreateOAuthMock(tokens);
        var tokenStoreMock = new Mock<ITokenStore>();
        var (writer, _) = CreateChannel();

        var result = await AuthorizeLinkCallbackEndpoint.Handle(
            "auth-code", nonce, null,
            svc, oauthMock.Object, tokenStoreMock.Object, writer,
            NullLogger<AuthorizeLinkCallbackEndpoint>.Instance, CancellationToken.None);

        var html = Assert.IsType<ContentHttpResult>(result);
        Assert.Contains("Google Account Mismatch", html.ResponseContent);

        // Token store must NOT be called on mismatch
        tokenStoreMock.Verify(t => t.SaveAsync(It.IsAny<OAuthCredential>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Callback_NullCode_ReturnsErrorPage()
    {
        var svc = CreateLinkService();
        var nonce = svc.GenerateNonce("acct-1", "agent-1", "agent@example.com");
        var oauthMock = CreateOAuthMock();
        var tokenStoreMock = new Mock<ITokenStore>();
        var (writer, _) = CreateChannel();

        var result = await AuthorizeLinkCallbackEndpoint.Handle(
            null, nonce, null,
            svc, oauthMock.Object, tokenStoreMock.Object, writer,
            NullLogger<AuthorizeLinkCallbackEndpoint>.Instance, CancellationToken.None);

        var html = Assert.IsType<ContentHttpResult>(result);
        Assert.Contains("Something Went Wrong", html.ResponseContent);
    }

    [Fact]
    public async Task Callback_ExchangeThrows_ReturnsErrorPage()
    {
        var svc = CreateLinkService();
        var nonce = svc.GenerateNonce("acct-1", "agent-1", "agent@example.com");
        var oauthMock = CreateOAuthMock();
        oauthMock.Setup(o => o.ExchangeCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Token exchange failed"));
        var tokenStoreMock = new Mock<ITokenStore>();
        var (writer, _) = CreateChannel();

        var result = await AuthorizeLinkCallbackEndpoint.Handle(
            "auth-code", nonce, null,
            svc, oauthMock.Object, tokenStoreMock.Object, writer,
            NullLogger<AuthorizeLinkCallbackEndpoint>.Instance, CancellationToken.None);

        var html = Assert.IsType<ContentHttpResult>(result);
        Assert.Contains("Something Went Wrong", html.ResponseContent);
    }

    [Fact]
    public async Task Callback_NonceIsSingleUse_SecondCallFails()
    {
        var svc = CreateLinkService();
        var nonce = svc.GenerateNonce("acct-1", "agent-1", "agent@example.com");
        var tokens = MakeTokens("agent@example.com");
        var oauthMock = CreateOAuthMock(tokens);
        var tokenStoreMock = new Mock<ITokenStore>();
        tokenStoreMock.Setup(t => t.SaveAsync(It.IsAny<OAuthCredential>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var (writer, _) = CreateChannel();

        // First call succeeds
        await AuthorizeLinkCallbackEndpoint.Handle(
            "auth-code", nonce, null,
            svc, oauthMock.Object, tokenStoreMock.Object, writer,
            NullLogger<AuthorizeLinkCallbackEndpoint>.Instance, CancellationToken.None);

        // Second call with same nonce should show expired page
        var result = await AuthorizeLinkCallbackEndpoint.Handle(
            "auth-code", nonce, null,
            svc, oauthMock.Object, tokenStoreMock.Object, writer,
            NullLogger<AuthorizeLinkCallbackEndpoint>.Instance, CancellationToken.None);

        var html = Assert.IsType<ContentHttpResult>(result);
        Assert.Contains("Please request a new one", html.ResponseContent);
    }

    [Fact]
    public async Task Callback_HtmlEncodesUserInput()
    {
        // If an attacker manipulates the state or error param, HTML-encoding prevents XSS
        var svc = CreateLinkService();
        var nonce = svc.GenerateNonce("acct-1", "agent-1", "agent@example.com");
        var oauthMock = CreateOAuthMock();
        var tokenStoreMock = new Mock<ITokenStore>();
        var (writer, _) = CreateChannel();

        var result = await AuthorizeLinkCallbackEndpoint.Handle(
            null, nonce, "<script>alert(1)</script>",
            svc, oauthMock.Object, tokenStoreMock.Object, writer,
            NullLogger<AuthorizeLinkCallbackEndpoint>.Instance, CancellationToken.None);

        var html = Assert.IsType<ContentHttpResult>(result);
        Assert.DoesNotContain("<script>alert(1)</script>", html.ResponseContent);
    }
}
