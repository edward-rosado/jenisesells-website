using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealEstateStar.Api.Health;

/// <summary>
/// Reports health based on whether the Azure Functions host is reachable.
/// Queries the Functions app health endpoint when <c>AzureFunctions:HealthUrl</c> is configured;
/// returns Healthy with a "not configured" message for local development.
/// </summary>
/// <remarks>
/// Registered under the <c>"workers"</c> health tag to maintain the same /health/workers
/// endpoint contract as the removed <c>BackgroundServiceHealthCheck</c>.
/// </remarks>
public sealed class DurableFunctionsHealthCheck(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<DurableFunctionsHealthCheck> logger) : IHealthCheck
{
    internal const string HttpClientName = "durable-functions-health";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        var healthUrl = configuration["AzureFunctions:HealthUrl"];

        if (string.IsNullOrEmpty(healthUrl))
        {
            return HealthCheckResult.Healthy(
                "Azure Functions not configured (local development — functions running separately)",
                data: new Dictionary<string, object>
                {
                    ["configured"] = false
                });
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetAsync(healthUrl, ct);

            var data = new Dictionary<string, object>
            {
                ["configured"] = true,
                ["statusCode"] = (int)response.StatusCode,
                ["url"] = healthUrl
            };

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Azure Functions host is healthy", data: data);
            }

            var description = $"Azure Functions host returned {(int)response.StatusCode} {response.ReasonPhrase}";
            logger.LogWarning("[HEALTH-DF-001] {Description}", description);
            return new HealthCheckResult(context.Registration.FailureStatus, description, data: data);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var description = $"Azure Functions host unreachable: {ex.Message}";
            logger.LogError(ex, "[HEALTH-DF-002] {Description}", description);
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                description,
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["configured"] = true,
                    ["url"] = healthUrl
                });
        }
    }
}
