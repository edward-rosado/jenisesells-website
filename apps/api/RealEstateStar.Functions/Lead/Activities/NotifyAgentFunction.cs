using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Functions.Lead.Models;

namespace RealEstateStar.Functions.Lead.Activities;

/// <summary>
/// Sends agent notification via <see cref="IAgentNotifier"/>.
/// Idempotency is already guarded inside the service (IIdempotencyStore with key
/// <c>lead:{agentId}-{leadId}:agent-notify</c>).
/// This activity is safe to retry — duplicate notifications are prevented at the service layer.
/// </summary>
public sealed class NotifyAgentFunction(
    ILeadStore leadStore,
    IAgentNotifier agentNotifier,
    ILogger<NotifyAgentFunction> logger)
{
    [Function("NotifyAgent")]
    public async Task RunAsync(
        [ActivityTrigger] NotifyAgentInput input,
        CancellationToken ct)
    {
        var leadId = Guid.Parse(input.LeadId);
        var lead = await leadStore.GetAsync(input.AgentId, leadId, ct)
            ?? throw new InvalidOperationException(
                $"[NAF-001] Lead {input.LeadId} not found. CorrelationId={input.CorrelationId}");

        logger.LogInformation("[NAF-010] Notifying agent for lead {LeadId}. Locale={Locale}. CorrelationId={CorrelationId}",
            input.LeadId, input.Locale ?? "en", input.CorrelationId);

        await agentNotifier.NotifyAsync(
            lead,
            input.Score,
            input.CmaResult,
            input.HsResult,
            input.AgentNotificationConfig,
            ct);

        logger.LogInformation("[NAF-020] Agent notified for lead {LeadId}. CorrelationId={CorrelationId}",
            input.LeadId, input.CorrelationId);
    }
}
