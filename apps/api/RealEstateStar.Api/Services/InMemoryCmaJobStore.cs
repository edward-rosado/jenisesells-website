using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.Cma;

namespace RealEstateStar.Api.Services;

public class InMemoryCmaJobStore(IMemoryCache cache, ILogger<InMemoryCmaJobStore> logger) : ICmaJobStore
{
    private static readonly TimeSpan JobTtl = TimeSpan.FromHours(24);

    private static readonly MemoryCacheEntryOptions CacheEntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = JobTtl,
        Size = 1
    };

    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _agentJobs = new();

    public CmaJob? Get(string jobId) =>
        cache.TryGetValue(jobId, out CmaJob? job) ? job : null;

    public void Set(string agentId, CmaJob job)
    {
        var jobId = job.Id.ToString();
        cache.Set(jobId, job, CacheEntryOptions);

        _agentJobs.AddOrUpdate(
            agentId,
            _ => [jobId],
            (_, bag) =>
            {
                if (!bag.Contains(jobId))
                    bag.Add(jobId);
                return bag;
            });

        logger.LogDebug("Stored job {JobId} for agent {AgentId}", jobId, agentId);
    }

    public IEnumerable<CmaJob> GetByAgent(string agentId) =>
        _agentJobs.TryGetValue(agentId, out var jobIds)
            ? jobIds.Select(id => Get(id)).OfType<CmaJob>()
            : [];
}
