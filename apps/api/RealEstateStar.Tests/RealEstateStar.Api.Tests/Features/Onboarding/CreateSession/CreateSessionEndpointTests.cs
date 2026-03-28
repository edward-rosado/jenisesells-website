using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Onboarding.CreateSession;

namespace RealEstateStar.Api.Tests.Features.Onboarding.CreateSession;

public class CreateSessionEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();

    [Fact]
    public async Task Handle_WithProfileUrl_CreatesSession()
    {
        _mockStore.Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new CreateSessionRequest { ProfileUrl = "https://zillow.com/profile/test" };
        var result = await CreateSessionEndpoint.Handle(request, _mockStore.Object, NullLogger<CreateSessionEndpoint>.Instance, CancellationToken.None);

        var ok = Assert.IsType<Ok<CreateSessionResponse>>(result);
        Assert.False(string.IsNullOrEmpty(ok.Value!.SessionId));
        _mockStore.Verify(s => s.SaveAsync(
            It.Is<OnboardingSession>(sess => sess.ProfileUrl == "https://zillow.com/profile/test"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutProfileUrl_CreatesSession()
    {
        _mockStore.Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new CreateSessionRequest { ProfileUrl = null };
        var result = await CreateSessionEndpoint.Handle(request, _mockStore.Object, NullLogger<CreateSessionEndpoint>.Instance, CancellationToken.None);

        var ok = Assert.IsType<Ok<CreateSessionResponse>>(result);
        Assert.False(string.IsNullOrEmpty(ok.Value!.SessionId));
    }
}
