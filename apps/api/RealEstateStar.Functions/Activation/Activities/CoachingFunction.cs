using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.Coaching;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: produces a coaching report from agent communications.
/// Delegates to <see cref="CoachingWorker"/>.
/// </summary>
public sealed class CoachingFunction(
    CoachingWorker worker,
    ILogger<CoachingFunction> logger)
{
    [Function(ActivityNames.Coaching)]
    public async Task<CoachingOutput> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-170] Coaching for agentId={AgentId}", input.AgentId);

        var result = await worker.AnalyzeAsync(
            agentName: input.AgentName,
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
            agentDiscovery: ActivationDtoMapper.ToDomain(input.Discovery),
            ct: ct);

        return new CoachingOutput(result.CoachingReportMarkdown, result.IsInsufficient);
    }
}
