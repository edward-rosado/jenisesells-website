using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 4 activity: sends a personalized welcome notification to the agent via WhatsApp or email fallback.
/// Delegates to <see cref="IWelcomeNotificationService"/>.
/// </summary>
public sealed class WelcomeNotificationFunction(
    IWelcomeNotificationService service,
    ILogger<WelcomeNotificationFunction> logger)
{
    [Function(ActivityNames.WelcomeNotification)]
    public async Task RunAsync(
        [ActivityTrigger] WelcomeNotificationInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-400] WelcomeNotification for accountId={AccountId}, agentId={AgentId}",
            input.AccountId, input.AgentId);

        // Build a minimal ActivationOutputs with the fields the welcome service needs.
        // Binary assets are not needed for notification; they are already persisted by PersistProfile.
        var outputs = new ActivationOutputs
        {
            AgentName = input.AgentName,
            AgentPhone = input.AgentPhone,
            LocalizedSkills = input.LocalizedSkills,
        };

        await service.SendAsync(
            accountId: input.AccountId,
            agentId: input.AgentId,
            handle: input.Handle,
            outputs: outputs,
            ct: ct);
    }
}
