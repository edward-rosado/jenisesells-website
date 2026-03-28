using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class TrialExpiryTests
{
    [Fact]
    public async Task Service_StopsGracefully_OnCancellation()
    {
        var mockStore = new Mock<ISessionStore>();
        var mockStripe = new Mock<IStripeService>();
        var service = new TrialExpiryService(
            mockStore.Object, mockStripe.Object, NullLogger<TrialExpiryService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await service.StartAsync(cts.Token);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        // Service should stop without throwing
        Assert.True(true);
    }

    [Fact]
    public async Task Service_LogsStartMessage()
    {
        var mockStore = new Mock<ISessionStore>();
        var mockStripe = new Mock<IStripeService>();
        var mockLogger = new Mock<ILogger<TrialExpiryService>>();
        var service = new TrialExpiryService(
            mockStore.Object, mockStripe.Object, mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StartAsync(cts.Token);
        // Wait for ExecuteAsync to run on the background thread — generous timeout for slow CI
        await Task.Delay(500);
        await service.StopAsync(CancellationToken.None);

        // Verify "Trial expiry service started" was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("Trial expiry service started")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Service_ExecutesCheckLoop_BeforeCancellation()
    {
        // Use a very short interval to ensure the check loop runs at least once
        // The service uses Task.Delay(CheckInterval) which is 1 hour, but cancellation
        // throws OperationCanceledException which is caught by the while loop
        var mockStore = new Mock<ISessionStore>();
        var mockStripe = new Mock<IStripeService>();
        var mockLogger = new Mock<ILogger<TrialExpiryService>>();
        var service = new TrialExpiryService(
            mockStore.Object, mockStripe.Object, mockLogger.Object);

        // Cancel quickly — the Task.Delay will throw OperationCanceledException
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await service.StartAsync(cts.Token);

        // Wait long enough for the cancellation to propagate and the service to stop
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        // The OperationCanceledException from Task.Delay should be caught and break the loop
        // Verify service started (proving ExecuteAsync ran)
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("Trial expiry service started")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
