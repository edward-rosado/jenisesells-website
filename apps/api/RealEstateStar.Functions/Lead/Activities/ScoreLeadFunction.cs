using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Functions.Lead.Models;

namespace RealEstateStar.Functions.Lead.Activities;

/// <summary>
/// Loads the lead and computes its lead score via <see cref="ILeadScorer"/>.
/// </summary>
public sealed class ScoreLeadFunction(
    ILeadStore leadStore,
    ILeadScorer scorer,
    ILogger<ScoreLeadFunction> logger)
{
    [Function("ScoreLead")]
    public async Task<string> RunAsync(
        [ActivityTrigger] ScoreLeadInput input,
        CancellationToken ct)
    {
        var leadId = Guid.Parse(input.LeadId);
        var lead = await leadStore.GetAsync(input.AgentId, leadId, ct)
            ?? throw new InvalidOperationException(
                $"[SLF-001] Lead {input.LeadId} not found for agent {input.AgentId}. CorrelationId={input.CorrelationId}");

        var score = scorer.Score(lead);

        // Persist score back to lead store so it survives across replays
        lead.Score = score;
        await leadStore.UpdateStatusAsync(lead, lead.Status, ct);

        logger.LogInformation("[SLF-010] Lead {LeadId} scored: {Score}/100 ({Bucket}). CorrelationId={CorrelationId}",
            input.LeadId, score.OverallScore, score.Bucket, input.CorrelationId);

        return JsonSerializer.Serialize(new ScoreLeadOutput { Score = score });
    }
}
