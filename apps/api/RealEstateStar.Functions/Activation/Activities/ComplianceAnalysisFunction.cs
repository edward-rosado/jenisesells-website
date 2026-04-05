using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.ComplianceAnalysis;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: analyzes compliance gaps in agent communications.
/// Delegates to <see cref="ComplianceAnalysisWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class ComplianceAnalysisFunction(
    ComplianceAnalysisWorker worker,
    IStagedContentProvider stagedContent,
    ILogger<ComplianceAnalysisFunction> logger)
{
    [Function(ActivityNames.ComplianceAnalysis)]
    public async Task<string> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-200] ComplianceAnalysis for agentId={AgentId}", input.AgentId);

        // Load Drive file contents from blob staging (workers are pure compute, don't touch storage)
        var stagedContents = await stagedContent.GetAllContentsAsync(input.AccountId, input.AgentId, ct);

        var result = await worker.AnalyzeAsync(
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomainWithContents(input.DriveIndex, stagedContents),
            discovery: ActivationDtoMapper.ToDomain(input.Discovery),
            ct: ct);

        return JsonSerializer.Serialize(new StringOutput { Value = result });
    }
}
