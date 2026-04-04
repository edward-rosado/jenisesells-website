using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.MarketingStyle;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: analyzes marketing style from email corpus and drive index.
/// Delegates to <see cref="MarketingStyleWorker"/>.
/// </summary>
public sealed class MarketingStyleFunction(
    MarketingStyleWorker worker,
    ILogger<MarketingStyleFunction> logger)
{
    [Function(ActivityNames.MarketingStyle)]
    public async Task<MarketingStyleOutput> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-140] MarketingStyle for agentId={AgentId}", input.AgentId);

        var (styleGuide, brandSignals) = await worker.AnalyzeAsync(
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomain(input.DriveIndex),
            ct: ct);

        return new MarketingStyleOutput { StyleGuide = styleGuide, BrandSignals = brandSignals };
    }
}
