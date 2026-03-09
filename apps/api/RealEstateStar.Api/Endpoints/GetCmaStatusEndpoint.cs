using RealEstateStar.Api.Models;
using RealEstateStar.Api.Models.Responses;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Endpoints;

public static class GetCmaStatusEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/agents/{agentId}/cma/{jobId}/status", Handle);

    private static IResult Handle(string agentId, string jobId, ICmaJobStore store, HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-cache";

        var job = store.Get(jobId);
        if (job is null)
            return Results.Problem(
                title: "Job not found",
                detail: $"No CMA job with ID '{jobId}' exists for agent '{agentId}'.",
                statusCode: StatusCodes.Status404NotFound);

        return Results.Ok(new CmaStatusResponse
        {
            Status = job.Status.ToString().ToLowerInvariant(),
            Step = job.Step,
            TotalSteps = job.TotalSteps,
            Message = StatusMessages.Get(job.Status),
            ErrorMessage = job.Status == CmaJobStatus.Failed ? job.ErrorMessage : null
        });
    }
}
