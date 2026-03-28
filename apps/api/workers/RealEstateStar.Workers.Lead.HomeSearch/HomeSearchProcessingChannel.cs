using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Lead.HomeSearch;

public sealed class HomeSearchProcessingChannel : ProcessingChannelBase<HomeSearchProcessingRequest>
{
    public HomeSearchProcessingChannel() : base(50) { }
}

public sealed record HomeSearchProcessingRequest(
    string AgentId,
    RealEstateStar.Domain.Leads.Models.Lead Lead,
    AgentNotificationConfig AgentConfig,
    string CorrelationId,
    TaskCompletionSource<HomeSearchWorkerResult> Completion);
