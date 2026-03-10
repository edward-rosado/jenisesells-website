using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class StripeServiceTests
{
    private readonly StripeService _service = new(NullLogger<StripeService>.Instance);

    [Fact]
    public async Task CreateSetupIntentAsync_ReturnsIntentId()
    {
        var session = OnboardingSession.Create(null);

        var intentId = await _service.CreateSetupIntentAsync(session, CancellationToken.None);

        Assert.NotNull(intentId);
        Assert.Contains(session.Id, intentId);
        Assert.Equal(intentId, session.StripeSetupIntentId);
    }

    [Fact]
    public async Task ChargeAsync_ReturnsTrue()
    {
        var result = await _service.ChargeAsync("seti_test", 900m, CancellationToken.None);

        Assert.True(result);
    }
}
