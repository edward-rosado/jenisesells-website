using RealEstateStar.Domain.Leads.Models;
namespace RealEstateStar.Domain.Privacy.Interfaces;

public interface IMarketingConsentDataService
{
    Task RecordConsentAsync(string agentId, MarketingConsent consent, CancellationToken ct);
    Task RedactAsync(string agentId, string email, CancellationToken ct);
}
