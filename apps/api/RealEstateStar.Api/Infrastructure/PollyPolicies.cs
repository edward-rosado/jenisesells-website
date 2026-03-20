using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace RealEstateStar.Api.Infrastructure;

public static class PollyPolicies
{
    /// <summary>
    /// Claude API: 3x exponential retry (2s base) + circuit breaker (5 failures, 1 min pause).
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
                UseJitter = false,
                ShouldRetryAfterHeader = false,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "[LEAD-035] Claude API retry attempt {Attempt} after {Delay}ms. Outcome: {Outcome}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
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
                        "[LEAD-036] Claude API circuit breaker opened. Break duration: {BreakDuration}s. Outcome: {Outcome}",
                        args.BreakDuration.TotalSeconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("[LEAD-037] Claude API circuit breaker closed.");
                    return ValueTask.CompletedTask;
                }
            });
        });

        return builder;
    }

    /// <summary>
    /// ScraperAPI: 2x linear retry (1s) + circuit breaker (10 failures, 2 min pause).
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
                UseJitter = false,
                ShouldRetryAfterHeader = false,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "[LEAD-035] ScraperAPI retry attempt {Attempt} after {Delay}ms. Outcome: {Outcome}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
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
                        "[LEAD-036] ScraperAPI circuit breaker opened. Break duration: {BreakDuration}s. Outcome: {Outcome}",
                        args.BreakDuration.TotalSeconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("[LEAD-037] ScraperAPI circuit breaker closed.");
                    return ValueTask.CompletedTask;
                }
            });
        });

        return builder;
    }

    /// <summary>
    /// Google Chat: 1x retry (500ms), no circuit breaker.
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
                UseJitter = false,
                ShouldRetryAfterHeader = false,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "[LEAD-035] Google Chat retry attempt {Attempt} after {Delay}ms. Outcome: {Outcome}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return ValueTask.CompletedTask;
                }
            });
        });

        return builder;
    }
}
