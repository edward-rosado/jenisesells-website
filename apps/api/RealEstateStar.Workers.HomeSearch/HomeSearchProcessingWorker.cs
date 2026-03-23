using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.HomeSearch;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Shared.Context;

namespace RealEstateStar.Workers.HomeSearch;

public sealed class HomeSearchProcessingWorker(
    HomeSearchProcessingChannel channel,
    IHomeSearchProvider homeSearchProvider,
    IHomeSearchNotifier homeSearchNotifier,
    ILeadStore leadStore,
    BackgroundServiceHealthTracker healthTracker,
    ILogger<HomeSearchProcessingWorker> logger)
    : PipelineWorker<HomeSearchProcessingRequest, HomeSearchPipelineContext>(channel, healthTracker, logger)
{
    protected override string WorkerName => "HomeSearchWorker";

    protected override HomeSearchPipelineContext CreateContext(HomeSearchProcessingRequest request) => new()
    {
        Request = request.Lead,
        AgentId = request.AgentId,
        CorrelationId = request.CorrelationId,
    };

    protected override async Task ProcessAsync(HomeSearchPipelineContext ctx, CancellationToken ct)
    {
        using var activity = HomeSearchDiagnostics.ActivitySource.StartActivity("home_search.process");
        activity?.SetTag("lead.id", ctx.Request.Id.ToString());
        activity?.SetTag("lead.agent_id", ctx.AgentId);
        activity?.SetTag("correlation.id", ctx.CorrelationId);

        await RunStepAsync(ctx, HomeSearchPipelineContext.StepFetchListings, () => FetchListingsAsync(ctx, ct), ct);

        if (ctx.Listings is null || ctx.Listings.Count == 0)
        {
            logger.LogWarning("[HomeSearchWorker] No listings found for lead {LeadId}. CorrelationId: {CorrelationId}",
                ctx.Request.Id, ctx.CorrelationId);
            return;
        }

        var searchId = $"search-{ctx.Request.Id}";
        await leadStore.UpdateHomeSearchIdAsync(ctx.AgentId, ctx.Request.Id, searchId, ct);

        await RunStepAsync(ctx, HomeSearchPipelineContext.StepNotifyBuyer, () => NotifyBuyerAsync(ctx, ct), ct);

        HomeSearchDiagnostics.SearchCompleted.Add(1);
        HomeSearchDiagnostics.TotalDuration.Record(ctx.PipelineDurationMs ?? 0);
    }

    private async Task FetchListingsAsync(HomeSearchPipelineContext ctx, CancellationToken ct)
    {
        var criteria = new HomeSearchCriteria
        {
            Area = string.IsNullOrWhiteSpace(ctx.Request.BuyerDetails!.State)
                ? ctx.Request.BuyerDetails.City
                : $"{ctx.Request.BuyerDetails.City}, {ctx.Request.BuyerDetails.State}",
            MinPrice = ctx.Request.BuyerDetails.MinBudget,
            MaxPrice = ctx.Request.BuyerDetails.MaxBudget,
            MinBeds = ctx.Request.BuyerDetails.Bedrooms,
            MinBaths = ctx.Request.BuyerDetails.Bathrooms
        };

        ctx.Listings = await homeSearchProvider.SearchAsync(criteria, ct);
        HomeSearchDiagnostics.ListingsFound.Record(ctx.Listings.Count);
    }

    private async Task NotifyBuyerAsync(HomeSearchPipelineContext ctx, CancellationToken ct)
    {
        await homeSearchNotifier.NotifyBuyerAsync(ctx.AgentId, ctx.Request, ctx.Listings!, ctx.CorrelationId, ct);
    }
}
