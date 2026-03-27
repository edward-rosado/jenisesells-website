using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealEstateStar.Api.Health;

public sealed class RentCastHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        var apiKey = configuration["RentCast:ApiKey"];
        return string.IsNullOrWhiteSpace(apiKey)
            ? Task.FromResult(HealthCheckResult.Degraded("RentCast API key not configured"))
            : Task.FromResult(HealthCheckResult.Healthy("RentCast API key configured"));
    }
}
