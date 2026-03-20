namespace RealEstateStar.Api.Features.Leads.Services;

public interface IMarketingConsentLog
{
    Task RecordConsentAsync(string agentId, MarketingConsent consent, CancellationToken ct);
    Task RedactAsync(string agentId, string email, CancellationToken ct);
}
