using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.AgentDiscovery;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 1 gather activity: discovers the agent's web presence, headshot, profiles, and WhatsApp.
/// Delegates to <see cref="AgentDiscoveryWorker"/>.
/// </summary>
public sealed class AgentDiscoveryFunction(
    AgentDiscoveryWorker worker,
    ILogger<AgentDiscoveryFunction> logger)
{
    [Function(ActivityNames.AgentDiscovery)]
    public async Task<AgentDiscoveryOutput> RunAsync(
        [ActivityTrigger] AgentDiscoveryInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-040] AgentDiscovery for accountId={AccountId}, agentId={AgentId}",
            input.AccountId, input.AgentId);

        var emailSignature = input.EmailSignature is null
            ? null
            : ActivationDtoMapper.ToDomain(input.EmailSignature);

        var discovery = await worker.RunAsync(
            accountId: input.AccountId,
            agentId: input.AgentId,
            agentName: input.AgentName,
            brokerageName: string.Empty,
            phoneNumber: null,
            emailSignature: emailSignature,
            ct: ct);

        return ActivationDtoMapper.ToDto(discovery);
    }
}
