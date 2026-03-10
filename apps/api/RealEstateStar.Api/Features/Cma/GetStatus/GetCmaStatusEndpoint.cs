using RealEstateStar.Api.Endpoints;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Features.Cma.GetStatus;

public class GetCmaStatusEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapGet("/agents/{agentId}/cma/{jobId}/status", Handle);

    internal static IResult Handle(string agentId, string jobId, ICmaJobStore store, HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-cache";

        var job = store.Get(jobId);
        if (job is null || job.AgentId != agentId)
            return Results.Problem(statusCode: 404, title: "Job not found");

        return Results.Ok(new GetStatusResponse
        {
            Status = job.Status,
            Step = job.Step,
            TotalSteps = job.TotalSteps,
            Message = StatusMessages.Get(job.Status),
            ErrorMessage = job.Status == CmaJobStatus.Failed ? job.ErrorMessage : null
        });
    }
}
