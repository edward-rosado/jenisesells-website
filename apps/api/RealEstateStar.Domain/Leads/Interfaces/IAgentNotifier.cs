namespace RealEstateStar.Domain.Leads.Interfaces;

using RealEstateStar.Domain.Leads.Models;

public interface IAgentNotifier
{
    Task NotifyAsync(Lead lead, LeadScore score,
        CmaWorkerResult? cmaResult, HomeSearchWorkerResult? homeSearchResult,
        AgentNotificationConfig agentConfig, CancellationToken ct);
}
