using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Features.Onboarding.GetSession;

namespace RealEstateStar.Api.Tests.Features.Onboarding.GetSession;

public class GetSessionEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();

    private static HttpContext CreateHttpContext(string? bearerToken)
    {
        var context = new DefaultHttpContext();
        if (bearerToken is not null)
            context.Request.Headers.Authorization = $"Bearer {bearerToken}";
        return context;
    }

    [Fact]
    public async Task Handle_ValidId_ReturnsSession()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var httpContext = CreateHttpContext(session.BearerToken);
        var result = await GetSessionEndpoint.Handle(session.Id, httpContext, _mockStore.Object, CancellationToken.None);

        var ok = Assert.IsType<Ok<GetSessionResponse>>(result);
        Assert.Equal(session.Id, ok.Value!.SessionId);
        Assert.Equal(OnboardingState.ScrapeProfile, ok.Value.CurrentState);
    }

    [Fact]
    public async Task Handle_InvalidId_Returns404()
    {
        _mockStore.Setup(s => s.LoadAsync("nope", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var httpContext = CreateHttpContext("any-token");
        var result = await GetSessionEndpoint.Handle("nope", httpContext, _mockStore.Object, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Handle_MissingBearerToken_Returns401()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var httpContext = CreateHttpContext(null);
        var result = await GetSessionEndpoint.Handle(session.Id, httpContext, _mockStore.Object, CancellationToken.None);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Handle_WrongBearerToken_Returns401()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var httpContext = CreateHttpContext("wrong-token");
        var result = await GetSessionEndpoint.Handle(session.Id, httpContext, _mockStore.Object, CancellationToken.None);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }
}
