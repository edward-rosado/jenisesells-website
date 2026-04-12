using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.AgentDiscovery;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 1 gather activity: discovers the agent's web presence, headshot, profiles, and WhatsApp.
/// Delegates to <see cref="AgentDiscoveryWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class AgentDiscoveryFunction(
    AgentDiscoveryWorker worker,
    ILogger<AgentDiscoveryFunction> logger)
{
    [Function(ActivityNames.AgentDiscovery)]
    public async Task<string> RunAsync(
        [ActivityTrigger] AgentDiscoveryInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-040] AgentDiscovery for accountId={AccountId}, agentId={AgentId}",
            input.AccountId, input.AgentId);

        try
        {
            var emailSignature = input.EmailSignature is null
                ? null
                : ActivationDtoMapper.ToDomain(input.EmailSignature);

            var discovery = await worker.RunAsync(
                accountId: input.AccountId,
                agentId: input.AgentId,
                agentName: input.AgentName,
                brokerageName: input.BrokerageName,
                phoneNumber: input.PhoneNumber,
                emailSignature: emailSignature,
                emailHandle: input.EmailHandle,
                agentEmail: input.AgentEmail,
                discoveredUrls: input.DiscoveredUrls,
                ct: ct);

            return JsonSerializer.Serialize(ActivationDtoMapper.ToDto(discovery));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ACTV-FN-041] AgentDiscovery FAILED for agentId={AgentId}: {Message}",
                input.AgentId, ex.Message);
            throw;
        }
    }
}
