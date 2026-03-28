using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Leads.Interfaces;

namespace RealEstateStar.Api.Features.Leads.DeleteData;

public class DeleteDataEndpoint : IEndpoint
{
    private static readonly HashSet<string> ValidReasons =
        new(StringComparer.OrdinalIgnoreCase) { "gdpr_erasure", "ccpa_deletion", "user_request" };

    public void MapEndpoint(WebApplication app) =>
        app.MapDelete("/agents/{agentId}/leads/data", Handle)
            .RequireRateLimiting("delete-data");

    internal static async Task<IResult> Handle(
        string agentId,
        [FromBody] DeleteLeadDataRequest request,
        ILeadStore leadStore,
        ILeadDataDeletion deletion,
        ILogger<DeleteDataEndpoint> logger,
        CancellationToken ct)
    {
        // Validate DataAnnotations
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
        {
            return Results.ValidationProblem(
                validationResults
                    .GroupBy(v => v.MemberNames.FirstOrDefault() ?? "")
                    .ToDictionary(g => g.Key, g => g.Select(v => v.ErrorMessage!).ToArray()));
        }

        // Validate reason
        if (!ValidReasons.Contains(request.Reason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Reason"] = [$"Reason must be one of: {string.Join(", ", ValidReasons)}."]
            });
        }

        // Look up lead by email
        var lead = await leadStore.GetByEmailAsync(agentId, request.Email, ct);
        if (lead is null)
        {
            logger.LogWarning("[LEAD-020] Deletion request for unknown email in agent {AgentId}", agentId);
            return Results.NotFound();
        }

        // Execute deletion — ILeadDataDeletion validates the token internally
        var result = await deletion.ExecuteDeletionAsync(agentId, request.Email, request.Token, request.Reason, ct);

        if (!result.Success)
        {
            // Distinguish token errors from already-deleted state
            if (result.Error is not null && result.Error.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("[LEAD-021] Deletion already completed for email in agent {AgentId}", agentId);
                return Results.Conflict(new { error = result.Error });
            }

            // Token invalid or expired
            logger.LogWarning("[LEAD-016] Deletion token invalid or expired for agent {AgentId}", agentId);
            return Results.Unauthorized();
        }

        LeadDiagnostics.LeadsDeleted.Add(1);
        logger.LogInformation("[LEAD-022] Lead data deleted for agent {AgentId}, items: {Items}",
            agentId, result.DeletedItems);

        return Results.Ok(new DeleteLeadDataResponse(result.DeletedItems));
    }
}
