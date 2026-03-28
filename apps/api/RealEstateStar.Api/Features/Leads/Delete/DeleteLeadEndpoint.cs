using Microsoft.AspNetCore.Mvc;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Leads.Delete;

public class DeleteLeadEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapDelete("/agents/{agentId}/leads/delete", Handle)
            .RequireRateLimiting("lead-delete");

    internal static async Task<IResult> Handle(
        string agentId,
        [FromQuery] string email,
        ILeadDataDeletion deletion,
        ILogger<DeleteLeadEndpoint> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["The email query parameter is required."]
            });
        }

        var result = await deletion.ExecuteDeletionAsync(agentId, email, "", "gdpr_erasure", ct);

        if (!result.Success)
        {
            logger.LogWarning("[DELETE-011] Lead not found for deletion. AgentId: {AgentId}", agentId);
            return Results.NotFound();
        }

        logger.LogInformation("[DELETE-010] Lead data deleted. AgentId: {AgentId}, Items: {Items}",
            agentId, result.DeletedItems);
        return Results.NoContent();
    }
}
