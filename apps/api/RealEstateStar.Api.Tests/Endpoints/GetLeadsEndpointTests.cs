using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.ListLeads;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Endpoints;
using RealEstateStar.Api.Tests.TestHelpers;

namespace RealEstateStar.Api.Tests.Endpoints;

public class GetLeadsEndpointTests
{
    private static List<CmaJob> MakeJobs(string agentId, int count)
    {
        var jobs = new List<CmaJob>();
        for (var i = 0; i < count; i++)
            jobs.Add(CmaJob.Create(agentId, TestData.MakeLead(firstName: $"Lead{i}", lastName: "Test")));
        return jobs;
    }

    [Fact]
    public void Handle_ReturnsLeads_ForValidAgent()
    {
        var jobs = MakeJobs("agent1", 3);
        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.GetByAgent("agent1")).Returns(jobs);

        var httpContext = new DefaultHttpContext();
        var result = GetLeadsEndpoint.Handle("agent1", null, null, store.Object, httpContext);

        var okResult = result.Should().BeAssignableTo<Ok<IEnumerable<LeadSummaryResponse>>>().Subject;
        okResult.Value!.Count().Should().Be(3);
    }

    [Fact]
    public void Handle_ReturnsEmpty_ForUnknownAgent()
    {
        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.GetByAgent("unknown")).Returns(new List<CmaJob>());

        var httpContext = new DefaultHttpContext();
        var result = GetLeadsEndpoint.Handle("unknown", null, null, store.Object, httpContext);

        var okResult = result.Should().BeAssignableTo<Ok<IEnumerable<LeadSummaryResponse>>>().Subject;
        okResult.Value!.Should().BeEmpty();
    }

    [Fact]
    public void Handle_PaginatesWithSkip()
    {
        var jobs = MakeJobs("agent1", 5);
        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.GetByAgent("agent1")).Returns(jobs);

        var httpContext = new DefaultHttpContext();
        var result = GetLeadsEndpoint.Handle("agent1", 3, null, store.Object, httpContext);

        var okResult = result.Should().BeAssignableTo<Ok<IEnumerable<LeadSummaryResponse>>>().Subject;
        okResult.Value!.Count().Should().Be(2);
    }

    [Fact]
    public void Handle_PaginatesWithTake()
    {
        var jobs = MakeJobs("agent1", 5);
        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.GetByAgent("agent1")).Returns(jobs);

        var httpContext = new DefaultHttpContext();
        var result = GetLeadsEndpoint.Handle("agent1", null, 2, store.Object, httpContext);

        var okResult = result.Should().BeAssignableTo<Ok<IEnumerable<LeadSummaryResponse>>>().Subject;
        okResult.Value!.Count().Should().Be(2);
    }

    [Fact]
    public void Handle_CapsMaxTakeAt100()
    {
        var jobs = MakeJobs("agent1", 150);
        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.GetByAgent("agent1")).Returns(jobs);

        var httpContext = new DefaultHttpContext();
        var result = GetLeadsEndpoint.Handle("agent1", null, 200, store.Object, httpContext);

        var okResult = result.Should().BeAssignableTo<Ok<IEnumerable<LeadSummaryResponse>>>().Subject;
        okResult.Value!.Count().Should().Be(100);
    }

    [Fact]
    public void Handle_DefaultsTo50_WhenNoTake()
    {
        var jobs = MakeJobs("agent1", 80);
        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.GetByAgent("agent1")).Returns(jobs);

        var httpContext = new DefaultHttpContext();
        var result = GetLeadsEndpoint.Handle("agent1", null, null, store.Object, httpContext);

        var okResult = result.Should().BeAssignableTo<Ok<IEnumerable<LeadSummaryResponse>>>().Subject;
        okResult.Value!.Count().Should().Be(50);
    }

    [Fact]
    public void Handle_SetsCacheControlHeader()
    {
        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.GetByAgent("agent1")).Returns(new List<CmaJob>());

        var httpContext = new DefaultHttpContext();
        GetLeadsEndpoint.Handle("agent1", null, null, store.Object, httpContext);

        httpContext.Response.Headers.CacheControl.ToString().Should().Be("no-cache");
    }
}
