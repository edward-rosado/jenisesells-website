using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Pdf;

public interface ICmaPdfGenerator
{
    void Generate(string outputPath, AgentConfig agent, Lead lead,
        List<Comp> comps, CmaAnalysis analysis, LeadResearch? research,
        ReportType reportType, CancellationToken ct = default);
}
