using Azure.Storage.Queues;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealEstateStar.Api.Health;

/// <summary>
/// Checks connectivity to both Azure Queue Storage queues (activation-requests and lead-requests).
/// Returns Healthy if both are reachable, Degraded if one fails, Unhealthy if both fail.
/// </summary>
public sealed class AzureQueueHealthCheck(
    IConfiguration configuration,
    ILogger<AzureQueueHealthCheck> logger) : IHealthCheck
{
    private static readonly string[] QueueNames = ["activation-requests", "lead-requests"];

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        var connectionString = configuration["AzureStorage:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Degraded(
                "AzureStorage:ConnectionString not configured — using in-memory queues");
        }

        var failures = new List<string>();
        var data = new Dictionary<string, object>();

        foreach (var queueName in QueueNames)
        {
            try
            {
                var client = new QueueClient(
                    connectionString,
                    queueName,
                    new QueueClientOptions
                    {
                        MessageEncoding = QueueMessageEncoding.Base64
                    });

                var response = await client.PeekMessagesAsync(maxMessages: 1, ct);
                data[$"{queueName}.reachable"] = true;
                data[$"{queueName}.peeked"] = response.Value.Length;
            }
            catch (Exception ex)
            {
                failures.Add(queueName);
                data[$"{queueName}.reachable"] = false;
                data[$"{queueName}.error"] = ex.Message;

                logger.LogWarning(ex,
                    "[QUEUE-HC-001] Azure queue health check failed for {QueueName}",
                    queueName);
            }
        }

        return failures.Count switch
        {
            0 => HealthCheckResult.Healthy("All Azure queues reachable", data),
            var n when n < QueueNames.Length => HealthCheckResult.Degraded(
                $"Azure queue(s) unreachable: {string.Join(", ", failures)}", data: data),
            _ => HealthCheckResult.Unhealthy(
                "All Azure queues unreachable", data: data)
        };
    }
}
