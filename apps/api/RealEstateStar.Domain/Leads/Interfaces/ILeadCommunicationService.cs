using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Domain.Leads.Interfaces;

public interface ILeadCommunicationService
{
    Task<CommunicationRecord> DraftAsync(LeadPipelineContext ctx, CancellationToken ct);
    Task<CommunicationRecord> SendAsync(CommunicationRecord draft, LeadPipelineContext ctx, CancellationToken ct);
}
