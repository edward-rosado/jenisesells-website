using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.Personality;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: extracts agent personality skill.
/// Delegates to <see cref="PersonalityWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class PersonalityFunction(
    PersonalityWorker worker,
    ILogger<PersonalityFunction> logger)
{
    [Function(ActivityNames.Personality)]
    public async Task<string> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-110] Personality for agentId={AgentId}", input.AgentId);

        var result = await worker.ExtractAsync(
            agentName: input.AgentName,
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
            agentDiscovery: ActivationDtoMapper.ToDomain(input.Discovery),
            ct: ct);

        return JsonSerializer.Serialize(new PersonalityOutput { PersonalitySkillMarkdown = result.PersonalitySkillMarkdown, IsLowConfidence = result.IsLowConfidence });
    }
}
