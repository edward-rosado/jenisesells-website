using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Domain.Privacy.Interfaces;

public interface IComplianceConsentDataService
{
    Task WriteAsync(string agentId, MarketingConsent consent, string hmacSignature, CancellationToken ct);
}
