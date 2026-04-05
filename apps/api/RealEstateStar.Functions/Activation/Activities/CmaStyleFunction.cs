using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.CmaStyle;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: analyzes CMA style from drive documents.
/// Delegates to <see cref="CmaStyleWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class CmaStyleFunction(
    CmaStyleWorker worker,
    ILogger<CmaStyleFunction> logger)
{
    [Function(ActivityNames.CmaStyle)]
    public async Task<string> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-130] CmaStyle for agentId={AgentId}", input.AgentId);

        var result = await worker.AnalyzeAsync(
            driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
            ct: ct);

        return JsonSerializer.Serialize(new StringOutput { Value = result });
    }
}
