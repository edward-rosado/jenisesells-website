using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.BrandVoice;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: extracts brokerage brand voice signals.
/// Delegates to <see cref="BrandVoiceWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class BrandVoiceFunction(
    BrandVoiceWorker worker,
    IStagedContentProvider stagedContent,
    ILogger<BrandVoiceFunction> logger)
{
    [Function(ActivityNames.BrandVoice)]
    public async Task<string> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-190] BrandVoice for agentId={AgentId}", input.AgentId);

        // Load Drive file contents from blob staging (workers are pure compute, don't touch storage)
        var stagedContents = await stagedContent.GetTopContentsAsync(input.AccountId, input.AgentId, 20, ct);

        var (signals, localizedSkills) = await worker.AnalyzeAsync(
            emailCorpus: ActivationDtoMapper.ToDomain(input.EmailCorpus),
            driveIndex: ActivationDtoMapper.ToDomainWithContents(input.DriveIndex, stagedContents),
            discovery: ActivationDtoMapper.ToDomain(input.Discovery),
            ct: ct);

        return JsonSerializer.Serialize(new BrandVoiceOutput { Signals = signals, LocalizedSkills = localizedSkills });
    }
}
