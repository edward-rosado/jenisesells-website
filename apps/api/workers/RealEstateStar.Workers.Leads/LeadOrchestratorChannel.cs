using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Leads;

public record LeadOrchestrationRequest(
    string AgentId,
    Lead Lead,
    string CorrelationId);

public sealed class LeadOrchestratorChannel : ProcessingChannelBase<LeadOrchestrationRequest>
{
    public LeadOrchestratorChannel() : base(capacity: 100) { }
}
