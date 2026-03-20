using Microsoft.Extensions.Diagnostics.HealthChecks;
using RealEstateStar.Api.Features.Leads.Services;

namespace RealEstateStar.Api.Health;

/// <summary>
/// Reports health based on background worker activity relative to channel depth.
/// <list type="bullet">
///   <item>Healthy — channel is empty (worker idle) OR worker processed an item recently</item>
///   <item>Unhealthy — items are queued but worker hasn't processed anything within the staleness window</item>
/// </list>
/// </summary>
public sealed class BackgroundServiceHealthCheck(
    BackgroundServiceHealthTracker tracker,
    LeadProcessingChannel leadChannel,
    CmaProcessingChannel cmaChannel,
    HomeSearchProcessingChannel homeSearchChannel,
    ILogger<BackgroundServiceHealthCheck> logger) : IHealthCheck
{
    internal static readonly TimeSpan StalenessThreshold = TimeSpan.FromMinutes(5);

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var stuckWorkers = new List<string>();
        var data = new Dictionary<string, object>();

        CheckWorker("LeadProcessingWorker", leadChannel.Count, now, stuckWorkers, data);
        CheckWorker("CmaProcessingWorker", cmaChannel.Count, now, stuckWorkers, data);
        CheckWorker("HomeSearchProcessingWorker", homeSearchChannel.Count, now, stuckWorkers, data);

        if (stuckWorkers.Count > 0)
        {
            var description = $"Stuck workers: {string.Join("; ", stuckWorkers)}";
            logger.LogWarning("[HEALTH-WORKER-001] {Description}", description);
            return Task.FromResult(new HealthCheckResult(
                HealthStatus.Unhealthy, description, data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("All workers active or idle", data));
    }

    private void CheckWorker(
        string name, int queueDepth, DateTime now,
        List<string> stuckWorkers, Dictionary<string, object> data)
    {
        var lastActivity = tracker.GetLastActivity(name);

        data[$"{name}.queueDepth"] = queueDepth;
        data[$"{name}.lastActivity"] = lastActivity?.ToString("o") ?? "never";

        if (queueDepth <= 0)
            return; // Channel empty — healthy idle

        if (lastActivity is null)
        {
            stuckWorkers.Add($"{name}: {queueDepth} queued, never active");
            return;
        }

        var idle = now - lastActivity.Value;
        if (idle > StalenessThreshold)
            stuckWorkers.Add($"{name}: {queueDepth} queued, idle {idle.TotalSeconds:F0}s");
    }
}
