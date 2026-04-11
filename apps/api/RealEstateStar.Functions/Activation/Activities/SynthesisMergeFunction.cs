using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.SynthesisMerge;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2.25 activity: cross-references all Phase 2 synthesis outputs.
/// Delegates to <see cref="SynthesisMergeWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class SynthesisMergeFunction(
    SynthesisMergeWorker worker,
    ILogger<SynthesisMergeFunction> logger)
{
    [Function(ActivityNames.SynthesisMerge)]
    public async Task<string> RunAsync(
        [ActivityTrigger] SynthesisMergeInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-038] SynthesisMerge for agentId={AgentId}", input.AgentId);

        try
        {
            var reviews = input.Reviews
                .Select(r => new Review(r.Text, r.Rating, r.Reviewer, r.Source, r.Date))
                .ToList();

            var result = await worker.MergeAsync(
                voiceSkill: input.VoiceSkill,
                personalitySkill: input.PersonalitySkill,
                coachingReport: input.CoachingReport,
                pipelineJson: input.PipelineJson,
                pipelineMarkdown: input.PipelineMarkdown,
                reviews: reviews,
                ct: ct);

            return JsonSerializer.Serialize(new SynthesisMergeOutput
            {
                EnrichedCoachingReport = result.EnrichedCoachingReport,
                Contradictions = result.Contradictions.Select(c => new ContradictionDto
                {
                    Source1 = c.Source1,
                    Source2 = c.Source2,
                    Signal = c.Signal,
                    Description = c.Description
                }).ToList(),
                StrengthsSummary = result.StrengthsSummary
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[ACTV-FN-039] SynthesisMerge FAILED for agentId={AgentId}: {Message}",
                input.AgentId, ex.Message);
            throw;
        }
    }
}
