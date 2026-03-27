using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Shared.Pdf;

/// <summary>
/// Reusable activity that generates a CMA PDF and persists it via
/// <see cref="IDocumentStorageProvider"/>. Designed for direct invocation
/// from any pipeline step — not a BackgroundService.
/// </summary>
public class PdfActivity(
    ICmaPdfGenerator generator,
    IDocumentStorageProvider storage,
    ILogger<PdfActivity> logger)
{
    /// <summary>
    /// Generates a CMA PDF for the given lead and analysis, writes it to
    /// document storage as base-64, and returns the storage path.
    /// </summary>
    /// <returns>The storage path of the persisted PDF (e.g. "folder/file.pdf.b64").</returns>
    public async Task<string> ExecuteAsync(
        Lead lead,
        CmaAnalysis analysis,
        List<Comp> comps,
        AccountConfig? accountConfig,
        ReportType reportType,
        byte[]? logoBytes,
        byte[]? headshotBytes,
        string correlationId,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[PDF-ACT-001] Generating CMA PDF. LeadId: {LeadId}, ReportType: {ReportType}, CorrelationId: {CorrelationId}",
            lead.Id, reportType, correlationId);

        var config = accountConfig ?? new AccountConfig { Handle = lead.AgentId };
        var tempPath = await generator.GenerateAsync(lead, analysis, comps, config, reportType,
            logoBytes, headshotBytes, ct);

        var storagePath = await StorePdfAsync(lead.Id.ToString(), tempPath, ct);

        logger.LogInformation(
            "[PDF-ACT-002] CMA PDF generated and stored. LeadId: {LeadId}, Path: {Path}, CorrelationId: {CorrelationId}",
            lead.Id, storagePath, correlationId);

        return storagePath;
    }

    internal async Task<string> StorePdfAsync(string leadId, string tempFilePath, CancellationToken ct)
    {
        var timestamp = DateTime.UtcNow;
        var folder = $"Real Estate Star/1 - Leads/{leadId}/CMA";
        var fileName = $"{timestamp:yyyy-MM-dd}-{leadId}-CMA-Report.pdf.b64";

        var pdfBytes = await File.ReadAllBytesAsync(tempFilePath, ct);

        await storage.WriteDocumentAsync(
            folder,
            fileName,
            Convert.ToBase64String(pdfBytes),
            ct);

        return $"{folder}/{fileName}";
    }
}
