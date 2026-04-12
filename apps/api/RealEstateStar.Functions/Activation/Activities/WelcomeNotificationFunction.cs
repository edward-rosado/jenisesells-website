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

        try
        {
            // Build ActivationOutputs with synthesis data for the personalized welcome email.
            // Binary assets are not needed — they are already persisted by PersistProfile.
            // WhatsAppEnabled lives on the input DTO (flat bool), so we bridge it via a minimal AgentDiscovery.
            var outputs = new ActivationOutputs
            {
                AgentName = input.AgentName,
                AgentPhone = input.AgentPhone,
                AgentEmail = input.AgentEmail,
                VoiceSkill = input.VoiceSkill,
                PersonalitySkill = input.PersonalitySkill,
                CoachingReport = input.CoachingReport,
                PipelineJson = input.PipelineJson,
                LocalizedSkills = input.LocalizedSkills,
                Discovery = new AgentDiscovery(
                    HeadshotBytes: null,
                    LogoBytes: null,
                    Phone: input.AgentPhone,
                    Websites: [],
                    Reviews: [],
                    Profiles: [],
                    Ga4MeasurementId: null,
                    WhatsAppEnabled: input.WhatsAppEnabled),
            };

            await service.SendAsync(
                accountId: input.AccountId,
                agentId: input.AgentId,
                handle: input.Handle,
                outputs: outputs,
                ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ACTV-FN-401] WelcomeNotification FAILED for agentId={AgentId}: {Message}",
                input.AgentId, ex.Message);
            throw;
        }
    }
}
