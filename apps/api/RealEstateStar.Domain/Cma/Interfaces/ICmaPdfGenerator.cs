using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Domain.Cma.Interfaces;

public interface ICmaPdfGenerator
{
    /// <summary>
    /// Generates a CMA PDF report and returns the path to the temp file.
    /// </summary>
    Task<string> GenerateAsync(Lead lead, CmaAnalysis analysis, List<Comp> comps, AccountConfig agent, ReportType reportType, CancellationToken ct);
}
