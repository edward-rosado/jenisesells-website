using Microsoft.DurableTask;

namespace RealEstateStar.Functions.Activation;

/// <summary>
/// Retry policies for activation pipeline activities, classified by phase.
///
/// Phase 1 (Gather): External APIs (Gmail, Drive, Scraper) — transient failures likely.
/// Phase 2 (Synthesize): Claude API — rate limits + timeouts.
/// Phase 3 (Persist): Storage writes — transient blob/drive failures.
/// Phase 4 (Notify): Gmail send — token refresh + delivery failures.
///
/// Activities are classified as FATAL (propagate failure) or BEST-EFFORT (catch + continue):
/// - FATAL: EmailFetch, DriveIndex, EmailTransactionExtraction, CheckActivationComplete, PersistProfile
/// - BEST-EFFORT: EmailClassification, all Phase 2 workers, SynthesisMerge, ContactDetection,
///   BrandMerge, ContactImport, CleanupStagedContent, WelcomeNotification
/// </summary>
internal static class ActivationRetryPolicies
{
    /// <summary>
    /// Phase 1: External API calls (Gmail, Drive, Scraper).
    /// 3 attempts, 30s initial delay, 2x backoff → 30s, 60s, 120s.
    /// </summary>
    public static readonly TaskOptions Gather = TaskOptions.FromRetryPolicy(new RetryPolicy(
        maxNumberOfAttempts: 3,
        firstRetryInterval: TimeSpan.FromSeconds(30),
        backoffCoefficient: 2.0));

    /// <summary>
    /// Phase 2: Claude API synthesis calls.
    /// 2 attempts, 15s initial delay → 15s, 30s.
    /// Lower retry count since Claude rate limits have longer recovery windows.
    /// </summary>
    public static readonly TaskOptions Synthesis = TaskOptions.FromRetryPolicy(new RetryPolicy(
        maxNumberOfAttempts: 2,
        firstRetryInterval: TimeSpan.FromSeconds(15),
        backoffCoefficient: 2.0));

    /// <summary>
    /// Phase 3: Storage writes (Azure Blob, Google Drive fan-out).
    /// 3 attempts, 10s initial delay → 10s, 20s, 40s.
    /// </summary>
    public static readonly TaskOptions Persist = TaskOptions.FromRetryPolicy(new RetryPolicy(
        maxNumberOfAttempts: 3,
        firstRetryInterval: TimeSpan.FromSeconds(10),
        backoffCoefficient: 2.0));

    /// <summary>
    /// Phase 4: Notification delivery (Gmail send).
    /// 2 attempts, 30s initial delay → 30s, 60s.
    /// </summary>
    public static readonly TaskOptions Notify = TaskOptions.FromRetryPolicy(new RetryPolicy(
        maxNumberOfAttempts: 2,
        firstRetryInterval: TimeSpan.FromSeconds(30),
        backoffCoefficient: 2.0));
}
