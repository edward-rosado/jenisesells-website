using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.ConnectGoogle;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.ConnectGoogle;

public class GoogleOAuthCallbackEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();
    private readonly Mock<GoogleOAuthService> _mockOAuth;
    private readonly Mock<ITokenStore> _mockTokenStore = new();
    private readonly OnboardingStateMachine _sm = new();
    private readonly IConfiguration _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Platform:BaseUrl"] = "http://localhost:3000" })
        .Build();
    private readonly NullLogger<GoogleOAuthCallbackEndpoint> _logger = new();

    public GoogleOAuthCallbackEndpointTests()
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        _mockOAuth = new Mock<GoogleOAuthService>(
            factoryMock.Object, "client-id", "client-secret", "http://localhost:5000/oauth/google/callback",
            NullLogger<GoogleOAuthService>.Instance);
        _mockTokenStore.Setup(t => t.SaveAsync(It.IsAny<OAuthCredential>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static OAuthCredential MakeTokens(string email = "agent@gmail.com", string name = "Jane Doe") => new()
    {
        AccessToken = "ya29.test",
        RefreshToken = "1//test",
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        Scopes = ["gmail.send"],
        Email = email,
        Name = name,
    };

    private OnboardingSession MakeSession(string? profileEmail = null, string? agentConfigId = null)
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ConnectGoogle;
        session.OAuthNonce = "test-nonce";
        if (profileEmail is not null)
            session.Profile = new ScrapedProfile { Email = profileEmail };
        if (agentConfigId is not null)
            session.AgentConfigId = agentConfigId;
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockStore.Setup(s => s.SaveAsync(session, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return session;
    }

    [Fact]
    public async Task Handle_WithValidCode_StoresTokensAndAdvancesState()
    {
        var session = MakeSession(); // no profile email — should allow
        var tokens = MakeTokens();
        _mockOAuth.Setup(o => o.ExchangeCodeAsync("auth-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        var result = await GoogleOAuthCallbackEndpoint.Handle(
            "auth-code", $"{session.Id}:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.NotNull(session.GoogleTokens);
        Assert.Equal("agent@gmail.com", session.GoogleTokens.Email);
        Assert.Equal(OnboardingState.DemoCma, session.CurrentState);
        _mockStore.Verify(s => s.SaveAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailMatch_AdvancesState()
    {
        var session = MakeSession(profileEmail: "agent@gmail.com");
        var tokens = MakeTokens(email: "agent@gmail.com");
        _mockOAuth.Setup(o => o.ExchangeCodeAsync("auth-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        await GoogleOAuthCallbackEndpoint.Handle(
            "auth-code", $"{session.Id}:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.Equal(OnboardingState.DemoCma, session.CurrentState);
        Assert.NotNull(session.GoogleTokens);
    }

    [Fact]
    public async Task Handle_EmailMatchCaseInsensitive_AdvancesState()
    {
        var session = MakeSession(profileEmail: "Agent@Gmail.COM");
        var tokens = MakeTokens(email: "agent@gmail.com");
        _mockOAuth.Setup(o => o.ExchangeCodeAsync("auth-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        await GoogleOAuthCallbackEndpoint.Handle(
            "auth-code", $"{session.Id}:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.Equal(OnboardingState.DemoCma, session.CurrentState);
        Assert.NotNull(session.GoogleTokens);
    }

    [Fact]
    public async Task Handle_EmailMismatch_DoesNotAdvanceState()
    {
        var session = MakeSession(profileEmail: "realagent@gmail.com");
        var tokens = MakeTokens(email: "imposter@gmail.com");
        _mockOAuth.Setup(o => o.ExchangeCodeAsync("auth-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        await GoogleOAuthCallbackEndpoint.Handle(
            "auth-code", $"{session.Id}:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.Equal(OnboardingState.ConnectGoogle, session.CurrentState);
        Assert.Null(session.GoogleTokens);
        _mockStore.Verify(s => s.SaveAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullScrapedProfileEmail_AllowsAnyGoogleAccount()
    {
        var session = MakeSession(); // no profile at all
        var tokens = MakeTokens(email: "any@gmail.com");
        _mockOAuth.Setup(o => o.ExchangeCodeAsync("auth-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        await GoogleOAuthCallbackEndpoint.Handle(
            "auth-code", $"{session.Id}:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.Equal(OnboardingState.DemoCma, session.CurrentState);
        Assert.NotNull(session.GoogleTokens);
    }

    [Fact]
    public async Task Handle_EmptyScrapedProfileEmail_AllowsAnyGoogleAccount()
    {
        var session = MakeSession(profileEmail: "  ");
        var tokens = MakeTokens(email: "any@gmail.com");
        _mockOAuth.Setup(o => o.ExchangeCodeAsync("auth-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        await GoogleOAuthCallbackEndpoint.Handle(
            "auth-code", $"{session.Id}:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.Equal(OnboardingState.DemoCma, session.CurrentState);
    }

    [Fact]
    public async Task Handle_WithMissingSession_ReturnsErrorHtml()
    {
        _mockStore.Setup(s => s.LoadAsync("bad-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var result = await GoogleOAuthCallbackEndpoint.Handle(
            "code", "bad-id:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WithErrorParam_ReturnsErrorHtml()
    {
        var result = await GoogleOAuthCallbackEndpoint.Handle(
            null, "session-id", "access_denied",
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WhenExchangeFails_ReturnsErrorHtml()
    {
        var session = MakeSession();
        _mockOAuth.Setup(o => o.ExchangeCodeAsync("bad-code", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Token exchange failed"));

        var result = await GoogleOAuthCallbackEndpoint.Handle(
            "bad-code", $"{session.Id}:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.Null(session.GoogleTokens);
    }

    [Fact]
    public async Task Handle_WithNoCode_ReturnsErrorHtml()
    {
        var result = await GoogleOAuthCallbackEndpoint.Handle(
            null, "session-id:nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WithInvalidStateFormat_ReturnsErrorHtml()
    {
        var result = await GoogleOAuthCallbackEndpoint.Handle(
            "code", "no-colon-separator", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WithWrongNonce_ReturnsErrorHtml()
    {
        var session = MakeSession();

        var result = await GoogleOAuthCallbackEndpoint.Handle(
            "code", $"{session.Id}:wrong-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(OnboardingState.ConnectGoogle, session.CurrentState);
    }

    [Fact]
    public async Task Handle_WithNullOAuthNonce_ReturnsErrorHtml()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ConnectGoogle;
        session.OAuthNonce = null; // nonce not set
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await GoogleOAuthCallbackEndpoint.Handle(
            "code", $"{session.Id}:any-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WithAgentConfigId_PersistsTokensToTokenStore()
    {
        var session = MakeSession(agentConfigId: "jenise-buckalew");
        var tokens = MakeTokens();
        _mockOAuth.Setup(o => o.ExchangeCodeAsync("auth-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        await GoogleOAuthCallbackEndpoint.Handle(
            "auth-code", $"{session.Id}:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        _mockTokenStore.Verify(t => t.SaveAsync(
            It.Is<OAuthCredential>(c =>
                c.AccountId == "jenise-buckalew" &&
                c.AgentId == "jenise-buckalew" &&
                c.AccessToken == tokens.AccessToken),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutAgentConfigId_SkipsTokenStorePersist()
    {
        var session = MakeSession(); // no AgentConfigId
        var tokens = MakeTokens();
        _mockOAuth.Setup(o => o.ExchangeCodeAsync("auth-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        await GoogleOAuthCallbackEndpoint.Handle(
            "auth-code", $"{session.Id}:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _mockTokenStore.Object, _configuration, _logger, CancellationToken.None);

        _mockTokenStore.Verify(t => t.SaveAsync(It.IsAny<OAuthCredential>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class IsEmailMatchTests
{
    [Theory]
    [InlineData(null, "user@gmail.com", true)]         // null profile email — allow
    [InlineData("", "user@gmail.com", true)]            // empty profile email — allow
    [InlineData("  ", "user@gmail.com", true)]          // whitespace profile email — allow
    [InlineData("user@gmail.com", "user@gmail.com", true)]   // exact match
    [InlineData("User@Gmail.COM", "user@gmail.com", true)]   // case-insensitive
    [InlineData(" user@gmail.com ", "user@gmail.com", true)]  // trimmed
    [InlineData("other@gmail.com", "user@gmail.com", false)]  // mismatch
    [InlineData("user@gmail.com", null, false)]         // null Google email
    [InlineData("user@gmail.com", "", false)]            // empty Google email
    [InlineData("user@gmail.com", "  ", false)]          // whitespace Google email
    public void IsEmailMatch_ReturnsExpected(string? profileEmail, string? googleEmail, bool expected)
    {
        Assert.Equal(expected, GoogleOAuthCallbackEndpoint.IsEmailMatch(profileEmail, googleEmail));
    }
}

public class HashEmailTests
{
    [Fact]
    public void HashEmail_NullInput_ReturnsNull()
    {
        Assert.Equal("null", GoogleOAuthCallbackEndpoint.HashEmail(null));
    }

    [Fact]
    public void HashEmail_EmptyInput_ReturnsNull()
    {
        Assert.Equal("null", GoogleOAuthCallbackEndpoint.HashEmail(""));
    }

    [Fact]
    public void HashEmail_WhitespaceInput_ReturnsNull()
    {
        Assert.Equal("null", GoogleOAuthCallbackEndpoint.HashEmail("  "));
    }

    [Fact]
    public void HashEmail_SameEmailDifferentCase_ReturnsSameHash()
    {
        var hash1 = GoogleOAuthCallbackEndpoint.HashEmail("Test@Gmail.com");
        var hash2 = GoogleOAuthCallbackEndpoint.HashEmail("test@gmail.com");
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashEmail_DifferentEmails_ReturnDifferentHashes()
    {
        var hash1 = GoogleOAuthCallbackEndpoint.HashEmail("alice@gmail.com");
        var hash2 = GoogleOAuthCallbackEndpoint.HashEmail("bob@gmail.com");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashEmail_ValidEmail_ReturnsTwelveCharHex()
    {
        var hash = GoogleOAuthCallbackEndpoint.HashEmail("test@example.com");
        Assert.Equal(12, hash.Length);
        Assert.Matches("^[0-9a-f]{12}$", hash);
    }
}
