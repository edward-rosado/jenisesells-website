using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Domain.Privacy.Interfaces;

public interface IConsentAuditService
{
    Task RecordAsync(string agentId, MarketingConsent consent, string hmacSignature, CancellationToken ct);
}
