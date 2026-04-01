using FluentAssertions;

namespace RealEstateStar.Workers.Activation.DriveIndex.Tests;

public class PdfPageExtractorTests
{
    // Minimal valid 1-page PDF as bytes (hand-constructed PDF 1.4 structure)
    private static readonly byte[] MinimalValidPdf = System.Text.Encoding.Latin1.GetBytes(
        "%PDF-1.4\n" +
        "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
        "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
        "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n" +
        "xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \n" +
        "trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n190\n%%EOF\n");

    // ── Null / empty guards ────────────────────────────────────────────────────

    [Fact]
    public void ExtractPageImages_NullBytes_ReturnsEmpty()
    {
        var result = PdfPageExtractor.ExtractPageImages(null!, maxPages: 5);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractPageImages_EmptyBytes_ReturnsEmpty()
    {
        var result = PdfPageExtractor.ExtractPageImages(Array.Empty<byte>(), maxPages: 5);

        result.Should().BeEmpty();
    }

    // ── Invalid PDF guard ──────────────────────────────────────────────────────

    [Fact]
    public void ExtractPageImages_InvalidPdfBytes_ReturnsEmpty()
    {
        var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE };

        var result = PdfPageExtractor.ExtractPageImages(garbage, maxPages: 5);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractPageImages_RandomTextBytes_ReturnsEmpty()
    {
        var notPdf = System.Text.Encoding.UTF8.GetBytes("This is not a PDF file at all.");

        var result = PdfPageExtractor.ExtractPageImages(notPdf, maxPages: 5);

        result.Should().BeEmpty();
    }

    // ── Valid PDF behavior ─────────────────────────────────────────────────────

    [Fact]
    public void ExtractPageImages_ValidPdf_ReturnsSingleEntry()
    {
        var result = PdfPageExtractor.ExtractPageImages(MinimalValidPdf, maxPages: 5);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void ExtractPageImages_ValidPdf_ReturnsPdfMimeType()
    {
        var result = PdfPageExtractor.ExtractPageImages(MinimalValidPdf, maxPages: 5);

        result[0].MimeType.Should().Be("application/pdf");
    }

    [Fact]
    public void ExtractPageImages_ValidPdf_ReturnsOriginalBytes()
    {
        var result = PdfPageExtractor.ExtractPageImages(MinimalValidPdf, maxPages: 5);

        result[0].Data.Should().BeSameAs(MinimalValidPdf);
    }

    [Fact]
    public void ExtractPageImages_ValidPdf_MaxPagesDoesNotAffectSinglePdfReturn()
    {
        // maxPages param is accepted but PdfPig path sends whole PDF as one entry
        var result1 = PdfPageExtractor.ExtractPageImages(MinimalValidPdf, maxPages: 1);
        var result2 = PdfPageExtractor.ExtractPageImages(MinimalValidPdf, maxPages: 10);

        result1.Should().HaveCount(1);
        result2.Should().HaveCount(1);
    }
}
