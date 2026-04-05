using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Functions.Lead.Models;
using DomainLead = RealEstateStar.Domain.Leads.Models.Lead;
using DomainLeadType = RealEstateStar.Domain.Leads.Models.LeadType;
using ILeadStore = RealEstateStar.Domain.Leads.Interfaces.ILeadStore;

namespace RealEstateStar.Functions.Lead;

/// <summary>
/// Queue-triggered function that starts the Durable Functions lead orchestration.
/// Reads <see cref="LeadOrchestrationMessage"/> from the "lead-requests" queue,
/// loads the lead to compute routing flags and content hashes, then schedules
/// <see cref="LeadOrchestratorFunction"/>.
/// </summary>
/// <remarks>
/// Feature flag: <c>Features:Lead:UseBackgroundService</c>
/// When true → this function exits immediately (BackgroundService handles it instead).
/// When false (default for Functions host) → schedules the Durable orchestration.
///
/// Instance ID: <c>lead-{agentId}-{leadId}</c>
/// Deterministic — re-queuing the same lead attaches to the existing orchestration
/// rather than starting a duplicate. Durable Functions de-duplicates at the
/// <see cref="DurableTaskClient.ScheduleNewOrchestrationInstanceAsync"/> call.
///
/// Backpressure: Azure Queue Storage handles backpressure via queue depth and
/// <see cref="host.json"/> <c>newBatchThreshold</c> / <c>maxDequeueCount</c>.
/// Channel&lt;T&gt; fan-out is no longer needed — the Functions runtime auto-scales
/// by adding more instances as the queue grows.
/// </remarks>
public sealed class StartLeadProcessingFunction(
    ILeadStore leadStore,
    IConfiguration configuration,
    ILogger<StartLeadProcessingFunction> logger)
{
    private const string QueueName = "lead-requests";

    [Function("StartLeadProcessing")]
    public async Task RunAsync(
        [QueueTrigger(QueueName)] LeadOrchestrationMessage message,
        [DurableClient] DurableTaskClient client,
        CancellationToken ct)
    {
        // Feature flag: opt-out of Functions orchestration when BackgroundService is still active
        var useBackgroundService = configuration.GetValue<bool>("Features:Lead:UseBackgroundService");
        if (useBackgroundService)
        {
            logger.LogDebug(
                "[SLP-000] Features:Lead:UseBackgroundService=true — skipping Durable orchestration for lead {LeadId}.",
                message.LeadId);
            return;
        }

        // Load lead to compute routing flags (seller → CMA, buyer → HomeSearch)
        var lead = await leadStore.GetAsync(message.AgentId, Guid.Parse(message.LeadId), ct);
        if (lead is null)
        {
            logger.LogWarning(
                "[SLP-002] Lead {LeadId} not found for agent {AgentId}. Dropping message. CorrelationId={CorrelationId}",
                message.LeadId, message.AgentId, message.CorrelationId);
            return;
        }

        var cmaInputHash = ComputeCmaInputHash(lead);
        var hsInputHash = ComputeHsInputHash(lead);

        var orchestratorInput = new LeadOrchestratorInput
        {
            AgentId = message.AgentId,
            LeadId = message.LeadId,
            CorrelationId = message.CorrelationId,
            ShouldRunCma = lead.LeadType is DomainLeadType.Seller or DomainLeadType.Both && lead.SellerDetails is not null,
            ShouldRunHomeSearch = lead.LeadType is DomainLeadType.Buyer or DomainLeadType.Both && lead.BuyerDetails is not null,
            CmaInputHash = cmaInputHash,
            HsInputHash = hsInputHash,
            Locale = lead.Locale
        };

        // Deterministic instance ID prevents duplicate orchestrations for the same lead
        var instanceId = $"lead-{message.AgentId}-{message.LeadId}";

        logger.LogInformation(
            "[SLP-010] Scheduling LeadOrchestrator. InstanceId={InstanceId}, LeadId={LeadId}, " +
            "ShouldRunCma={ShouldRunCma}, ShouldRunHomeSearch={ShouldRunHomeSearch}, CorrelationId={CorrelationId}",
            instanceId, message.LeadId,
            orchestratorInput.ShouldRunCma, orchestratorInput.ShouldRunHomeSearch,
            message.CorrelationId);

        await client.ScheduleNewOrchestrationInstanceAsync(
            "LeadOrchestrator",
            orchestratorInput,
            new Microsoft.DurableTask.StartOrchestrationOptions(instanceId),
            ct);

        logger.LogInformation("[SLP-020] LeadOrchestrator scheduled. InstanceId={InstanceId}", instanceId);
    }

    // Content hash helpers — must match LeadOrchestrator.ComputeCmaInputHash / ComputeHsInputHash
    internal static string ComputeCmaInputHash(DomainLead lead) =>
        ContentHash.Compute(
            lead.SellerDetails?.Address,
            lead.SellerDetails?.City,
            lead.SellerDetails?.State,
            lead.SellerDetails?.Zip);

    internal static string ComputeHsInputHash(DomainLead lead) =>
        ContentHash.Compute(
            lead.BuyerDetails?.City,
            lead.BuyerDetails?.State,
            lead.BuyerDetails?.MinBudget?.ToString(),
            lead.BuyerDetails?.MaxBudget?.ToString(),
            lead.BuyerDetails?.Bedrooms?.ToString(),
            lead.BuyerDetails?.Bathrooms?.ToString());
}
