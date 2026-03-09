using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Models.Responses;
using RealEstateStar.Api.Services;
using System.Reflection;
using RealEstateStar.Api.Endpoints;

namespace RealEstateStar.Api.Tests.Endpoints;

public class GetCmaStatusEndpointTests
{
    private static Lead MakeLead() => new()
    {
        FirstName = "John",
        LastName = "Doe",
        Email = "john@example.com",
        Phone = "555-1234",
        Address = "123 Main St",
        City = "Springfield",
        State = "NJ",
        Zip = "07081",
        Timeline = "3-6 months"
    };

    private static IResult InvokeHandle(string agentId, string jobId, ICmaJobStore store, HttpContext httpContext)
    {
        var endpoint = new GetCmaStatusEndpoint();
        var handleMethod = typeof(GetCmaStatusEndpoint).GetMethod("Handle", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (IResult)handleMethod.Invoke(null, [agentId, jobId, store, httpContext])!;
    }

    [Fact]
    public void Handle_Returns404ProblemDetails_WhenJobNotFound()
    {
        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.Get("nonexistent")).Returns((CmaJob?)null);

        var httpContext = new DefaultHttpContext();
        var result = InvokeHandle("agent1", "nonexistent", store.Object, httpContext);

        var problemResult = result.Should().BeAssignableTo<ProblemHttpResult>().Subject;
        problemResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void Handle_SetsCacheControlHeader()
    {
        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.Get("nonexistent")).Returns((CmaJob?)null);

        var httpContext = new DefaultHttpContext();
        InvokeHandle("agent1", "nonexistent", store.Object, httpContext);

        httpContext.Response.Headers.CacheControl.ToString().Should().Be("no-cache");
    }

    [Fact]
    public void Handle_ReturnsStatusWithErrorMessage_WhenJobFailed()
    {
        var job = CmaJob.Create("agent1", MakeLead());
        job.Fail("Something went wrong");

        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.Get("job1")).Returns(job);

        var httpContext = new DefaultHttpContext();
        var result = InvokeHandle("agent1", "job1", store.Object, httpContext);

        var okResult = result.Should().BeAssignableTo<Ok<CmaStatusResponse>>().Subject;
        okResult.Value!.ErrorMessage.Should().Be("Something went wrong");
        okResult.Value.Status.Should().Be("failed");
    }

    [Fact]
    public void Handle_ReturnsStatusWithoutErrorMessage_WhenJobNotFailed()
    {
        var job = CmaJob.Create("agent1", MakeLead());

        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.Get("job1")).Returns(job);

        var httpContext = new DefaultHttpContext();
        var result = InvokeHandle("agent1", "job1", store.Object, httpContext);

        var okResult = result.Should().BeAssignableTo<Ok<CmaStatusResponse>>().Subject;
        okResult.Value!.ErrorMessage.Should().BeNull();
        okResult.Value.Status.Should().Be("parsing");
        okResult.Value.Step.Should().Be(0);
        okResult.Value.TotalSteps.Should().Be(9);
    }
}
