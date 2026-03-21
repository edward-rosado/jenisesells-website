using System.Diagnostics;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Api.Diagnostics;
using RealEstateStar.Api.Features.Leads.Cma;
using RealEstateStar.Api.Health;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Api.Features.Leads.Services;

/// <summary>
/// Background service that processes CMA requests from the <see cref="CmaProcessingChannel"/>.
/// Fetches comps, runs Claude analysis, generates PDF, and notifies the seller.
/// </summary>
public sealed class CmaProcessingWorker(
    CmaProcessingChannel channel,
    ICompAggregator compAggregator,
    ICmaAnalyzer cmaAnalyzer,
    ICmaPdfGenerator pdfGenerator,
    ICmaNotifier cmaNotifier,
    IAccountConfigService accountConfigService,
    BackgroundServiceHealthTracker healthTracker,
    ILogger<CmaProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[CMA-WORKER-001] CMA processing worker started.");

        await foreach (var request in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessCmaAsync(request, stoppingToken);
                healthTracker.RecordActivity(nameof(CmaProcessingWorker));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                CmaDiagnostics.CmaFailed.Add(1);
                logger.LogError(ex,
                    "[CMA-WORKER-002] Unhandled error processing CMA for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
                    request.Lead.Id, request.AgentId, request.CorrelationId);
            }
        }

        logger.LogInformation("[CMA-WORKER-003] CMA processing worker stopping.");
    }

    private async Task ProcessCmaAsync(CmaProcessingRequest request, CancellationToken ct)
    {
        var (agentId, lead, enrichment, score, correlationId) = request;
        var pipelineStart = Stopwatch.GetTimestamp();

        using var activity = CmaDiagnostics.ActivitySource.StartActivity("cma.process");
        activity?.SetTag("lead.id", lead.Id.ToString());
        activity?.SetTag("lead.agent_id", agentId);
        activity?.SetTag("correlation.id", correlationId);

        logger.LogInformation(
            "[CMA-WORKER-010] Starting CMA pipeline for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
            lead.Id, agentId, correlationId);

        var seller = lead.SellerDetails!;
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

        // Step 1: Fetch comparable sales
        var comps = await FetchCompsAsync(searchRequest, correlationId, ct);

        if (comps.Count == 0)
        {
            logger.LogWarning(
                "[CMA-WORKER-011] No comps found for lead {LeadId}. Skipping CMA generation. CorrelationId: {CorrelationId}",
                lead.Id, correlationId);
            return;
        }

        // Step 2: Analyze with Claude
        var analysis = await AnalyzeAsync(lead, comps, correlationId, ct);

        // Step 3: Load agent config and determine report type
        var accountConfig = await accountConfigService.GetAccountAsync(agentId, ct)
            ?? throw new InvalidOperationException($"[CMA-WORKER-012] Account config not found for agent {agentId}");

        var reportType = DetermineReportType(comps.Count, score);

        // Step 4: Generate PDF
        var pdfPath = await GeneratePdfAsync(lead, analysis, comps, accountConfig, reportType, correlationId, ct);

        // Step 5: Notify seller (email + Drive storage)
        await NotifySellerAsync(agentId, lead, pdfPath, analysis, correlationId, ct);

        CmaDiagnostics.CmaGenerated.Add(1);
        var totalMs = Stopwatch.GetElapsedTime(pipelineStart).TotalMilliseconds;
        CmaDiagnostics.TotalDuration.Record(totalMs);

        logger.LogInformation(
            "[CMA-WORKER-013] CMA pipeline complete for lead {LeadId} in {DurationMs}ms. ReportType: {ReportType}. Comps: {CompCount}. CorrelationId: {CorrelationId}",
            lead.Id, totalMs, reportType, comps.Count, correlationId);
    }

    private async Task<List<Comp>> FetchCompsAsync(CompSearchRequest request, string correlationId, CancellationToken ct)
    {
        var sw = Stopwatch.GetTimestamp();

        try
        {
            var comps = await compAggregator.FetchCompsAsync(request, ct);
            CmaDiagnostics.CompsFound.Record(comps.Count);

            logger.LogInformation(
                "[CMA-WORKER-020] Fetched {CompCount} comps for {Address}. Duration: {DurationMs}ms. CorrelationId: {CorrelationId}",
                comps.Count, request.Address, Stopwatch.GetElapsedTime(sw).TotalMilliseconds, correlationId);

            return comps;
        }
        finally
        {
            CmaDiagnostics.CompsDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    private async Task<CmaAnalysis> AnalyzeAsync(Lead lead, List<Comp> comps, string correlationId, CancellationToken ct)
    {
        var sw = Stopwatch.GetTimestamp();

        try
        {
            var analysis = await cmaAnalyzer.AnalyzeAsync(lead, comps, ct);

            logger.LogInformation(
                "[CMA-WORKER-030] Analysis complete for lead {LeadId}. Value range: {Low:C0}-{High:C0}. Trend: {Trend}. Duration: {DurationMs}ms. CorrelationId: {CorrelationId}",
                lead.Id, analysis.ValueLow, analysis.ValueHigh, analysis.MarketTrend,
                Stopwatch.GetElapsedTime(sw).TotalMilliseconds, correlationId);

            return analysis;
        }
        finally
        {
            CmaDiagnostics.AnalysisDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    private async Task<string> GeneratePdfAsync(
        Lead lead, CmaAnalysis analysis, List<Comp> comps,
        AccountConfig accountConfig, ReportType reportType,
        string correlationId, CancellationToken ct)
    {
        var sw = Stopwatch.GetTimestamp();

        try
        {
            var pdfPath = await pdfGenerator.GenerateAsync(lead, analysis, comps, accountConfig, reportType, ct);

            logger.LogInformation(
                "[CMA-WORKER-040] PDF generated for lead {LeadId}. ReportType: {ReportType}. Duration: {DurationMs}ms. CorrelationId: {CorrelationId}",
                lead.Id, reportType, Stopwatch.GetElapsedTime(sw).TotalMilliseconds, correlationId);

            return pdfPath;
        }
        finally
        {
            CmaDiagnostics.PdfDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    private async Task NotifySellerAsync(
        string agentId, Lead lead, string pdfPath,
        CmaAnalysis analysis, string correlationId, CancellationToken ct)
    {
        try
        {
            await cmaNotifier.NotifySellerAsync(agentId, lead, pdfPath, analysis, correlationId, ct);

            logger.LogInformation(
                "[CMA-WORKER-050] Seller notification complete for lead {LeadId}. CorrelationId: {CorrelationId}",
                lead.Id, correlationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[CMA-WORKER-051] Seller notification failed for lead {LeadId}. CorrelationId: {CorrelationId}",
                lead.Id, correlationId);
        }
        finally
        {
            // Clean up temp PDF file
            TryDeleteTempFile(pdfPath);
        }
    }

    internal static ReportType DetermineReportType(int compCount, LeadScore score) =>
        (compCount, score.OverallScore) switch
        {
            ( >= 6, >= 70) => ReportType.Comprehensive,
            ( >= 3, _) => ReportType.Standard,
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
            logger.LogWarning(ex, "[CMA-WORKER-060] Failed to delete temp PDF: {FileName}", Path.GetFileName(path));
        }
    }
}
