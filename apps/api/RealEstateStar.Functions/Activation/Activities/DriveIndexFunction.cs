using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.DriveIndex;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 1 gather activity: indexes the agent's Google Drive.
/// Delegates to <see cref="DriveIndexWorker"/>.
///
/// Passes <see cref="IStagedContentProvider"/> to the worker so it can stage each file's
/// content to blob storage immediately after reading from Drive (stream-and-stage).
/// This avoids accumulating all file contents in memory, preventing OOM on Consumption plan.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class DriveIndexFunction(
    DriveIndexWorker worker,
    IStagedContentProvider stagedContent,
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
            // Worker reads files from Drive one-by-one and stages each to blob via stagedContent.
            // Contents dictionary will be empty in the returned model — content lives in blob.
            var driveIndex = await worker.RunAsync(input.AccountId, input.AgentId, ct, stagedContent);

            return JsonSerializer.Serialize(ActivationDtoMapper.ToDto(driveIndex));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ACTV-FN-031] DriveIndex FAILED for accountId={AccountId}, agentId={AgentId}: {Message}",
                input.AccountId, input.AgentId, ex.Message);
            throw;
        }
    }
}
