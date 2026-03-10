using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.Submit;

namespace RealEstateStar.Api.Services.Research;

public interface ILeadResearchService
{
    Task<LeadResearch> ResearchAsync(Lead lead, CancellationToken ct);
}
