using UglyToad.PdfPig;

namespace RealEstateStar.Workers.Activation.DriveIndex;

internal static class PdfPageExtractor
{
    /// <summary>
    /// Validates the PDF is readable and returns it as a single image entry.
    /// Claude Vision supports PDF directly as application/pdf, so PdfPig just
    /// validates the file before sending the raw bytes to Claude.
    /// </summary>
    internal static IReadOnlyList<(byte[] Data, string MimeType)> ExtractPageImages(byte[] pdfBytes, int maxPages)
    {
        if (pdfBytes is null || pdfBytes.Length == 0)
            return Array.Empty<(byte[], string)>();

        try
        {
            using var document = PdfDocument.Open(pdfBytes);
            // Claude Vision supports PDF directly — send raw bytes as application/pdf
            return [(pdfBytes, "application/pdf")];
        }
        catch
        {
            return Array.Empty<(byte[], string)>();
        }
    }
}
