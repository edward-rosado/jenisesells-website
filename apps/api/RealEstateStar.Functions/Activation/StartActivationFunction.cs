using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Functions.Activation;

/// <summary>
/// Queue-triggered function that starts a new activation orchestration.
///
/// When <c>Features:Activation:UseBackgroundService</c> is false, the API writes an
/// <see cref="ActivationRequest"/> to the <c>activation-requests</c> queue and this
/// function picks it up, creating a Durable orchestration instance with a deterministic
/// instance ID that prevents duplicate runs for the same agent.
///
/// The API side queues the message; this side starts the orchestration.
/// The feature flag is read by the API — this function always runs when a message arrives.
/// </summary>
public sealed class StartActivationFunction(
    ILogger<StartActivationFunction> logger)
{
    [Function("StartActivation")]
    public async Task RunAsync(
        [QueueTrigger("activation-requests")] ActivationRequest request,
        [DurableClient] DurableTaskClient client,
        CancellationToken ct)
    {
        var instanceId = ActivationOrchestratorFunction.InstanceId(request.AccountId, request.AgentId);

        logger.LogInformation(
            "[ACTV-FN-500] StartActivation: accountId={AccountId}, agentId={AgentId}, instanceId={InstanceId}",
            request.AccountId, request.AgentId, instanceId);

        // Check if orchestration is already running to avoid duplicate starts.
        // Durable Functions ensures idempotency: if the instance already exists and is
        // Running/Pending, ScheduleNewOrchestrationInstanceAsync with the same instance ID
        // throws an exception. We catch it and log a skip.
        try
        {
            await client.ScheduleNewOrchestrationInstanceAsync(
                orchestratorName: "ActivationOrchestrator",
                input: request,
                options: new Microsoft.DurableTask.StartOrchestrationOptions(instanceId),
                cancellation: ct);

            logger.LogInformation(
                "[ACTV-FN-501] Orchestration scheduled: instanceId={InstanceId}", instanceId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains(instanceId, StringComparison.OrdinalIgnoreCase))
        {
            // Instance already running — skip (idempotent dedup)
            logger.LogInformation(
                "[ACTV-FN-502] Orchestration already running for instanceId={InstanceId} — skipping duplicate start",
                instanceId);
        }
    }
}
