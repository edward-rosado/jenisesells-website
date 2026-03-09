using System.Collections.Concurrent;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services;

public class InMemoryCmaJobStore : ICmaJobStore
{
    private readonly ConcurrentDictionary<string, CmaJob> _jobs = new();
    private readonly ConcurrentDictionary<string, List<string>> _agentJobs = new();

    public CmaJob? Get(string jobId) =>
        _jobs.GetValueOrDefault(jobId);

    public void Set(string agentId, CmaJob job)
    {
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
}
