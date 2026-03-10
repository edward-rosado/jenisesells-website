using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Features.Cma.ListLeads;

public class ListLeadsEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapGet("/agents/{agentId}/leads", Handle);

    internal static IResult Handle(string agentId, int? skip, int? take, ICmaJobStore store, HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-cache";

        var jobs = store.GetByAgent(agentId);
        var paged = jobs.Skip(skip ?? 0).Take(Math.Min(take ?? 50, 100));

        return Results.Ok(paged.Select(j => j.ToListLeadsResponse()));
    }
}
