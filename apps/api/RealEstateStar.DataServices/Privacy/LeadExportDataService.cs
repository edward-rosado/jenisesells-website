using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Privacy;
using RealEstateStar.Domain.Privacy.Interfaces;

namespace RealEstateStar.DataServices.Privacy;

public class LeadExportDataService(
    ILeadDataService leadStore,
    ILogger<LeadExportDataService> logger) : ILeadExportDataService
{
    public async Task<LeadExportData?> GatherAsync(string agentId, string email, CancellationToken ct)
    {
        var lead = await leadStore.GetByEmailAsync(agentId, email, ct);
        if (lead is null)
        {
            logger.LogInformation("[EXPORT-001] No lead found for email in agent {AgentId}", agentId);
            return null;
        }

        // For now, return profile only. Consent history and enrichment
        // will be populated when those stores support querying by lead.
        // TODO: Pipeline redesign — LeadEnrichment removed in Phase 1.5; consent history populated in Phase 2/3/4
        return new LeadExportData(lead, []);
    }
}
