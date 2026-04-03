using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Activities.Activation.PersistAgentProfile;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 3 activity: writes all per-agent activation outputs to storage and generates account.json / content.json.
/// Delegates to <see cref="AgentProfilePersistActivity"/>.
/// </summary>
public sealed class PersistProfileFunction(
    AgentProfilePersistActivity activity,
    ILogger<PersistProfileFunction> logger)
{
    [Function(ActivityNames.PersistProfile)]
    public async Task RunAsync(
        [ActivityTrigger] PersistProfileInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-300] PersistProfile for accountId={AccountId}, agentId={AgentId}",
            input.AccountId, input.AgentId);

        var outputs = ActivationDtoMapper.BuildActivationOutputs(input);

        await activity.ExecuteAsync(
            accountId: input.AccountId,
            agentId: input.AgentId,
            handle: input.Handle,
            outputs: outputs,
            ct: ct);
    }
}
