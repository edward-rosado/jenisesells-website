using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.VoiceExtraction;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: extracts agent voice skill from email corpus and drive index.
/// Delegates to <see cref="VoiceExtractionWorker"/>.
/// </summary>
public sealed class VoiceExtractionFunction(
    VoiceExtractionWorker worker,
    ILogger<VoiceExtractionFunction> logger)
{
    [Function(ActivityNames.VoiceExtraction)]
    public async Task<VoiceExtractionOutput> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-100] VoiceExtraction for agentId={AgentId}", input.AgentId);

        var result = await worker.ExtractAsync(
            agentName: input.AgentName,
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
            agentDiscovery: ActivationDtoMapper.ToDomain(input.Discovery),
            ct: ct);

        return new VoiceExtractionOutput(result.VoiceSkillMarkdown, result.IsLowConfidence);
    }
}
