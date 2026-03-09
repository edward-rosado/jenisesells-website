using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Tests.Services;

public class InMemoryCmaJobStoreTests : IDisposable
{
    private readonly InMemoryCmaJobStore _store = new(Mock.Of<ILogger<InMemoryCmaJobStore>>());

    private static Lead MakeLead() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-1234",
        Address = "123 Main St",
        City = "Old Bridge",
        State = "NJ",
        Zip = "08857",
        Timeline = "3-6 months",
        Beds = 3,
        Baths = 2,
        Sqft = 1800
    };

    private static CmaJob MakeJob(DateTime? createdAt = null)
    {
        var job = CmaJob.Create(Guid.NewGuid(), MakeLead());

        if (createdAt.HasValue)
        {
            // Use reflection to set the init-only CreatedAt for testing
            typeof(CmaJob)
                .GetProperty(nameof(CmaJob.CreatedAt))!
                .SetValue(job, createdAt.Value);
        }

        return job;
    }

    [Fact]
    public void EvictExpiredJobs_RemovesJobsOlderThan24Hours()
    {
        var agentId = Guid.NewGuid().ToString();
        var expiredJob = MakeJob(createdAt: DateTime.UtcNow.AddHours(-25));
        var freshJob = MakeJob(createdAt: DateTime.UtcNow.AddMinutes(-30));

        _store.Set(agentId, expiredJob);
        _store.Set(agentId, freshJob);

        _store.EvictExpiredJobs();

        _store.Get(expiredJob.Id.ToString()).Should().BeNull();
        _store.Get(freshJob.Id.ToString()).Should().NotBeNull();
    }

    [Fact]
    public void EvictExpiredJobs_RemovesFromAgentIndex()
    {
        var agentId = Guid.NewGuid().ToString();
        var expiredJob = MakeJob(createdAt: DateTime.UtcNow.AddHours(-25));

        _store.Set(agentId, expiredJob);

        _store.EvictExpiredJobs();

        _store.GetByAgent(agentId).Should().BeEmpty();
    }

    [Fact]
    public void EvictExpiredJobs_KeepsFreshJobs()
    {
        var agentId = Guid.NewGuid().ToString();
        var freshJob = MakeJob(createdAt: DateTime.UtcNow.AddHours(-1));

        _store.Set(agentId, freshJob);

        _store.EvictExpiredJobs();

        _store.Get(freshJob.Id.ToString()).Should().NotBeNull();
        _store.GetByAgent(agentId).Should().HaveCount(1);
    }

    public void Dispose() => _store.Dispose();
}
