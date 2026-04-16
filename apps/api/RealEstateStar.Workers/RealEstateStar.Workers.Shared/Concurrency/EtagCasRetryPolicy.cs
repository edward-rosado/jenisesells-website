using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Shared.Concurrency;

/// <summary>
/// Executes a compare-and-swap (CAS) operation with exponential backoff and jitter.
/// Designed for ETag-based optimistic concurrency updates (e.g. Azure Table Storage).
/// </summary>
public static class EtagCasRetryPolicy
{
    private const int BaseDelayMs = 50;
    private const int MaxDelayMs = 2000;
    private const int JitterMs = 50;

    public static async Task<CasOutcome> ExecuteAsync(
        int maxAttempts,
        Func<CancellationToken, Task<CasAttemptResult>> attemptFn,
        ILogger logger,
        string component,
        CancellationToken ct)
    {
        CasAttemptResult result = default;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            result = await attemptFn(ct).ConfigureAwait(false);

            if (result.Committed)
            {
                return new CasOutcome(Succeeded: true, AttemptCount: attempt, FailureReason: null);
            }

            if (!result.ShouldRetry)
            {
                return new CasOutcome(Succeeded: false, AttemptCount: attempt, FailureReason: result.Reason);
            }

            if (attempt < maxAttempts)
            {
                logger.LogWarning(
                    "[CAS-010] {Component} CAS conflict on attempt {Attempt}/{Max}: {Reason}",
                    component, attempt, maxAttempts, result.Reason);

                int backoffMs = Math.Min(BaseDelayMs * (1 << (attempt - 1)), MaxDelayMs);
                int jitter = Random.Shared.Next(0, JitterMs + 1);
                await Task.Delay(backoffMs + jitter, ct).ConfigureAwait(false);
            }
        }

        logger.LogError(
            "[CAS-020] {Component} CAS retries exhausted after {Max} attempts: {Reason}",
            component, maxAttempts, result.Reason);

        return new CasOutcome(Succeeded: false, AttemptCount: maxAttempts, FailureReason: result.Reason);
    }
}
