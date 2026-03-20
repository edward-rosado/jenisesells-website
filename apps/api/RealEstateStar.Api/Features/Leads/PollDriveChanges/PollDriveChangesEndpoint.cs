using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Features.Leads.PollDriveChanges;

public class PollDriveChangesEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/internal/drive/poll", Handle)
            .DisableRateLimiting();

    internal static async Task<IResult> Handle(
        HttpContext httpContext,
        DriveChangeMonitor monitor,
        IAgentConfigService agentConfigService,
        IConfiguration config,
        ILogger<PollDriveChangesEndpoint> logger,
        CancellationToken ct)
    {
        var expectedToken = config["InternalApiToken"];
        var authHeader = httpContext.Request.Headers.Authorization.ToString();

        var providedToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..]
            : null;
        var tokensEqual = !string.IsNullOrEmpty(expectedToken)
            && !string.IsNullOrEmpty(providedToken)
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedToken),
                Encoding.UTF8.GetBytes(expectedToken));
        if (!tokensEqual)
        {
            return Results.Unauthorized();
        }

        var since = config.GetValue<DateTime?>("DriveMonitor:LastPollTimestamp")
                    ?? DateTime.UtcNow.AddMinutes(-2);

        var agents = await agentConfigService.ListAllAsync(ct);
        var perAgentResults = new List<DriveChangeResult>();

        foreach (var agent in agents)
        {
            var agentEmail = agent.Identity?.Email ?? "";
            try
            {
                var result = await monitor.PollAsync(agent.Id, agentEmail, since, ct);
                perAgentResults.Add(result);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[LEAD-070] Drive poll failed for agent {AgentId}", agent.Id);
                perAgentResults.Add(new DriveChangeResult(0, 0, 1, [$"[LEAD-070] Poll failed for agent {agent.Id}: {ex.Message}"]));
            }
        }

        var aggregate = DriveChangeResult.Merge(perAgentResults);

        logger.LogInformation(
            "[LEAD-071] Drive poll complete: processed={Processed}, statusUpdated={StatusUpdated}, errors={Errors}",
            aggregate.Processed, aggregate.StatusUpdated, aggregate.Errors);

        return Results.Ok(aggregate);
    }
}
