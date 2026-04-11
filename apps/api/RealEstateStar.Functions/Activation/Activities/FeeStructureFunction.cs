using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.FeeStructure;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: extracts fee structure and commission information.
/// Delegates to <see cref="FeeStructureWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class FeeStructureFunction(
    FeeStructureWorker worker,
    IStagedContentProvider stagedContent,
    ILogger<FeeStructureFunction> logger)
{
    [Function(ActivityNames.FeeStructure)]
    public async Task<string> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-210] FeeStructure for agentId={AgentId}", input.AgentId);

        // Load Drive file contents from blob staging (workers are pure compute, don't touch storage)
        var stagedContents = await stagedContent.GetTopContentsAsync(input.AccountId, input.AgentId, 20, ct);

        try
        {
            var discovery = ActivationDtoMapper.ToDomain(input.Discovery);

            var result = await worker.AnalyzeAsync(
                emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
                driveIndex: ActivationDtoMapper.ToDomainWithContents(input.DriveIndex, stagedContents),
                websites: discovery.Websites,
                reviews: discovery.Reviews,
                ct: ct);

            return JsonSerializer.Serialize(new StringOutput { Value = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ACTV-FN-034] FeeStructure FAILED for accountId={AccountId}, agentId={AgentId}: {Message}",
                input.AccountId, input.AgentId, ex.Message);
            throw;
        }
    }
}
