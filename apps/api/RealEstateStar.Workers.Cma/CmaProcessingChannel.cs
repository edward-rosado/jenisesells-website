using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Cma;

public sealed class CmaProcessingChannel : ProcessingChannelBase<CmaProcessingRequest>
{
    public CmaProcessingChannel() : base(50) { }
}

public sealed record CmaProcessingRequest(
    string AgentId,
    Lead Lead,
    string CorrelationId);
