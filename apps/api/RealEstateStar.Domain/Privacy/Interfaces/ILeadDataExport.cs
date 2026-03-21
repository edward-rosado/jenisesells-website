using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Domain.Privacy.Interfaces;

public interface ILeadDataExport
{
    Task<LeadExportData?> GatherAsync(string agentId, string email, CancellationToken ct);
}
