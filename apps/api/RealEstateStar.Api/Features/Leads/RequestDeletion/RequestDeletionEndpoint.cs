using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Leads.RequestDeletion;

public class RequestDeletionEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/agents/{agentId}/leads/request-deletion", Handle)
            .RequireRateLimiting("deletion-request");

    internal static async Task<IResult> Handle(
        string agentId,
        [FromBody] RequestDeletionRequest request,
        ILeadStore leadStore,
        ILeadDataDeletion deletion,
        IDeletionAuditLog auditLog,
        ILogger<RequestDeletionEndpoint> logger,
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

        // Look up lead by email
        var lead = await leadStore.GetByEmailAsync(agentId, request.Email, ct);

        if (lead is not null)
        {
            try
            {
                // Send verification email with deletion token
                await deletion.InitiateDeletionRequestAsync(agentId, request.Email, ct);

                // Audit log the initiation
                await auditLog.RecordInitiationAsync(agentId, lead.Id, request.Email, ct);

                logger.LogInformation("[LEAD-023] Deletion request initiated for lead {LeadId} in agent {AgentId}",
                    lead.Id, agentId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[LEAD-024] Failed to initiate deletion request for agent {AgentId}", agentId);
            }
        }
        else
        {
            // Do not reveal whether email exists (prevent enumeration)
            logger.LogInformation("[LEAD-025] Deletion request for unknown email in agent {AgentId} — returning 202", agentId);
        }

        // Always return 202 to prevent email enumeration
        return Results.Accepted();
    }
}
