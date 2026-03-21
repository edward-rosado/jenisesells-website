using RealEstateStar.Api.Common;

namespace RealEstateStar.Api.Features.Leads.Cma;

public interface ICmaPdfGenerator
{
    /// <summary>
    /// Generates a CMA PDF report and returns the path to the temp file.
    /// </summary>
    Task<string> GenerateAsync(Lead lead, CmaAnalysis analysis, List<Comp> comps, AccountConfig agent, ReportType reportType, CancellationToken ct);
}
