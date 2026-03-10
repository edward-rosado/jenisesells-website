using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.GetSession;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.GetSession;

public class GetSessionEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();

    [Fact]
    public async Task Handle_ValidId_ReturnsSession()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await GetSessionEndpoint.Handle(session.Id, _mockStore.Object, CancellationToken.None);

        var ok = Assert.IsType<Ok<GetSessionResponse>>(result);
        Assert.Equal(session.Id, ok.Value!.SessionId);
        Assert.Equal(OnboardingState.ScrapeProfile, ok.Value.CurrentState);
    }

    [Fact]
    public async Task Handle_InvalidId_Returns404()
    {
        _mockStore.Setup(s => s.LoadAsync("nope", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var result = await GetSessionEndpoint.Handle("nope", _mockStore.Object, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }
}
