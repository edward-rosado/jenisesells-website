using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.DriveIndex;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 1 gather activity: indexes the agent's Google Drive.
/// Delegates to <see cref="DriveIndexWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class DriveIndexFunction(
    DriveIndexWorker worker,
    ILogger<DriveIndexFunction> logger)
{
    [Function(ActivityNames.DriveIndex)]
    public async Task<string> RunAsync(
        [ActivityTrigger] DriveIndexInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-030] DriveIndex for accountId={AccountId}, agentId={AgentId}",
            input.AccountId, input.AgentId);

        try
        {
            var driveIndex = await worker.RunAsync(input.AccountId, input.AgentId, ct);
            return JsonSerializer.Serialize(ActivationDtoMapper.ToDto(driveIndex));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ACTV-FN-031] DriveIndex FAILED for accountId={AccountId}, agentId={AgentId}: {Message}",
                input.AccountId, input.AgentId, ex.Message);
            throw; // re-throw so the Durable Task framework captures it with the message
        }
    }
}
