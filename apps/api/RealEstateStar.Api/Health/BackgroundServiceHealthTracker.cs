using System.Collections.Concurrent;

namespace RealEstateStar.Api.Health;

/// <summary>
/// Tracks background service activity for health reporting.
/// Each worker calls <see cref="RecordActivity"/> after processing an item.
/// The health check reads last-activity timestamps to detect stuck workers.
/// </summary>
public sealed class BackgroundServiceHealthTracker
{
    private readonly ConcurrentDictionary<string, DateTime> _lastActivity = new();

    /// <summary>
    /// Called by a worker after successfully processing an item.
    /// </summary>
    public void RecordActivity(string workerName) =>
        RecordActivity(workerName, DateTime.UtcNow);

    /// <summary>
    /// Records activity with a specific timestamp (for testing).
    /// </summary>
    internal void RecordActivity(string workerName, DateTime utcTimestamp) =>
        _lastActivity[workerName] = utcTimestamp;

    /// <summary>
    /// Returns when the worker last processed an item, or null if it has never processed.
    /// </summary>
    public DateTime? GetLastActivity(string workerName) =>
        _lastActivity.TryGetValue(workerName, out var ts) ? ts : null;
}
