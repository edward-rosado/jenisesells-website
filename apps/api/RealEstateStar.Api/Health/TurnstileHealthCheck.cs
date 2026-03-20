using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealEstateStar.Api.Health;

public class TurnstileHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        var secretKey = configuration["Turnstile:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
            return Task.FromResult(HealthCheckResult.Degraded("Turnstile secret key not configured"));

        return Task.FromResult(HealthCheckResult.Healthy("Turnstile secret key configured"));
    }
}
