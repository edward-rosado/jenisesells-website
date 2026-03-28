using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RealEstateStar.DataServices.Privacy;

public class MarketingConsentLog(
    ISheetStorageProvider fileStorageProvider,
    IOptions<ConsentHmacOptions> hmacOptions,
    ILogger<MarketingConsentLog> logger) : IMarketingConsentLog
{
    public async Task RecordConsentAsync(string agentId, MarketingConsent consent, CancellationToken ct)
    {
        logger.LogInformation(
            "[CONSENT-001] Recording consent for lead {LeadId}. OptedIn: {OptedIn}, Channels: {Channels}",
            consent.LeadId, consent.OptedIn, string.Join(",", consent.Channels));

        var signature = ComputeHmacSignature(consent, hmacOptions.Value.Secret);
        var row = BuildCsvRow(consent, signature);

        await fileStorageProvider.AppendRowAsync(LeadPaths.ConsentLogSheet, row, ct);
    }

    public Task RedactAsync(string agentId, string email, CancellationToken ct) =>
        fileStorageProvider.RedactRowsAsync(
            LeadPaths.ConsentLogSheet,
            email,
            email,
            "[REDACTED]",
            ct);

    internal static List<string> BuildCsvRow(MarketingConsent consent, string hmacSignature) =>
    [
        consent.Timestamp.ToString("o"),
        consent.LeadId.ToString(),
        consent.Email,
        consent.FirstName,
        consent.LastName,
        consent.OptedIn.ToString(),
        consent.ConsentText,
        string.Join(",", consent.Channels),
        consent.IpAddress,
        consent.UserAgent,
        consent.Action.ToString(),
        consent.Source.ToString(),
        hmacSignature
    ];

    internal static string ComputeHmacSignature(MarketingConsent consent, string secret)
    {
        var payload = string.Join("|",
            consent.Timestamp.ToString("o"),
            consent.LeadId.ToString(),
            consent.Email,
            consent.FirstName,
            consent.LastName,
            consent.OptedIn.ToString(),
            consent.ConsentText,
            string.Join(",", consent.Channels),
            consent.IpAddress,
            consent.UserAgent,
            consent.Action.ToString(),
            consent.Source.ToString());

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
