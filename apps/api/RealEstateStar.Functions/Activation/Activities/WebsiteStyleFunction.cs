using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.WebsiteStyle;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2 synthesis activity: analyzes website style from agent discovery data.
/// Delegates to <see cref="WebsiteStyleWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class WebsiteStyleFunction(
    WebsiteStyleWorker worker,
    ILogger<WebsiteStyleFunction> logger)
{
    [Function(ActivityNames.WebsiteStyle)]
    public async Task<string> RunAsync(
        [ActivityTrigger] SynthesisInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-150] WebsiteStyle for agentId={AgentId}", input.AgentId);

        try
        {
            var result = await worker.AnalyzeAsync(
                discovery: ActivationDtoMapper.ToDomain(input.Discovery),
                ct: ct);

            return JsonSerializer.Serialize(new StringOutput { Value = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ACTV-FN-151] WebsiteStyle FAILED for agentId={AgentId}: {Message}",
                input.AgentId, ex.Message);
            throw;
        }
    }
}
