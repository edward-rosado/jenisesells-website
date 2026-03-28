namespace RealEstateStar.Domain.Privacy.Interfaces;

public interface ILeadExportDataService
{
    Task<LeadExportData?> GatherAsync(string agentId, string email, CancellationToken ct);
}
