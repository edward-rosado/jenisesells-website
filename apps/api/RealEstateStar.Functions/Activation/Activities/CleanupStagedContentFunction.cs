using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Activation.Activities;

public sealed class CleanupStagedContentFunction(
    IStagedContentProvider stagedContent,
    ILogger<CleanupStagedContentFunction> logger)
{
    [Function(ActivityNames.CleanupStagedContent)]
    public async Task RunAsync(
        [ActivityTrigger] CleanupStagedContentInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-045] Cleaning up staged content for accountId={AccountId}, agentId={AgentId}",
            input.AccountId, input.AgentId);

        try
        {
            await stagedContent.CleanupAsync(input.AccountId, input.AgentId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ACTV-FN-047] CleanupStagedContent FAILED for agentId={AgentId}: {Message}",
                input.AgentId, ex.Message);
            throw;
        }
    }
}
