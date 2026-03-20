using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealEstateStar.Api.Health;

public class ScraperApiHealthCheck(IConfiguration configuration, IHttpClientFactory _) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        var apiKey = configuration["ScraperApi:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return Task.FromResult(HealthCheckResult.Degraded("ScraperAPI key not configured"));

        return Task.FromResult(HealthCheckResult.Healthy("ScraperAPI key configured"));
    }
}
