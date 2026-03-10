using RealEstateStar.Api.Features.Cma;

namespace RealEstateStar.Api.Services.Analysis;

public interface IAnalysisService
{
    Task<CmaAnalysis> AnalyzeAsync(Lead lead, List<Comp> comps, LeadResearch? research, ReportType reportType, CancellationToken ct);
}
