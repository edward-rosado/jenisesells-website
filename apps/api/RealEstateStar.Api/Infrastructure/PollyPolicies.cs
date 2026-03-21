using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace RealEstateStar.Api.Infrastructure;

public static class PollyPolicies
{
    /// <summary>
    /// Claude API: 3x exponential retry (2s base, jitter) + circuit breaker (5 failures / 60s → 1 min break).
    /// Log codes: [CLAUDE-001] retry, [CLAUDE-002] CB opened, [CLAUDE-003] CB closed.
    /// </summary>
    public static IHttpClientBuilder AddClaudeApiResilience(this IHttpClientBuilder builder, ILogger logger)
    {
        builder.AddResilienceHandler("claude-api", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "[CLAUDE-001] Claude API retry {Attempt}/{MaxAttempts} after {DelayMs}ms. Status: {Status}. Error: {Error}",
                        args.AttemptNumber + 1, 3,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result?.StatusCode,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(60),
                BreakDuration = TimeSpan.FromMinutes(1),
                OnOpened = args =>
                {
                    logger.LogError(
                        "[CLAUDE-002] Claude API circuit OPEN for {BreakDurationSec}s. Last error: {Error}",
                        args.BreakDuration.TotalSeconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("[CLAUDE-003] Claude API circuit CLOSED — resuming normal traffic.");
                    return ValueTask.CompletedTask;
                }
            });
        });

        return builder;
    }

    /// <summary>
    /// ScraperAPI: 2x linear retry (1s, jitter) + circuit breaker (10 failures / 120s → 2 min break).
    /// Log codes: [SCRAPER-001] retry, [SCRAPER-002] CB opened, [SCRAPER-003] CB closed.
    /// </summary>
    public static IHttpClientBuilder AddScraperApiResilience(this IHttpClientBuilder builder, ILogger logger)
    {
        builder.AddResilienceHandler("scraper-api", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Linear,
                Delay = TimeSpan.FromSeconds(1),
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "[SCRAPER-001] ScraperAPI retry {Attempt}/{MaxAttempts} after {DelayMs}ms. Status: {Status}. Error: {Error}",
                        args.AttemptNumber + 1, 2,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result?.StatusCode,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(120),
                BreakDuration = TimeSpan.FromMinutes(2),
                OnOpened = args =>
                {
                    logger.LogError(
                        "[SCRAPER-002] ScraperAPI circuit OPEN for {BreakDurationSec}s. Last error: {Error}",
                        args.BreakDuration.TotalSeconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("[SCRAPER-003] ScraperAPI circuit CLOSED — resuming normal traffic.");
                    return ValueTask.CompletedTask;
                }
            });
        });

        return builder;
    }

    /// <summary>
    /// Google Workspace (Drive/Gmail): 3x exponential retry (1s base, jitter) + circuit breaker (5 failures / 120s → 2 min break).
    /// GWS calls are critical for CMA delivery and home search storage.
    /// Log codes: [GWS-001] retry, [GWS-002] CB opened, [GWS-003] CB closed.
    /// </summary>
    public static IHttpClientBuilder AddGwsResilience(this IHttpClientBuilder builder, ILogger logger)
    {
        builder.AddResilienceHandler("gws", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "[GWS-001] GWS retry {Attempt}/{MaxAttempts} after {DelayMs}ms. Status: {Status}. Error: {Error}",
                        args.AttemptNumber + 1, 3,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result?.StatusCode,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(120),
                BreakDuration = TimeSpan.FromMinutes(2),
                OnOpened = args =>
                {
                    logger.LogError(
                        "[GWS-002] GWS circuit OPEN for {BreakDurationSec}s. Last error: {Error}",
                        args.BreakDuration.TotalSeconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("[GWS-003] GWS circuit CLOSED — resuming normal traffic.");
                    return ValueTask.CompletedTask;
                }
            });
        });

        return builder;
    }

    /// <summary>
    /// Google Chat webhook: 1x retry (500ms, jitter) + circuit breaker (5 failures / 60s → 30s break).
    /// Chat is best-effort notification — short break keeps the pipeline moving.
    /// Log codes: [GCHAT-001] retry, [GCHAT-002] CB opened, [GCHAT-003] CB closed.
    /// </summary>
    public static IHttpClientBuilder AddGoogleChatResilience(this IHttpClientBuilder builder, ILogger logger)
    {
        builder.AddResilienceHandler("google-chat", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromMilliseconds(500),
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "[GCHAT-001] Google Chat retry {Attempt}/{MaxAttempts} after {DelayMs}ms. Status: {Status}. Error: {Error}",
                        args.AttemptNumber + 1, 1,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result?.StatusCode,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(60),
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    logger.LogError(
                        "[GCHAT-002] Google Chat circuit OPEN for {BreakDurationSec}s. Last error: {Error}",
                        args.BreakDuration.TotalSeconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("[GCHAT-003] Google Chat circuit CLOSED — resuming normal traffic.");
                    return ValueTask.CompletedTask;
                }
            });
        });

        return builder;
    }
}
