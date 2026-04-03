using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.DriveIndex;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 1 gather activity: indexes the agent's Google Drive.
/// Delegates to <see cref="DriveIndexWorker"/>.
/// </summary>
public sealed class DriveIndexFunction(
    DriveIndexWorker worker,
    ILogger<DriveIndexFunction> logger)
{
    [Function(ActivityNames.DriveIndex)]
    public async Task<DriveIndexOutput> RunAsync(
        [ActivityTrigger] DriveIndexInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-030] DriveIndex for accountId={AccountId}, agentId={AgentId}",
            input.AccountId, input.AgentId);

        var driveIndex = await worker.RunAsync(input.AccountId, input.AgentId, ct);

        return ActivationDtoMapper.ToDto(driveIndex);
    }
}
