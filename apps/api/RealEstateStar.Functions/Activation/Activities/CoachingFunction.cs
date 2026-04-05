using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.Coaching;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: produces a coaching report from agent communications.
/// Delegates to <see cref="CoachingWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class CoachingFunction(
    CoachingWorker worker,
    ILogger<CoachingFunction> logger)
{
    [Function(ActivityNames.Coaching)]
    public async Task<string> RunAsync(
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

        return JsonSerializer.Serialize(new CoachingOutput { CoachingReportMarkdown = result.CoachingReportMarkdown, IsInsufficient = result.IsInsufficient });
    }
}
