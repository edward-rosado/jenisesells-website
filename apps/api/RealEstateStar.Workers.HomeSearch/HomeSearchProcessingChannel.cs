using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.HomeSearch;

public sealed class HomeSearchProcessingChannel : ProcessingChannelBase<HomeSearchProcessingRequest>
{
    public HomeSearchProcessingChannel() : base(50) { }
}

public sealed record HomeSearchProcessingRequest(
    string AgentId,
    Lead Lead,
    string CorrelationId);
