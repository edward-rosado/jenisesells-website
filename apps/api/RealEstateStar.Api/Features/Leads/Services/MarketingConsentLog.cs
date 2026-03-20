using RealEstateStar.Api.Services.Storage;

namespace RealEstateStar.Api.Features.Leads.Services;

public class MarketingConsentLog(IFileStorageProvider fileStorageProvider) : IMarketingConsentLog
{
    public async Task RecordConsentAsync(string agentId, MarketingConsent consent, CancellationToken ct)
    {
        var row = new List<string>
        {
            consent.Timestamp.ToString("o"),
            consent.LeadId.ToString(),
            consent.Email,
            consent.FirstName,
            consent.LastName,
            consent.OptedIn.ToString(),
            consent.ConsentText,
            string.Join(",", consent.Channels),
            consent.IpAddress,
            consent.UserAgent
        };

        await fileStorageProvider.AppendRowAsync(LeadPaths.ConsentLogSheet, row, ct);
    }
}
