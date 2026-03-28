using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Lead.Orchestrator;

public record LeadOrchestrationRequest(
    string AgentId,
    RealEstateStar.Domain.Leads.Models.Lead Lead,
    string CorrelationId);

public sealed class LeadOrchestratorChannel : ProcessingChannelBase<LeadOrchestrationRequest>
{
    public LeadOrchestratorChannel() : base(capacity: 100) { }
}
