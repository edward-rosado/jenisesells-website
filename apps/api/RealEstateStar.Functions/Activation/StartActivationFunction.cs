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

        // Pre-check: query the instance state before scheduling to avoid duplicate starts.
        // This is more reliable than catching InvalidOperationException (which is brittle —
        // the exception message format is an implementation detail of the Durable Task SDK).
        var existing = await client.GetInstanceAsync(instanceId, ct);
        if (existing is not null &&
            existing.RuntimeStatus is OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending)
        {
            logger.LogInformation(
                "[ACTV-FN-002] Skipping duplicate activation — orchestration already Running/Pending. instanceId={InstanceId}",
                instanceId);
            return;
        }

        await client.ScheduleNewOrchestrationInstanceAsync(
            orchestratorName: "ActivationOrchestrator",
            input: request,
            options: new Microsoft.DurableTask.StartOrchestrationOptions(instanceId),
            cancellation: ct);

        logger.LogInformation(
            "[ACTV-FN-501] Orchestration scheduled: instanceId={InstanceId}", instanceId);
    }
}
