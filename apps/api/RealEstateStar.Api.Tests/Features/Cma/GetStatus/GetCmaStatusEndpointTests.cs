using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.GetStatus;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Tests.TestHelpers;

namespace RealEstateStar.Api.Tests.Features.Cma.GetStatus;

public class GetCmaStatusEndpointTests
{
    [Fact]
    public void Handle_Returns404ProblemDetails_WhenJobNotFound()
    {
        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.Get("nonexistent")).Returns((CmaJob?)null);

        var httpContext = new DefaultHttpContext();
        var result = GetCmaStatusEndpoint.Handle("agent1", "nonexistent", store.Object, httpContext);

        var problemResult = result.Should().BeAssignableTo<ProblemHttpResult>().Subject;
        problemResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void Handle_SetsCacheControlHeader()
    {
        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.Get("nonexistent")).Returns((CmaJob?)null);

        var httpContext = new DefaultHttpContext();
        GetCmaStatusEndpoint.Handle("agent1", "nonexistent", store.Object, httpContext);

        httpContext.Response.Headers.CacheControl.ToString().Should().Be("no-cache");
    }

    [Fact]
    public void Handle_ReturnsStatusWithErrorMessage_WhenJobFailed()
    {
        var job = CmaJob.Create("agent1", TestData.MakeLead());
        job.Fail("Something went wrong");

        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.Get("job1")).Returns(job);

        var httpContext = new DefaultHttpContext();
        var result = GetCmaStatusEndpoint.Handle("agent1", "job1", store.Object, httpContext);

        var okResult = result.Should().BeAssignableTo<Ok<GetStatusResponse>>().Subject;
        okResult.Value!.ErrorMessage.Should().Be("Something went wrong");
        okResult.Value.Status.Should().Be(CmaJobStatus.Failed);
    }

    [Fact]
    public void Handle_ReturnsStatusWithoutErrorMessage_WhenJobNotFailed()
    {
        var job = CmaJob.Create("agent1", TestData.MakeLead());

        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.Get("job1")).Returns(job);

        var httpContext = new DefaultHttpContext();
        var result = GetCmaStatusEndpoint.Handle("agent1", "job1", store.Object, httpContext);

        var okResult = result.Should().BeAssignableTo<Ok<GetStatusResponse>>().Subject;
        okResult.Value!.ErrorMessage.Should().BeNull();
        okResult.Value.Status.Should().Be(CmaJobStatus.Parsing);
        okResult.Value.Step.Should().Be(0);
        okResult.Value.TotalSteps.Should().Be(9);
    }

    [Fact]
    public void Handle_Returns404_WhenAgentIdDoesNotMatchJob()
    {
        var job = CmaJob.Create("agent1", TestData.MakeLead());

        var store = new Mock<ICmaJobStore>();
        store.Setup(s => s.Get("job1")).Returns(job);

        var httpContext = new DefaultHttpContext();
        var result = GetCmaStatusEndpoint.Handle("different-agent", "job1", store.Object, httpContext);

        var problemResult = result.Should().BeAssignableTo<ProblemHttpResult>().Subject;
        problemResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
