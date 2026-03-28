using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using RealEstateStar.Domain.Cma.Interfaces;

namespace RealEstateStar.Clients.RentCast;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRentCastClient(this IServiceCollection services, IConfiguration configuration, ILogger pollyLogger)
    {
        services.Configure<RentCastOptions>(configuration.GetSection("RentCast"));
        services.AddSingleton<IRentCastClient, RentCastClient>();
        services.AddHttpClient("RentCast")
            .AddResilienceHandler("rentcast-api", pipeline =>
            {
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 1,
                    BackoffType = DelayBackoffType.Constant,
                    Delay = TimeSpan.FromSeconds(5),
                    UseJitter = true,
                    OnRetry = args =>
                    {
                        pollyLogger.LogWarning(
                            "[RENTCAST-030] RentCast retry {Attempt}/{MaxAttempts} after {DelayMs}ms. Status: {Status}. Error: {Error}",
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
                    BreakDuration = TimeSpan.FromMinutes(1),
                    OnOpened = args =>
                    {
                        pollyLogger.LogError(
                            "[RENTCAST-031] RentCast API circuit OPEN for {BreakDurationSec}s. Last error: {Error}",
                            args.BreakDuration.TotalSeconds,
                            args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = _ =>
                    {
                        pollyLogger.LogInformation("[RENTCAST-032] RentCast API circuit CLOSED — resuming normal traffic.");
                        return ValueTask.CompletedTask;
                    }
                });
            });
        return services;
    }
}
