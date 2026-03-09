using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services;

public class InMemoryCmaJobStore(ILogger<InMemoryCmaJobStore> logger) : ICmaJobStore, IDisposable
{
    private static readonly TimeSpan JobTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, CmaJob> _jobs = new();
    private readonly ConcurrentDictionary<string, List<string>> _agentJobs = new();
    private Timer? _evictionTimer;
    private bool _disposed;

    internal void StartEviction() =>
        _evictionTimer ??= new Timer(_ => EvictExpiredJobs(), null, SweepInterval, SweepInterval);

    public CmaJob? Get(string jobId) =>
        _jobs.GetValueOrDefault(jobId);

    public void Set(string agentId, CmaJob job)
    {
        StartEviction();

        var jobId = job.Id.ToString();
        _jobs[jobId] = job;
        _agentJobs.AddOrUpdate(
            agentId,
            _ => [jobId],
            (_, list) => { lock (list) { if (!list.Contains(jobId)) list.Add(jobId); } return list; });
    }

    public IEnumerable<CmaJob> GetByAgent(string agentId) =>
        _agentJobs.TryGetValue(agentId, out var jobIds)
            ? jobIds.Select(id => _jobs.GetValueOrDefault(id)).Where(j => j is not null)!
            : [];

    internal void EvictExpiredJobs()
    {
        var cutoff = DateTime.UtcNow - JobTtl;
        var evicted = 0;

        foreach (var kvp in _jobs)
        {
            if (kvp.Value.CreatedAt >= cutoff)
                continue;

            if (!_jobs.TryRemove(kvp.Key, out var job))
                continue;

            evicted++;
            var agentId = job.AgentId.ToString();

            if (_agentJobs.TryGetValue(agentId, out var jobIds))
                lock (jobIds) { jobIds.Remove(kvp.Key); }
        }

        if (evicted > 0)
            logger.LogInformation("Evicted {Count} expired jobs from in-memory store", evicted);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _evictionTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
