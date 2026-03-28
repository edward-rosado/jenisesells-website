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
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Notifications.WhatsApp;
using Moq;

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
