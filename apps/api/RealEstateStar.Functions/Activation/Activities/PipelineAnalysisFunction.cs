using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.PipelineAnalysis;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: analyzes agent sales pipeline from email and drive data.
/// Delegates to <see cref="PipelineAnalysisWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class PipelineAnalysisFunction(
    PipelineAnalysisWorker worker,
    ILogger<PipelineAnalysisFunction> logger)
{
    [Function(ActivityNames.PipelineAnalysis)]
    public async Task<string> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-160] PipelineAnalysis for agentId={AgentId}", input.AgentId);

        try
        {
            var result = await worker.AnalyzeAsync(
                emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
                driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
                agentDiscovery: ActivationDtoMapper.ToDomain(input.Discovery),
                ct: ct);

            return JsonSerializer.Serialize(new PipelineAnalysisOutput
            {
                PipelineJson = result?.PipelineJson,
                Markdown = result?.Markdown
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ACTV-FN-161] PipelineAnalysis FAILED for agentId={AgentId}: {Message}",
                input.AgentId, ex.Message);
            throw;
        }
    }
}
