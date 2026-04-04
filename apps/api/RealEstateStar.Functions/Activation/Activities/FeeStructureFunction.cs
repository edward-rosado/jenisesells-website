using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.FeeStructure;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: extracts fee structure and commission information.
/// Delegates to <see cref="FeeStructureWorker"/>.
/// </summary>
public sealed class FeeStructureFunction(
    FeeStructureWorker worker,
    ILogger<FeeStructureFunction> logger)
{
    [Function(ActivityNames.FeeStructure)]
    public async Task<StringOutput> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-210] FeeStructure for agentId={AgentId}", input.AgentId);

        var discovery = ActivationDtoMapper.ToDomain(input.Discovery);

        var result = await worker.AnalyzeAsync(
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
            websites: discovery.Websites,
            ct: ct);

        return new StringOutput { Value = result };
    }
}
