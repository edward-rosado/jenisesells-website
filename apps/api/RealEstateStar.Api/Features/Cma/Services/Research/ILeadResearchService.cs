using RealEstateStar.Api.Features.Cma;

namespace RealEstateStar.Api.Features.Cma.Services.Research;

public interface ILeadResearchService
{
    Task<LeadResearch> ResearchAsync(Lead lead, CancellationToken ct);
}
