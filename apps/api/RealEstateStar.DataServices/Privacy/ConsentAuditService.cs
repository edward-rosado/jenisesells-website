using System.Security.Cryptography;
using System.Text;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Privacy.Interfaces;

namespace RealEstateStar.DataServices.Privacy;

public class ConsentAuditService(
    TableClient tableClient,
    ILogger<ConsentAuditService> logger) : IConsentAuditService
{
    public async Task RecordAsync(string agentId, MarketingConsent consent, string hmacSignature, CancellationToken ct)
    {
        try
        {
            var entry = new ConsentAuditEntry
            {
                PartitionKey = agentId,
                RowKey = Guid.NewGuid().ToString(),
                EventTimestamp = consent.Timestamp,
                LeadId = consent.LeadId,
                EmailHash = ComputeSha256(consent.Email),
                OptedIn = consent.OptedIn,
                ConsentText = consent.ConsentText,
                Channels = string.Join(",", consent.Channels),
                Action = consent.Action.ToString(),
                Source = consent.Source.ToString(),
                HmacSignature = hmacSignature,
            };

            await tableClient.UpsertEntityAsync(entry, TableUpdateMode.Merge, ct);
            logger.LogInformation("[CONSENT-003] Consent audit recorded for lead {LeadId}", consent.LeadId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CONSENT-010] Azure Table consent audit write failed for lead {LeadId}", consent.LeadId);
        }
    }

    internal static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
