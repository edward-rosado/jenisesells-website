using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Shared.Context;

namespace RealEstateStar.Workers.Cma;

public sealed class CmaProcessingWorker(
    CmaProcessingChannel channel,
    ICompAggregator compAggregator,
    ICmaAnalyzer cmaAnalyzer,
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
        ProcessingRequest = request,
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
            ctx.ProcessingRequest.Completion.TrySetResult(new CmaWorkerResult(
                ctx.Request.Id.ToString(), false, "No comparable sales found",
                null, null, null, null, null));
            return;
        }

        await RunStepAsync(ctx, CmaPipelineContext.StepAnalyze, () => AnalyzeAsync(ctx, ct), ct);

        var result = new CmaWorkerResult(
            ctx.Request.Id.ToString(), true, null,
            ctx.Analysis!.ValueMid, ctx.Analysis.ValueLow, ctx.Analysis.ValueHigh,
            ctx.Comps.Select(c => new CompSummary(
                c.Address, c.SalePrice, c.Beds, c.Baths, c.Sqft, c.DaysOnMarket, c.DistanceMiles)).ToList(),
            ctx.Analysis.MarketNarrative);
        ctx.ProcessingRequest.Completion.TrySetResult(result);

        CmaDiagnostics.CmaGenerated.Add(1);
        CmaDiagnostics.TotalDuration.Record(ctx.PipelineDurationMs ?? 0);
    }

    protected override Task OnPermanentFailureAsync(CmaPipelineContext ctx, Exception lastException, CancellationToken ct)
    {
        ctx.ProcessingRequest.Completion.TrySetResult(new CmaWorkerResult(
            ctx.Request.Id.ToString(), false, lastException.Message,
            null, null, null, null, null));
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

    internal static ReportType DetermineReportType(int compCount) =>
        compCount switch
        {
            >= 6 => ReportType.Comprehensive,
            >= 3 => ReportType.Standard,
            _ => ReportType.Lean
        };
}
