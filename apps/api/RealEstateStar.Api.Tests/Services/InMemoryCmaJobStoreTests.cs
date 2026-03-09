using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Tests.Services;

public class InMemoryCmaJobStoreTests
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 10_000 });

    private readonly InMemoryCmaJobStore _store;

    public InMemoryCmaJobStoreTests() =>
        _store = new InMemoryCmaJobStore(_cache, Mock.Of<ILogger<InMemoryCmaJobStore>>());

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

    private static CmaJob MakeJob() =>
        CmaJob.Create(Guid.NewGuid().ToString(), MakeLead());

    [Fact]
    public void Set_And_Get_RoundTrips()
    {
        var agentId = Guid.NewGuid().ToString();
        var job = MakeJob();

        _store.Set(agentId, job);

        _store.Get(job.Id.ToString()).Should().BeSameAs(job);
    }

    [Fact]
    public void Get_ReturnsNull_ForUnknownJobId() =>
        _store.Get("nonexistent").Should().BeNull();

    [Fact]
    public void GetByAgent_ReturnsAllJobsForAgent()
    {
        var agentId = Guid.NewGuid().ToString();
        var job1 = MakeJob();
        var job2 = MakeJob();

        _store.Set(agentId, job1);
        _store.Set(agentId, job2);

        _store.GetByAgent(agentId).Should().HaveCount(2)
            .And.Contain(job1)
            .And.Contain(job2);
    }

    [Fact]
    public void GetByAgent_ReturnsEmpty_ForUnknownAgent() =>
        _store.GetByAgent("unknown").Should().BeEmpty();

    [Fact]
    public void GetByAgent_ExcludesExpiredEntries()
    {
        var agentId = Guid.NewGuid().ToString();
        var job1 = MakeJob();
        var job2 = MakeJob();

        _store.Set(agentId, job1);
        _store.Set(agentId, job2);

        // Manually remove job1 from cache to simulate expiration
        _cache.Remove(job1.Id.ToString());

        var results = _store.GetByAgent(agentId).ToList();
        results.Should().HaveCount(1);
        results.Should().Contain(job2);
    }

    [Fact]
    public void Set_DoesNotDuplicateJobIdInAgentIndex()
    {
        var agentId = Guid.NewGuid().ToString();
        var job = MakeJob();

        _store.Set(agentId, job);
        _store.Set(agentId, job); // Set same job twice

        _store.GetByAgent(agentId).Should().HaveCount(1);
    }

    [Fact]
    public void Set_UpdatesExistingJob()
    {
        var agentId = Guid.NewGuid().ToString();
        var job = MakeJob();

        _store.Set(agentId, job);
        job.AdvanceTo(CmaJobStatus.Complete);
        _store.Set(agentId, job);

        _store.Get(job.Id.ToString())!.Status.Should().Be(CmaJobStatus.Complete);
    }
}
