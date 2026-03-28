using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Cma.Download;

internal sealed record CmaErrorResponse(string Error);

public class DownloadCmaEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapGet("/accounts/{accountId}/agents/{agentId}/leads/{leadId}/cma/download", Handle)
            .WithName("DownloadCma")
            .WithTags("CMA");

    internal static async Task<IResult> Handle(
        string accountId,
        string agentId,
        Guid leadId,
        IDocumentStorageProvider documentStorage,
        ILogger<DownloadCmaEndpoint> logger,
        CancellationToken ct)
    {
        // The PDF blob is named {leadId}-CMA-Report.pdf.b64 under the lead's folder.
        // Since we don't know the lead's folder path from just the leadId, scan all lead
        // folders for a blob matching the leadId pattern.
        var targetBlobName = $"{leadId}-CMA-Report.pdf.b64";

        // List all documents under the leads root to find the PDF
        string? base64Content = null;
        try
        {
            var allDocs = await documentStorage.ListDocumentsAsync("Real Estate Star/1 - Leads", ct);
            var match = allDocs.FirstOrDefault(d => d.Contains(targetBlobName, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                // The ListDocumentsAsync returns paths relative to the prefix — reconstruct full path
                base64Content = await documentStorage.ReadDocumentAsync(
                    "Real Estate Star/1 - Leads", match, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CMA-DL-020] Failed to search for CMA PDF. LeadId={LeadId}", leadId);
            return Results.Problem("Failed to retrieve CMA report.", statusCode: StatusCodes.Status500InternalServerError);
        }

        if (base64Content is null)
        {
            logger.LogWarning("[CMA-DL-011] No CMA PDF found for lead. LeadId={LeadId}", leadId);
            return Results.NotFound(new CmaErrorResponse("CMA report not yet generated"));
        }

        byte[] pdfBytes;
        try
        {
            pdfBytes = Convert.FromBase64String(base64Content);
        }
        catch (FormatException ex)
        {
            logger.LogError(ex, "[CMA-DL-020] Base64 decode failed. LeadId={LeadId}", leadId);
            return Results.Problem("CMA report data is corrupted.", statusCode: StatusCodes.Status500InternalServerError);
        }

        logger.LogInformation("[CMA-DL-001] PDF downloaded. LeadId={LeadId}, SizeKB={SizeKB}", leadId, pdfBytes.Length / 1024);
        return Results.File(pdfBytes, "application/pdf", "CMA-Report.pdf");
    }
}
