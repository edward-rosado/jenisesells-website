using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.BrandExtraction;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: extracts brokerage brand signals from websites, emails, and documents.
/// Delegates to <see cref="BrandExtractionWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class BrandExtractionFunction(
    BrandExtractionWorker worker,
    IStagedContentProvider stagedContent,
    ILogger<BrandExtractionFunction> logger)
{
    [Function(ActivityNames.BrandExtraction)]
    public async Task<string> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-180] BrandExtraction for agentId={AgentId}", input.AgentId);

        // Load Drive file contents from blob staging (workers are pure compute, don't touch storage)
        var stagedContents = await stagedContent.GetTopContentsAsync(input.AccountId, input.AgentId, 20, ct);

        try
        {
            var result = await worker.AnalyzeAsync(
                emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
                driveIndex: ActivationDtoMapper.ToDomainWithContents(input.DriveIndex, stagedContents),
                discovery: ActivationDtoMapper.ToDomain(input.Discovery),
                ct: ct);

            return JsonSerializer.Serialize(new StringOutput { Value = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ACTV-FN-033] BrandExtraction FAILED for accountId={AccountId}, agentId={AgentId}: {Message}",
                input.AccountId, input.AgentId, ex.Message);
            throw;
        }
    }
}
