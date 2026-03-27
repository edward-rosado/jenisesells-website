using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.HomeSearch;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Shared.Context;

namespace RealEstateStar.Workers.HomeSearch;

public sealed class HomeSearchProcessingWorker(
    HomeSearchProcessingChannel channel,
    IHomeSearchProvider homeSearchProvider,
    BackgroundServiceHealthTracker healthTracker,
    ILogger<HomeSearchProcessingWorker> logger,
    IConfiguration configuration)
    : PipelineWorker<HomeSearchProcessingRequest, HomeSearchPipelineContext>(
        channel, healthTracker, logger,
        configuration.GetSection("Pipeline:HomeSearch:Retry").Get<PipelineRetryOptions>())
{
    protected override string WorkerName => "HomeSearchWorker";

    protected override HomeSearchPipelineContext CreateContext(HomeSearchProcessingRequest request) => new()
    {
        Request = request.Lead,
        AgentId = request.AgentId,
        CorrelationId = request.CorrelationId,
        AgentConfig = request.AgentConfig,
        Completion = request.Completion,
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
            ctx.Completion.TrySetResult(new HomeSearchWorkerResult(
                ctx.Request.Id.ToString(), true, null, [], null));
            return;
        }

        var summaries = ctx.Listings
            .Select(l => new ListingSummary(
                Address: $"{l.Address}, {l.City}, {l.State} {l.Zip}",
                Price: l.Price,
                Beds: l.Beds,
                Baths: l.Baths,
                Sqft: l.Sqft,
                Status: null,
                Url: l.ListingUrl))
            .ToList();

        ctx.Completion.TrySetResult(new HomeSearchWorkerResult(
            ctx.Request.Id.ToString(), true, null, summaries, null));

        HomeSearchDiagnostics.SearchCompleted.Add(1);
        HomeSearchDiagnostics.TotalDuration.Record(ctx.PipelineDurationMs ?? 0);
    }

    protected override Task OnPermanentFailureAsync(HomeSearchPipelineContext ctx, Exception lastException, CancellationToken ct)
    {
        ctx.Completion.TrySetResult(new HomeSearchWorkerResult(
            ctx.Request.Id.ToString(), false, lastException.Message, null, null));
        return Task.CompletedTask;
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
}
