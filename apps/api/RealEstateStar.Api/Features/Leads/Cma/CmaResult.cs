namespace RealEstateStar.Api.Features.Leads.Cma;

/// <summary>
/// Output of the CMA pipeline — everything needed to store and notify.
/// </summary>
public class CmaResult
{
    public required CmaAnalysis Analysis { get; init; }
    public required List<Comp> Comps { get; init; }
    public required ReportType ReportType { get; init; }
    public required string PdfPath { get; init; }
    public string? DriveLink { get; init; }
}
