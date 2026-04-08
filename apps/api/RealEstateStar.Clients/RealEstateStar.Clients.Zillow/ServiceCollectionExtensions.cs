using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Zillow;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddZillowClient(this IServiceCollection services, IConfiguration configuration, ILogger pollyLogger)
    {
        services.Configure<ZillowOptions>(configuration.GetSection("Zillow"));
        services.AddSingleton<IZillowReviewsClient, ZillowReviewsClient>();
        services.AddHttpClient("ZillowAPI")
            .AddResilienceHandler("zillow-api", pipeline =>
            {
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 2,
                    BackoffType = DelayBackoffType.Linear,
                    Delay = TimeSpan.FromSeconds(1),
                    UseJitter = true,
                    OnRetry = args =>
                    {
                        pollyLogger.LogWarning(
                            "[ZILLOW-040] Zillow API retry {Attempt}/{MaxAttempts} after {DelayMs}ms. Status: {Status}. Error: {Error}",
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
                    MinimumThroughput = 5,
                    SamplingDuration = TimeSpan.FromSeconds(60),
                    BreakDuration = TimeSpan.FromMinutes(5),
                    OnOpened = args =>
                    {
                        pollyLogger.LogError(
                            "[ZILLOW-041] Zillow API circuit OPEN for {BreakDurationSec}s. Last error: {Error}",
                            args.BreakDuration.TotalSeconds,
                            args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = _ =>
                    {
                        pollyLogger.LogInformation("[ZILLOW-042] Zillow API circuit CLOSED — resuming normal traffic.");
                        return ValueTask.CompletedTask;
                    }
                });
            });
        return services;
    }
}
