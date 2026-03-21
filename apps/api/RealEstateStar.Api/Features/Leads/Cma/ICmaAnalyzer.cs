namespace RealEstateStar.Api.Features.Leads.Cma;

public interface ICmaAnalyzer
{
    Task<CmaAnalysis> AnalyzeAsync(Lead lead, List<Comp> comps, CancellationToken ct);
}
