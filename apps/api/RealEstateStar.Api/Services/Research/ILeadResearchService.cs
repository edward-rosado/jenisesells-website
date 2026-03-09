using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Research;

public interface ILeadResearchService
{
    Task<LeadResearch> ResearchAsync(Lead lead);
}
