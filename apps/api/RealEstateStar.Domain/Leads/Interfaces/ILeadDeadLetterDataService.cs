using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Domain.Leads.Interfaces;

public interface ILeadDeadLetterDataService
{
    Task RecordAsync(Lead lead, string operation, string lastError, CancellationToken ct);
}
