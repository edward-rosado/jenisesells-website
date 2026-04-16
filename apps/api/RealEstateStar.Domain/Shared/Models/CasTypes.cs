namespace RealEstateStar.Domain.Shared.Models;

/// <summary>
/// Result of a single CAS (compare-and-swap) attempt.
/// Returned by the user-supplied lambda in EtagCasRetryPolicy.ExecuteAsync.
/// </summary>
public readonly record struct CasAttemptResult(
    bool Committed,
    bool ShouldRetry,
    string? Reason);

/// <summary>
/// Final outcome after all CAS retry attempts.
/// </summary>
public readonly record struct CasOutcome(
    bool Succeeded,
    int AttemptCount,
    string? FailureReason);
