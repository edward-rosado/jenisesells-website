using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.VoiceExtraction;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: extracts agent voice skill from email corpus and drive index.
/// Delegates to <see cref="VoiceExtractionWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class VoiceExtractionFunction(
    VoiceExtractionWorker worker,
    ILogger<VoiceExtractionFunction> logger)
{
    [Function(ActivityNames.VoiceExtraction)]
    public async Task<string> RunAsync(
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

        return JsonSerializer.Serialize(new VoiceExtractionOutput { VoiceSkillMarkdown = result.VoiceSkillMarkdown, IsLowConfidence = result.IsLowConfidence });
    }
}
