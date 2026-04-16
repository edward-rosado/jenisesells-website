using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Workers.Shared.Concurrency;

namespace RealEstateStar.Workers.Shared.Tests.Concurrency;

public class EtagCasRetryPolicyTests
{
    private static readonly NullLogger<EtagCasRetryPolicyTests> Logger =
        NullLogger<EtagCasRetryPolicyTests>.Instance;

    [Fact]
    public async Task ExecuteAsync_SuccessOnFirstAttempt_ReturnsSucceededWithAttemptCountOne()
    {
        var callCount = 0;

        var outcome = await EtagCasRetryPolicy.ExecuteAsync(
            maxAttempts: 5,
            attemptFn: _ =>
            {
                callCount++;
                return Task.FromResult(new CasAttemptResult(Committed: true, ShouldRetry: false, Reason: null));
            },
            logger: Logger,
            component: "TestComponent",
            ct: CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.Equal(1, outcome.AttemptCount);
        Assert.Null(outcome.FailureReason);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessOnThirdAttemptAfterTwoConflicts_ReturnsSucceededWithAttemptCountThree()
    {
        var callCount = 0;

        var outcome = await EtagCasRetryPolicy.ExecuteAsync(
            maxAttempts: 5,
            attemptFn: _ =>
            {
                callCount++;
                if (callCount < 3)
                {
                    return Task.FromResult(new CasAttemptResult(Committed: false, ShouldRetry: true, Reason: "ETag conflict"));
                }

                return Task.FromResult(new CasAttemptResult(Committed: true, ShouldRetry: false, Reason: null));
            },
            logger: Logger,
            component: "TestComponent",
            ct: CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.Equal(3, outcome.AttemptCount);
        Assert.Null(outcome.FailureReason);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustsAllAttempts_ReturnsFailedWithFailureReasonSet()
    {
        const int maxAttempts = 3;
        var callCount = 0;

        var outcome = await EtagCasRetryPolicy.ExecuteAsync(
            maxAttempts: maxAttempts,
            attemptFn: _ =>
            {
                callCount++;
                return Task.FromResult(new CasAttemptResult(Committed: false, ShouldRetry: true, Reason: "persistent conflict"));
            },
            logger: Logger,
            component: "TestComponent",
            ct: CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal(maxAttempts, outcome.AttemptCount);
        Assert.Equal("persistent conflict", outcome.FailureReason);
        Assert.Equal(maxAttempts, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRetryFalse_ExitsAfterFirstAttemptWithoutRetrying()
    {
        var callCount = 0;

        var outcome = await EtagCasRetryPolicy.ExecuteAsync(
            maxAttempts: 5,
            attemptFn: _ =>
            {
                callCount++;
                return Task.FromResult(new CasAttemptResult(Committed: false, ShouldRetry: false, Reason: "precondition failed"));
            },
            logger: Logger,
            component: "TestComponent",
            ct: CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal(1, outcome.AttemptCount);
        Assert.Equal("precondition failed", outcome.FailureReason);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            EtagCasRetryPolicy.ExecuteAsync(
                maxAttempts: 5,
                attemptFn: _ => Task.FromResult(new CasAttemptResult(Committed: false, ShouldRetry: true, Reason: "conflict")),
                logger: Logger,
                component: "TestComponent",
                ct: cts.Token));
    }
}
