using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Activities.Activation.BrandMerge;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 3 activity: merges branding kit and voice skill into Brand Profile.md and Brand Voice.md.
/// Delegates to <see cref="BrandMergeActivity"/>.
/// </summary>
public sealed class BrandMergeFunction(
    BrandMergeActivity activity,
    ILogger<BrandMergeFunction> logger)
{
    [Function(ActivityNames.BrandMerge)]
    public async Task RunAsync(
        [ActivityTrigger] BrandMergeInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-310] BrandMerge for accountId={AccountId}, agentId={AgentId}",
            input.AccountId, input.AgentId);

        await activity.ExecuteAsync(
            accountId: input.AccountId,
            agentId: input.AgentId,
            brandingKit: input.BrandingKit,
            voiceSkill: input.VoiceSkill,
            ct: ct);
    }
}
