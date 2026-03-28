namespace RealEstateStar.Domain.Cma.Interfaces;

public interface IPdfDataService
{
    Task<string> StorePdfAsync(string leadName, string leadId, byte[] pdfBytes, CancellationToken ct);
}
