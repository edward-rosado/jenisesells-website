using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.WebsiteStyle;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: analyzes website style from agent discovery data.
/// Delegates to <see cref="WebsiteStyleWorker"/>.
/// </summary>
public sealed class WebsiteStyleFunction(
    WebsiteStyleWorker worker,
    ILogger<WebsiteStyleFunction> logger)
{
    [Function(ActivityNames.WebsiteStyle)]
    public async Task<StringOutput> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-150] WebsiteStyle for agentId={AgentId}", input.AgentId);

        var result = await worker.AnalyzeAsync(
            discovery: ActivationDtoMapper.ToDomain(input.Discovery),
            ct: ct);

        return new StringOutput(result);
    }
}
