using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.EmailFetch;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 1 gather activity: fetches agent email corpus from Gmail.
/// Delegates to <see cref="AgentEmailFetchWorker"/>.
/// </summary>
public sealed class EmailFetchFunction(
    AgentEmailFetchWorker worker,
    ILogger<EmailFetchFunction> logger)
{
    [Function(ActivityNames.EmailFetch)]
    public async Task<EmailFetchOutput> RunAsync(
        [ActivityTrigger] EmailFetchInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-020] EmailFetch for accountId={AccountId}, agentId={AgentId}",
            input.AccountId, input.AgentId);

        var corpus = await worker.RunAsync(input.AccountId, input.AgentId, ct);

        return ActivationDtoMapper.ToDto(corpus);
    }
}
