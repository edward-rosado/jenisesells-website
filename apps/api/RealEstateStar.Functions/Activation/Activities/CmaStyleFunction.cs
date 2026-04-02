using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.CmaStyle;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: analyzes CMA style from drive documents.
/// Delegates to <see cref="CmaStyleWorker"/>.
/// </summary>
public sealed class CmaStyleFunction(
    CmaStyleWorker worker,
    ILogger<CmaStyleFunction> logger)
{
    [Function(ActivityNames.CmaStyle)]
    public async Task<StringOutput> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-130] CmaStyle for agentId={AgentId}", input.AgentId);

        var result = await worker.AnalyzeAsync(
            driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
            ct: ct);

        return new StringOutput(result);
    }
}
