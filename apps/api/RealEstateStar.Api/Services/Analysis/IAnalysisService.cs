using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.Submit;

namespace RealEstateStar.Api.Services.Analysis;

public interface IAnalysisService
{
    Task<CmaAnalysis> AnalyzeAsync(Lead lead, List<Comp> comps, LeadResearch? research, ReportType reportType, CancellationToken ct);
}
