using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Pdf;

public interface ICmaPdfGenerator
{
    void Generate(PdfGenerationRequest request, CancellationToken ct);
}
