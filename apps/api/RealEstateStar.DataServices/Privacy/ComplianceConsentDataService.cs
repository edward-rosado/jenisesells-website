using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Privacy.Interfaces;

namespace RealEstateStar.DataServices.Privacy;

public class ComplianceConsentDataService(
    IComplianceFileStorageProvider storageProvider,
    ILogger<ComplianceConsentDataService> logger) : IComplianceConsentDataService
{
    public virtual async Task WriteAsync(string agentId, MarketingConsent consent, string hmacSignature, CancellationToken ct)
    {
        try
        {
            var path = $"compliance/{agentId}/consent-log";
            var row = MarketingConsentDataService.BuildCsvRow(consent, hmacSignature);
            await storageProvider.AppendRowAsync(path, row, ct);
            logger.LogInformation("[CONSENT-004] Compliance copy written for lead {LeadId}", consent.LeadId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CONSENT-011] Service-account Drive consent write failed for lead {LeadId}", consent.LeadId);
        }
    }
}
