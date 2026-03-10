using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class DomainServiceTests
{
    private readonly DomainService _service = new(NullLogger<DomainService>.Instance);

    [Fact]
    public async Task ValidateDnsAsync_ReturnsTrue()
    {
        var result = await _service.ValidateDnsAsync("janedoe.com", CancellationToken.None);
        Assert.True(result);
    }

    [Fact]
    public async Task ConfigureCustomDomainAsync_SetsSessionDomain()
    {
        var session = OnboardingSession.Create(null);

        var result = await _service.ConfigureCustomDomainAsync(session, "janedoe.com", CancellationToken.None);

        Assert.Equal("janedoe.com", session.CustomDomain);
        Assert.Contains("janedoe.com", result);
    }
}
