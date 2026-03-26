using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Shared.Context;

namespace RealEstateStar.Workers.Cma;

public sealed class CmaProcessingWorker(
    CmaProcessingChannel channel,
    ICompAggregator compAggregator,
    RentCastCompSource rentCastCompSource,
    ICmaAnalyzer cmaAnalyzer,
    ICmaPdfGenerator pdfGenerator,
    ICmaNotifier cmaNotifier,
    IAccountConfigService accountConfigService,
    IDocumentStorageProvider documentStorage,
    BackgroundServiceHealthTracker healthTracker,
    ILogger<CmaProcessingWorker> logger,
    IConfiguration configuration)
    : PipelineWorker<CmaProcessingRequest, CmaPipelineContext>(
        channel, healthTracker, logger,
        configuration.GetSection("Pipeline:Cma:Retry").Get<PipelineRetryOptions>())
{
    protected override string WorkerName => "CmaWorker";

    protected override CmaPipelineContext CreateContext(CmaProcessingRequest request) => new()
    {
        Request = request.Lead,
        AgentId = request.AgentId,
        CorrelationId = request.CorrelationId,
    };

    protected override async Task ProcessAsync(CmaPipelineContext ctx, CancellationToken ct)
    {
        using var activity = CmaDiagnostics.ActivitySource.StartActivity("cma.process");
        activity?.SetTag("lead.id", ctx.Request.Id.ToString());
        activity?.SetTag("lead.agent_id", ctx.AgentId);
        activity?.SetTag("correlation.id", ctx.CorrelationId);

        await RunStepAsync(ctx, CmaPipelineContext.StepFetchComps, () => FetchCompsAsync(ctx, ct), ct);

        if (ctx.Comps is null || ctx.Comps.Count == 0)
        {
            logger.LogWarning("[CmaWorker] No comps found for lead {LeadId}. Skipping CMA. CorrelationId: {CorrelationId}",
                ctx.Request.Id, ctx.CorrelationId);
            return;
        }

        await RunStepAsync(ctx, CmaPipelineContext.StepEnrichSubject, () => EnrichSubjectAsync(ctx, ct), ct);
        await RunStepAsync(ctx, CmaPipelineContext.StepAnalyze, () => AnalyzeAsync(ctx, ct), ct);
        await RunStepAsync(ctx, CmaPipelineContext.StepGeneratePdf, () => GeneratePdfAsync(ctx, ct), ct);
        await StorePdfAsync(ctx, ct);
        await RunStepAsync(ctx, CmaPipelineContext.StepNotifySeller, () => NotifySellerAsync(ctx, ct), ct);

        CmaDiagnostics.CmaGenerated.Add(1);
        CmaDiagnostics.TotalDuration.Record(ctx.PipelineDurationMs ?? 0);
    }

    private Task EnrichSubjectAsync(CmaPipelineContext ctx, CancellationToken ct)
    {
        var subject = rentCastCompSource.LastValuation?.SubjectProperty;
        if (subject is null)
        {
            logger.LogInformation("[CMA-ENRICH-001] No RentCast subject property available; skipping enrichment. Lead={LeadId}",
                ctx.Request.Id);
            return Task.CompletedTask;
        }

        var seller = ctx.Request.SellerDetails!;
        var filledBeds = seller.Beds is null && subject.Bedrooms.HasValue;
        var filledBaths = seller.Baths is null && subject.Bathrooms.HasValue;
        var filledSqft = seller.Sqft is null && subject.SquareFootage.HasValue;

        if (!filledBeds && !filledBaths && !filledSqft)
            return Task.CompletedTask;

        ctx.Request.SellerDetails = seller with
        {
            Beds = filledBeds ? subject.Bedrooms : seller.Beds,
            Baths = filledBaths ? (int)Math.Round(subject.Bathrooms!.Value, MidpointRounding.AwayFromZero) : seller.Baths,
            Sqft = filledSqft ? subject.SquareFootage : seller.Sqft
        };

        logger.LogInformation(
            "[CMA-ENRICH-001] Enriched subject property from RentCast. Lead={LeadId} Beds={Beds} Baths={Baths} Sqft={Sqft}",
            ctx.Request.Id,
            filledBeds ? $"{subject.Bedrooms} (filled)" : $"{seller.Beds} (from lead)",
            filledBaths ? $"{subject.Bathrooms} (filled)" : $"{seller.Baths} (from lead)",
            filledSqft ? $"{subject.SquareFootage} (filled)" : $"{seller.Sqft} (from lead)");

        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private async Task FetchCompsAsync(CmaPipelineContext ctx, CancellationToken ct)
    {
        var seller = ctx.Request.SellerDetails!;
        var searchRequest = new CompSearchRequest
        {
            Address = seller.Address,
            City = seller.City,
            State = seller.State,
            Zip = seller.Zip,
            Beds = seller.Beds,
            Baths = seller.Baths,
            SqFt = seller.Sqft
        };

        ctx.Comps = await compAggregator.FetchCompsAsync(searchRequest, ct);
        CmaDiagnostics.CompsFound.Record(ctx.Comps.Count);
    }

    private async Task AnalyzeAsync(CmaPipelineContext ctx, CancellationToken ct)
    {
        ctx.Analysis = await cmaAnalyzer.AnalyzeAsync(ctx.Request, ctx.Comps!, ct);
    }

    private async Task GeneratePdfAsync(CmaPipelineContext ctx, CancellationToken ct)
    {
        var accountConfig = await accountConfigService.GetAccountAsync(ctx.AgentId, ct)
            ?? throw new InvalidOperationException($"Account config not found for agent {ctx.AgentId}");

        var reportType = DetermineReportType(ctx.Comps!.Count);
        var pdfPath = await pdfGenerator.GenerateAsync(ctx.Request, ctx.Analysis!, ctx.Comps!, accountConfig, reportType, ct);
        ctx.Set("pdf-path", pdfPath);
    }

    private async Task StorePdfAsync(CmaPipelineContext ctx, CancellationToken ct)
    {
        var pdfPath = ctx.Get<string>("pdf-path");
        if (pdfPath is null || !File.Exists(pdfPath)) return;

        try
        {
            var seller = ctx.Request.SellerDetails!;
            var folder = $"Real Estate Star/1 - Leads/{ctx.Request.FullName}/{seller.Address}, {seller.City}, {seller.State} {seller.Zip}";
            var fileName = $"{DateTime.UtcNow:yyyy-MM-dd}-CMA-Report.pdf.b64";
            var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);

            // Store as base64 since IDocumentStorageProvider only supports text content.
            // The .b64 extension signals this needs base64-decoding to get the original PDF.
            await documentStorage.WriteDocumentAsync(folder, fileName, Convert.ToBase64String(pdfBytes), ct);

            logger.LogInformation(
                "[CmaWorker-020] CMA PDF stored. Lead: {LeadId}, Size: {SizeKB}KB",
                ctx.Request.Id, pdfBytes.Length / 1024);
        }
        catch (Exception ex)
        {
            // Best-effort — don't fail the pipeline if storage fails
            logger.LogWarning(ex,
                "[CmaWorker-021] Failed to store CMA PDF. Lead: {LeadId}",
                ctx.Request.Id);
        }
    }

    private async Task NotifySellerAsync(CmaPipelineContext ctx, CancellationToken ct)
    {
        var pdfPath = ctx.Get<string>("pdf-path")
            ?? throw new InvalidOperationException("PDF path not set");

        try
        {
            await cmaNotifier.NotifySellerAsync(ctx.AgentId, ctx.Request, pdfPath, ctx.Analysis!, ctx.CorrelationId, ct);
        }
        finally
        {
            TryDeleteTempFile(pdfPath);
        }
    }

    internal static ReportType DetermineReportType(int compCount) =>
        compCount switch
        {
            >= 5 => ReportType.Comprehensive,
            >= 3 => ReportType.Standard,
            _ => ReportType.Lean
        };

    private void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CmaWorker] Failed to delete temp PDF: {FileName}", Path.GetFileName(path));
        }
    }
}
