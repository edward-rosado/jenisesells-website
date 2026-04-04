using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.BrandExtraction;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: extracts brokerage brand signals from websites, emails, and documents.
/// Delegates to <see cref="BrandExtractionWorker"/>.
/// </summary>
public sealed class BrandExtractionFunction(
    BrandExtractionWorker worker,
    ILogger<BrandExtractionFunction> logger)
{
    [Function(ActivityNames.BrandExtraction)]
    public async Task<StringOutput> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-180] BrandExtraction for agentId={AgentId}", input.AgentId);

        var result = await worker.AnalyzeAsync(
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
            discovery: ActivationDtoMapper.ToDomain(input.Discovery),
            ct: ct);

        return new StringOutput { Value = result };
    }
}
