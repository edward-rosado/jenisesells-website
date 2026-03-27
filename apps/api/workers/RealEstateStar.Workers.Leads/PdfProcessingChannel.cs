using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Leads;

public record PdfProcessingRequest(
    string LeadId,
    CmaWorkerResult CmaResult,
    AgentNotificationConfig AgentConfig,
    string CorrelationId,
    TaskCompletionSource<PdfWorkerResult> Completion);

public sealed class PdfProcessingChannel : ProcessingChannelBase<PdfProcessingRequest>
{
    public PdfProcessingChannel() : base(capacity: 20) { }
}
