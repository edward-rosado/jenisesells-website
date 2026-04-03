using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.Personality;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: extracts agent personality skill.
/// Delegates to <see cref="PersonalityWorker"/>.
/// </summary>
public sealed class PersonalityFunction(
    PersonalityWorker worker,
    ILogger<PersonalityFunction> logger)
{
    [Function(ActivityNames.Personality)]
    public async Task<PersonalityOutput> RunAsync(
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

        return new PersonalityOutput(result.PersonalitySkillMarkdown, result.IsLowConfidence);
    }
}
