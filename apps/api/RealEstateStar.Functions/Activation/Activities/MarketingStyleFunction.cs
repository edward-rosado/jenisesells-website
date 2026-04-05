using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.MarketingStyle;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: analyzes marketing style from email corpus and drive index.
/// Delegates to <see cref="MarketingStyleWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class MarketingStyleFunction(
    MarketingStyleWorker worker,
    IStagedContentProvider stagedContent,
    ILogger<MarketingStyleFunction> logger)
{
    [Function(ActivityNames.MarketingStyle)]
    public async Task<string> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-140] MarketingStyle for agentId={AgentId}", input.AgentId);

        // Load Drive file contents from blob staging (workers are pure compute, don't touch storage)
        var stagedContents = await stagedContent.GetAllContentsAsync(input.AccountId, input.AgentId, ct);

        var (styleGuide, brandSignals) = await worker.AnalyzeAsync(
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomainWithContents(input.DriveIndex, stagedContents),
            ct: ct);

        return JsonSerializer.Serialize(new MarketingStyleOutput { StyleGuide = styleGuide, BrandSignals = brandSignals });
    }
}
