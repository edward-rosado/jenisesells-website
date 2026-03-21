using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Privacy.Interfaces;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.Privacy;

public class MarketingConsentLog(IFileStorageProvider fileStorageProvider, ILogger<MarketingConsentLog> logger) : IMarketingConsentLog
{
    public async Task RecordConsentAsync(string agentId, MarketingConsent consent, CancellationToken ct)
    {
        logger.LogInformation(
            "[CONSENT-001] Recording consent for lead {LeadId}. OptedIn: {OptedIn}, Channels: {Channels}",
            consent.LeadId, consent.OptedIn, string.Join(",", consent.Channels));
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

    public Task RedactAsync(string agentId, string email, CancellationToken ct) =>
        fileStorageProvider.RedactRowsAsync(
            LeadPaths.ConsentLogSheet,
            email,
            email,
            "[REDACTED]",
            ct);
}
