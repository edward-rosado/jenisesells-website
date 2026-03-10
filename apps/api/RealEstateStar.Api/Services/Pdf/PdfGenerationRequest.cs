using RealEstateStar.Api.Common;
using RealEstateStar.Api.Features.Cma;

namespace RealEstateStar.Api.Services.Pdf;

public record PdfGenerationRequest
{
    public required string OutputPath { get; init; }
    public required AgentConfig Agent { get; init; }
    public required Lead Lead { get; init; }
    public required List<Comp> Comps { get; init; }
    public required CmaAnalysis Analysis { get; init; }
    public LeadResearch? Research { get; init; }
    public required ReportType ReportType { get; init; }
}
