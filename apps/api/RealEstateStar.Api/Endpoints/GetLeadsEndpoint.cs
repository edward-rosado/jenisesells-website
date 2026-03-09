using RealEstateStar.Api.Models;
using RealEstateStar.Api.Models.Responses;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Endpoints;

public static class GetLeadsEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/agents/{agentId}/leads", Handle);

    private static IResult Handle(string agentId, int? skip, int? take, ICmaJobStore store, HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-cache";

        var jobs = store.GetByAgent(agentId);
        var paged = jobs.Skip(skip ?? 0).Take(Math.Min(take ?? 50, 100));

        return Results.Ok(paged.Select(j => new LeadSummaryResponse
        {
            Id = j.Id.ToString(),
            Name = j.Lead.FullName,
            Address = j.Lead.FullAddress,
            Timeline = j.Lead.Timeline,
            CmaStatus = j.Status.ToString().ToLowerInvariant(),
            SubmittedAt = j.CreatedAt,
            DriveLink = j.DriveLink
        }));
    }
}
