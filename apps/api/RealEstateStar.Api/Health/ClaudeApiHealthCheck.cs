using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealEstateStar.Api.Health;

public class ClaudeApiHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync("https://api.anthropic.com/v1/models", ct);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Claude API reachable")
                : HealthCheckResult.Degraded($"Claude API returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Claude API unreachable", ex)).Result;
        }
    }
}
