using Microsoft.AspNetCore.Mvc;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Privacy.Interfaces;

namespace RealEstateStar.Api.Features.Leads.Export;

public class ExportLeadEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapGet("/agents/{agentId}/leads/export", Handle)
            .RequireRateLimiting("lead-export");

    internal static async Task<IResult> Handle(
        string agentId,
        [FromQuery] string email,
        [FromServices] ILeadExportDataService dataExport,
        [FromServices] ILogger<ExportLeadEndpoint> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["The email query parameter is required."]
            });
        }

        var data = await dataExport.GatherAsync(agentId, email, ct);

        if (data is null)
        {
            logger.LogWarning("[EXPORT-011] No data found for export. AgentId: {AgentId}", agentId);
            return Results.NotFound();
        }

        logger.LogInformation("[EXPORT-010] Lead data exported. AgentId: {AgentId}", agentId);
        return Results.Ok(data);
    }
}
