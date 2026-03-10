using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.ConnectGoogle;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.ConnectGoogle;

public class StartGoogleOAuthEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();
    private readonly Mock<GoogleOAuthService> _mockOAuth;

    public StartGoogleOAuthEndpointTests()
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        _mockOAuth = new Mock<GoogleOAuthService>(
            factoryMock.Object, "client-id", "client-secret", "http://localhost:5000/oauth/google/callback",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleOAuthService>.Instance);
    }

    [Fact]
    public async Task Handle_WithValidSession_RedirectsToGoogle()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ConnectGoogle;
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _mockOAuth.Setup(o => o.BuildAuthorizationUrl(session.Id))
            .Returns(("https://accounts.google.com/o/oauth2/v2/auth?test=true", "test-nonce"));
        _mockStore.Setup(s => s.SaveAsync(session, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await StartGoogleOAuthEndpoint.Handle(
            session.Id, _mockStore.Object, _mockOAuth.Object, CancellationToken.None);

        var redirect = Assert.IsType<RedirectHttpResult>(result);
        Assert.Contains("accounts.google.com", redirect.Url);
    }

    [Fact]
    public async Task Handle_WithMissingSession_Returns404()
    {
        _mockStore.Setup(s => s.LoadAsync("bad-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var result = await StartGoogleOAuthEndpoint.Handle(
            "bad-id", _mockStore.Object, _mockOAuth.Object, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Handle_WhenNotInConnectGoogleState_Returns400()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ScrapeProfile;
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await StartGoogleOAuthEndpoint.Handle(
            session.Id, _mockStore.Object, _mockOAuth.Object, CancellationToken.None);

        Assert.IsType<BadRequest<string>>(result);
    }
}
