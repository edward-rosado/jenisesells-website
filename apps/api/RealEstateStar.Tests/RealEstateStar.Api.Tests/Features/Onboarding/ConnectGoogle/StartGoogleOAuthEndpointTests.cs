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
using RealEstateStar.Clients.GoogleOAuth;
using RealEstateStar.Workers.Onboarding;
using RealEstateStar.Workers.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Notifications.WhatsApp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Features.Onboarding.ConnectGoogle;

namespace RealEstateStar.Api.Tests.Features.Onboarding.ConnectGoogle;

public class StartGoogleOAuthEndpointTests
{
    private readonly Mock<ISessionDataService> _mockStore = new();
    private readonly Mock<GoogleOAuthService> _mockOAuth;

    public StartGoogleOAuthEndpointTests()
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        _mockOAuth = new Mock<GoogleOAuthService>(
            factoryMock.Object, "client-id", "client-secret", "http://localhost:5135/oauth/google/callback",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleOAuthService>.Instance);
    }

    private static HttpContext CreateHttpContext(string? bearerToken)
    {
        var context = new DefaultHttpContext();
        if (bearerToken is not null)
            context.Request.Headers.Authorization = $"Bearer {bearerToken}";
        return context;
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

        var httpContext = CreateHttpContext(session.BearerToken);
        var result = await StartGoogleOAuthEndpoint.Handle(
            session.Id, httpContext, _mockStore.Object, _mockOAuth.Object, CancellationToken.None);

        var redirect = Assert.IsType<RedirectHttpResult>(result);
        Assert.Contains("accounts.google.com", redirect.Url);
    }

    [Fact]
    public async Task Handle_WithMissingSession_Returns404()
    {
        _mockStore.Setup(s => s.LoadAsync("bad-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var httpContext = CreateHttpContext("any-token");
        var result = await StartGoogleOAuthEndpoint.Handle(
            "bad-id", httpContext, _mockStore.Object, _mockOAuth.Object, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Handle_WhenNotInConnectGoogleState_Returns400()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ScrapeProfile;
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var httpContext = CreateHttpContext(session.BearerToken);
        var result = await StartGoogleOAuthEndpoint.Handle(
            session.Id, httpContext, _mockStore.Object, _mockOAuth.Object, CancellationToken.None);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task Handle_WithMissingBearerToken_ReturnsUnauthorized()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ConnectGoogle;
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var httpContext = CreateHttpContext(null);
        var result = await StartGoogleOAuthEndpoint.Handle(
            session.Id, httpContext, _mockStore.Object, _mockOAuth.Object, CancellationToken.None);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Handle_WithWrongBearerToken_ReturnsUnauthorized()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ConnectGoogle;
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var httpContext = CreateHttpContext("wrong-token");
        var result = await StartGoogleOAuthEndpoint.Handle(
            session.Id, httpContext, _mockStore.Object, _mockOAuth.Object, CancellationToken.None);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }
}
