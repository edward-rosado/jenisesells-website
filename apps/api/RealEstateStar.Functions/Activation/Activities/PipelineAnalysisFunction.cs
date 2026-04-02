using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.PipelineAnalysis;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: analyzes agent sales pipeline from email and drive data.
/// Delegates to <see cref="PipelineAnalysisWorker"/>.
/// </summary>
public sealed class PipelineAnalysisFunction(
    PipelineAnalysisWorker worker,
    ILogger<PipelineAnalysisFunction> logger)
{
    [Function(ActivityNames.PipelineAnalysis)]
    public async Task<StringOutput> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-160] PipelineAnalysis for agentId={AgentId}", input.AgentId);

        var result = await worker.AnalyzeAsync(
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
            ct: ct);

        return new StringOutput(result);
    }
}
