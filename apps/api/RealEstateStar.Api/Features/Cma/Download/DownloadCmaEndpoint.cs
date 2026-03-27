using Microsoft.AspNetCore.Http.HttpResults;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

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
        ILeadStore leadStore,
        IDocumentStorageProvider documentStorage,
        ILogger<DownloadCmaEndpoint> logger,
        CancellationToken ct)
    {
        // 1. Look up the lead to get FullName and SellerDetails for path resolution
        var lead = await leadStore.GetAsync(agentId, leadId, ct);
        if (lead is null)
        {
            logger.LogWarning("[CMA-DL-010] Lead not found. AgentId={AgentId}, LeadId={LeadId}", agentId, leadId);
            return Results.NotFound(new CmaErrorResponse("Lead not found"));
        }

        if (lead.SellerDetails is null)
        {
            logger.LogWarning("[CMA-DL-011] Lead has no seller details — no CMA can exist. AgentId={AgentId}, LeadId={LeadId}", agentId, leadId);
            return Results.NotFound(new CmaErrorResponse("CMA report not yet generated"));
        }

        // 2. Build the folder path matching how CmaProcessingWorker stores the PDF
        var seller = lead.SellerDetails;
        var folder = $"Real Estate Star/1 - Leads/{lead.FullName}/{seller.Address}, {seller.City}, {seller.State} {seller.Zip}";

        // 3. List blobs in the folder and find the CMA PDF
        List<string> documents;
        try
        {
            documents = await documentStorage.ListDocumentsAsync(folder, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CMA-DL-020] Failed to list documents in folder. AgentId={AgentId}, LeadId={LeadId}, Folder={Folder}",
                agentId, leadId, folder);
            return Results.Problem("Failed to retrieve CMA report.", statusCode: StatusCodes.Status500InternalServerError);
        }

        var pdfFileName = documents
            .Where(d => d.EndsWith("-CMA-Report.pdf.b64", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d)
            .FirstOrDefault();

        if (pdfFileName is null)
        {
            logger.LogWarning("[CMA-DL-011] No CMA PDF blob found. AgentId={AgentId}, LeadId={LeadId}, Folder={Folder}",
                agentId, leadId, folder);
            return Results.NotFound(new CmaErrorResponse("CMA report not yet generated"));
        }

        // 4. Download and decode the base64-encoded PDF
        string? base64Content;
        try
        {
            base64Content = await documentStorage.ReadDocumentAsync(folder, pdfFileName, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CMA-DL-020] Failed to read PDF blob. AgentId={AgentId}, LeadId={LeadId}, File={FileName}",
                agentId, leadId, pdfFileName);
            return Results.Problem("Failed to retrieve CMA report.", statusCode: StatusCodes.Status500InternalServerError);
        }

        if (base64Content is null)
        {
            logger.LogWarning("[CMA-DL-011] PDF blob was empty. AgentId={AgentId}, LeadId={LeadId}, File={FileName}",
                agentId, leadId, pdfFileName);
            return Results.NotFound(new CmaErrorResponse("CMA report not yet generated"));
        }

        byte[] pdfBytes;
        try
        {
            pdfBytes = Convert.FromBase64String(base64Content);
        }
        catch (FormatException ex)
        {
            logger.LogError(ex, "[CMA-DL-020] Base64 decode failed. AgentId={AgentId}, LeadId={LeadId}, File={FileName}",
                agentId, leadId, pdfFileName);
            return Results.Problem("CMA report data is corrupted.", statusCode: StatusCodes.Status500InternalServerError);
        }

        logger.LogInformation("[CMA-DL-001] PDF downloaded successfully. AgentId={AgentId}, LeadId={LeadId}, SizeKB={SizeKB}",
            agentId, leadId, pdfBytes.Length / 1024);

        return Results.File(pdfBytes, "application/pdf", "CMA-Report.pdf");
    }
}
