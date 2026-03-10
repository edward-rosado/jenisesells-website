using Microsoft.Extensions.Configuration;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.ConnectGoogle;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.ConnectGoogle;

public class GoogleOAuthCallbackEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();
    private readonly Mock<GoogleOAuthService> _mockOAuth;
    private readonly OnboardingStateMachine _sm = new();
    private readonly IConfiguration _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Platform:BaseUrl"] = "http://localhost:3000" })
        .Build();

    public GoogleOAuthCallbackEndpointTests()
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        _mockOAuth = new Mock<GoogleOAuthService>(
            factoryMock.Object, "client-id", "client-secret", "http://localhost:5000/oauth/google/callback",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleOAuthService>.Instance);
    }

    [Fact]
    public async Task Handle_WithValidCode_StoresTokensAndAdvancesState()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ConnectGoogle;
        session.OAuthNonce = "test-nonce";
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockStore.Setup(s => s.SaveAsync(session, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tokens = new GoogleTokens
        {
            AccessToken = "ya29.test",
            RefreshToken = "1//test",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["gmail.send"],
            GoogleEmail = "agent@gmail.com",
            GoogleName = "Jane Doe",
        };
        _mockOAuth.Setup(o => o.ExchangeCodeAsync("auth-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        var result = await GoogleOAuthCallbackEndpoint.Handle(
            "auth-code", $"{session.Id}:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _configuration, CancellationToken.None);

        Assert.NotNull(session.GoogleTokens);
        Assert.Equal("agent@gmail.com", session.GoogleTokens.GoogleEmail);
        Assert.Equal(OnboardingState.GenerateSite, session.CurrentState);
        _mockStore.Verify(s => s.SaveAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMissingSession_ReturnsErrorHtml()
    {
        _mockStore.Setup(s => s.LoadAsync("bad-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var result = await GoogleOAuthCallbackEndpoint.Handle(
            "code", "bad-id:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _configuration, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WithErrorParam_ReturnsErrorHtml()
    {
        var result = await GoogleOAuthCallbackEndpoint.Handle(
            null, "session-id", "access_denied",
            _mockStore.Object, _mockOAuth.Object, _sm, _configuration, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WhenExchangeFails_ReturnsErrorHtml()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ConnectGoogle;
        session.OAuthNonce = "test-nonce";
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockStore.Setup(s => s.SaveAsync(session, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockOAuth.Setup(o => o.ExchangeCodeAsync("bad-code", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Token exchange failed"));

        var result = await GoogleOAuthCallbackEndpoint.Handle(
            "bad-code", $"{session.Id}:test-nonce", null,
            _mockStore.Object, _mockOAuth.Object, _sm, _configuration, CancellationToken.None);

        Assert.Null(session.GoogleTokens);
    }
}
