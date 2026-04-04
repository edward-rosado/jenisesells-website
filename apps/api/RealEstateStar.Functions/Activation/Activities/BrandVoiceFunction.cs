using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.BrandVoice;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: extracts brokerage brand voice signals.
/// Delegates to <see cref="BrandVoiceWorker"/>.
/// </summary>
public sealed class BrandVoiceFunction(
    BrandVoiceWorker worker,
    ILogger<BrandVoiceFunction> logger)
{
    [Function(ActivityNames.BrandVoice)]
    public async Task<StringOutput> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-190] BrandVoice for agentId={AgentId}", input.AgentId);

        var result = await worker.AnalyzeAsync(
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
            discovery: ActivationDtoMapper.ToDomain(input.Discovery),
            ct: ct);

        return new StringOutput { Value = result };
    }
}
