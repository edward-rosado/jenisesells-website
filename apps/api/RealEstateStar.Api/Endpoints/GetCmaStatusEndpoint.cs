using RealEstateStar.Api.Models;
using RealEstateStar.Api.Models.Responses;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Endpoints;

public class GetCmaStatusEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/agents/{agentId}/cma/{jobId}/status", Handle);

    internal static IResult Handle(string agentId, string jobId, ICmaJobStore store, HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-cache";

        var job = store.Get(jobId);
        if (job is null || job.AgentId != agentId)
            return Results.Problem(statusCode: 404, title: "Job not found");

        return Results.Ok(new CmaStatusResponse
        {
            Status = job.Status,
            Step = job.Step,
            TotalSteps = job.TotalSteps,
            Message = StatusMessages.Get(job.Status),
            ErrorMessage = job.Status == CmaJobStatus.Failed ? job.ErrorMessage : null
        });
    }
}
