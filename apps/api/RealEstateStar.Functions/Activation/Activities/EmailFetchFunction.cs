using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.EmailFetch;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 1 gather activity: fetches agent email corpus from Gmail.
/// Delegates to <see cref="AgentEmailFetchWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class EmailFetchFunction(
    AgentEmailFetchWorker worker,
    ILogger<EmailFetchFunction> logger)
{
    [Function(ActivityNames.EmailFetch)]
    public async Task<string> RunAsync(
        [ActivityTrigger] EmailFetchInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-020] EmailFetch for accountId={AccountId}, agentId={AgentId}",
            input.AccountId, input.AgentId);

        try
        {
            var corpus = await worker.RunAsync(input.AccountId, input.AgentId, ct);
            return JsonSerializer.Serialize(ActivationDtoMapper.ToDto(corpus));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ACTV-FN-021] EmailFetch FAILED for accountId={AccountId}, agentId={AgentId}: {Message}",
                input.AccountId, input.AgentId, ex.Message);
            throw;
        }
    }
}
