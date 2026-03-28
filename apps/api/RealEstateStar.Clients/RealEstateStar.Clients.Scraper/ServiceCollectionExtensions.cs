using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Scraper;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScraperClient(this IServiceCollection services, IConfiguration configuration, ILogger pollyLogger)
    {
        services.Configure<ScraperOptions>(configuration.GetSection("Scraper"));
        services.AddSingleton<IScraperClient, ScraperClient>();
        services.AddHttpClient("ScraperAPI")
            .AddResilienceHandler("scraper-api", pipeline =>
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
                        pollyLogger.LogError(
                            "[SCRAPER-002] ScraperAPI circuit OPEN for {BreakDurationSec}s. Last error: {Error}",
                            args.BreakDuration.TotalSeconds,
                            args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = _ =>
                    {
                        pollyLogger.LogInformation("[SCRAPER-003] ScraperAPI circuit CLOSED — resuming normal traffic.");
                        return ValueTask.CompletedTask;
                    }
                });
            });
        return services;
    }
}
