using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.Cma;

public class PdfDataService(IDocumentStorageProvider storage) : IPdfDataService
{
    public async Task<string> StorePdfAsync(string leadName, string leadId, byte[] pdfBytes, CancellationToken ct)
    {
        var folder = $"Real Estate Star/1 - Leads/{leadName}/CMA";
        var fileName = $"{DateTime.UtcNow:yyyy-MM-dd}-{leadId}-CMA-Report.pdf.b64";
        var base64 = Convert.ToBase64String(pdfBytes);

        await storage.WriteDocumentAsync(folder, fileName, base64, ct);
        return $"{folder}/{fileName}";
    }
}
