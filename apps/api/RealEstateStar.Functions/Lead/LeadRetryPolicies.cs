using Microsoft.DurableTask;

namespace RealEstateStar.Functions.Lead;

/// <summary>
/// Retry policies for lead pipeline activities.
///
/// Lead processing is triggered per-lead and must complete reliably.
/// All activities call external APIs (Claude, Gmail, Drive) that are subject to
/// transient failures (rate limits, timeouts, token refresh races).
/// </summary>
internal static class LeadRetryPolicies
{
    /// <summary>
    /// Standard retry for most lead activities (config load, scoring, cache check, CMA, HomeSearch, PDF).
    /// 3 attempts, 15s initial delay, 2x backoff → 15s, 30s.
    /// </summary>
    public static readonly TaskOptions Standard = TaskOptions.FromRetryPolicy(new RetryPolicy(
        maxNumberOfAttempts: 3,
        firstRetryInterval: TimeSpan.FromSeconds(15),
        backoffCoefficient: 2.0));

    /// <summary>
    /// Email delivery retry (Gmail send).
    /// 2 attempts, 30s delay — allows OAuth token refresh between attempts.
    /// </summary>
    public static readonly TaskOptions EmailDelivery = TaskOptions.FromRetryPolicy(new RetryPolicy(
        maxNumberOfAttempts: 2,
        firstRetryInterval: TimeSpan.FromSeconds(30),
        backoffCoefficient: 2.0));
}
