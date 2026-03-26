using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Domain.Cma.Interfaces;

public interface ICmaPdfGenerator
{
    /// <summary>
    /// Generates a CMA PDF report and returns the path to the temp file.
    /// </summary>
    /// <param name="lead">The seller lead.</param>
    /// <param name="analysis">Claude's CMA analysis output.</param>
    /// <param name="comps">Comparable sales used in the analysis.</param>
    /// <param name="config">Agent account config (branding, contact info, etc.).</param>
    /// <param name="reportType">Lean / Standard / Comprehensive.</param>
    /// <param name="logoBytes">Brokerage logo image bytes, or null if unavailable.</param>
    /// <param name="headshotBytes">Agent headshot image bytes, or null if unavailable.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Path to the generated temp PDF file.</returns>
    Task<string> GenerateAsync(
        Lead lead,
        CmaAnalysis analysis,
        List<Comp> comps,
        AccountConfig config,
        ReportType reportType,
        byte[]? logoBytes,
        byte[]? headshotBytes,
        CancellationToken ct);
}
