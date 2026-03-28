using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Lead.CMA;

public sealed class CmaProcessingChannel : ProcessingChannelBase<CmaProcessingRequest>
{
    public CmaProcessingChannel() : base(50) { }
}

public sealed record CmaProcessingRequest(
    string AgentId,
    global::RealEstateStar.Domain.Leads.Models.Lead Lead,
    AgentNotificationConfig AgentConfig,
    string CorrelationId,
    TaskCompletionSource<CmaWorkerResult> Completion);
