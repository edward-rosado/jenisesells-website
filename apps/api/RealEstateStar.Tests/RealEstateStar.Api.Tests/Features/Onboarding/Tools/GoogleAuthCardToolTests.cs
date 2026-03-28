using System.Text.Json;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.Onboarding;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class GoogleAuthCardToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsOAuthUrl()
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var mockOAuth = new Mock<GoogleOAuthService>(
            factoryMock.Object, "client-id", "client-secret", "http://localhost:5000/oauth/google/callback",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleOAuthService>.Instance);

        mockOAuth.Setup(o => o.BuildAuthorizationUrl(It.IsAny<string>()))
            .Returns(("https://accounts.google.com/o/oauth2/v2/auth?test=true", "test-nonce"));

        var mockStore = new Mock<ISessionStore>();
        mockStore.Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tool = new GoogleAuthCardTool(mockOAuth.Object, mockStore.Object);
        var session = OnboardingSession.Create(null);

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.Contains("accounts.google.com", result);
        Assert.Equal("google_auth_card", tool.Name);
        Assert.Equal("test-nonce", session.OAuthNonce);
        mockStore.Verify(s => s.SaveAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_IncludesSessionIdInUrl()
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var mockOAuth = new Mock<GoogleOAuthService>(
            factoryMock.Object, "client-id", "client-secret", "http://localhost:5000/oauth/google/callback",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleOAuthService>.Instance);

        mockOAuth.Setup(o => o.BuildAuthorizationUrl(It.IsAny<string>()))
            .Returns((string sid) => ($"https://accounts.google.com/o/oauth2/v2/auth?state={sid}", "test-nonce"));

        var mockStore = new Mock<ISessionStore>();
        mockStore.Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tool = new GoogleAuthCardTool(mockOAuth.Object, mockStore.Object);
        var session = OnboardingSession.Create(null);

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.Contains(session.Id, result);
    }
}
