using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;

namespace RealEstateStar.Clients.WhatsApp;

/// <summary>
/// Resilience policies for the WhatsApp "Graph API" HttpClient.
/// Meta's Cloud API returns 429 on rate-limit and 5xx on transient failures.
///
/// Policy stack:
///   1. Retry — 3 attempts, exponential backoff (2s base) + jitter, honours Retry-After header
///   2. Circuit breaker — opens after 5 consecutive failures in 60s, pauses for 30s
/// </summary>
public static class WhatsAppResiliencePolicies
{
    /// <summary>
    /// Adds retry + circuit-breaker resilience to the "WhatsApp" named HttpClient.
    /// Overload accepting a logger directly — used by tests and cross-branch consistency.
    /// </summary>
    public static IHttpClientBuilder AddWhatsAppResilience(this IHttpClientBuilder builder, ILogger logger)
    {
        builder.AddResilienceHandler("whatsapp-api", pipeline =>
        {
            ConfigureRetry(pipeline, logger);
            ConfigureCircuitBreaker(pipeline, logger);
        });

        return builder;
    }

    /// <summary>
    /// Adds retry + circuit-breaker resilience, resolving the logger from DI.
    /// Preferred for Program.cs registration — avoids BuildServiceProvider().
    /// </summary>
    public static IHttpClientBuilder AddWhatsAppResilience(this IHttpClientBuilder builder)
    {
        builder.AddResilienceHandler("whatsapp-api", (pipeline, context) =>
        {
            var logger = context.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("WhatsAppResilience");

            ConfigureRetry(pipeline, logger);
            ConfigureCircuitBreaker(pipeline, logger);
        });

        return builder;
    }

    private static void ConfigureRetry(ResiliencePipelineBuilder<HttpResponseMessage> pipeline, ILogger logger)
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(2),
            UseJitter = true,
            ShouldRetryAfterHeader = true,
            ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result?.StatusCode is
                    System.Net.HttpStatusCode.TooManyRequests or
                    System.Net.HttpStatusCode.InternalServerError or
                    System.Net.HttpStatusCode.BadGateway or
                    System.Net.HttpStatusCode.ServiceUnavailable or
                    System.Net.HttpStatusCode.GatewayTimeout
                || args.Outcome.Exception is HttpRequestException or TaskCanceledException),
            OnRetry = args =>
            {
                logger.LogWarning(
                    "[WA-030] WhatsApp API retry attempt {Attempt} after {Delay}ms. Status: {Status}. Error: {Error}",
                    args.AttemptNumber + 1,
                    args.RetryDelay.TotalMilliseconds,
                    args.Outcome.Result?.StatusCode.ToString() ?? "n/a",
                    args.Outcome.Exception?.Message ?? "none");
                return ValueTask.CompletedTask;
            }
        });
    }

    private static void ConfigureCircuitBreaker(ResiliencePipelineBuilder<HttpResponseMessage> pipeline, ILogger logger)
    {
        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 1.0,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(60),
            BreakDuration = TimeSpan.FromSeconds(30),
            OnOpened = args =>
            {
                logger.LogError(
                    "[WA-031] WhatsApp API circuit breaker OPENED for {BreakDuration}s. Last error: {Error}",
                    args.BreakDuration.TotalSeconds,
                    args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                logger.LogInformation("[WA-032] WhatsApp API circuit breaker closed — calls resuming.");
                return ValueTask.CompletedTask;
            }
        });
    }
}
