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
using RealEstateStar.Workers.Onboarding;
using RealEstateStar.Workers.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Notifications.WhatsApp;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Onboarding.CreateSession;

namespace RealEstateStar.Api.Tests.Features.Onboarding.CreateSession;

public class CreateSessionEndpointTests
{
    private readonly Mock<ISessionDataService> _mockStore = new();

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
