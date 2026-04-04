using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.ComplianceAnalysis;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: analyzes compliance gaps in agent communications.
/// Delegates to <see cref="ComplianceAnalysisWorker"/>.
/// </summary>
public sealed class ComplianceAnalysisFunction(
    ComplianceAnalysisWorker worker,
    ILogger<ComplianceAnalysisFunction> logger)
{
    [Function(ActivityNames.ComplianceAnalysis)]
    public async Task<StringOutput> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-200] ComplianceAnalysis for agentId={AgentId}", input.AgentId);

        var result = await worker.AnalyzeAsync(
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
            discovery: ActivationDtoMapper.ToDomain(input.Discovery),
            ct: ct);

        return new StringOutput { Value = result };
    }
}
