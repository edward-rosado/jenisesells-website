using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Domain.Cma.Interfaces;

public interface ICmaAnalyzer
{
    Task<CmaAnalysis> AnalyzeAsync(Lead lead, List<Comp> comps, CancellationToken ct,
        AgentContext? agentContext = null);
}
