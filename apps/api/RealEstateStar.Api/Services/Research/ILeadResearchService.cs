using RealEstateStar.Api.Features.Cma;

namespace RealEstateStar.Api.Services.Research;

public interface ILeadResearchService
{
    Task<LeadResearch> ResearchAsync(Lead lead, CancellationToken ct);
}
