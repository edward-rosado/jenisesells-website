using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class TrialExpiryTests
{
    [Fact]
    public async Task Service_StopsGracefully_OnCancellation()
    {
        var mockStore = new Mock<ISessionStore>();
        var stripeService = new StripeService(NullLogger<StripeService>.Instance);
        var service = new TrialExpiryService(
            mockStore.Object, stripeService, NullLogger<TrialExpiryService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await service.StartAsync(cts.Token);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        // Service should stop without throwing
        Assert.True(true);
    }
}
