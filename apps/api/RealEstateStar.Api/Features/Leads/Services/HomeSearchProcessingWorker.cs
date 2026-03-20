using System.Diagnostics;
using RealEstateStar.Api.Diagnostics;
using RealEstateStar.Api.Health;

namespace RealEstateStar.Api.Features.Leads.Services;

/// <summary>
/// Background service that processes home search requests from the <see cref="HomeSearchProcessingChannel"/>.
/// Fetches and curates listings, notifies the buyer, and stores results in Google Drive.
/// </summary>
public sealed class HomeSearchProcessingWorker(
    HomeSearchProcessingChannel channel,
    IHomeSearchProvider homeSearchProvider,
    IHomeSearchNotifier homeSearchNotifier,
    ILeadStore leadStore,
    BackgroundServiceHealthTracker healthTracker,
    ILogger<HomeSearchProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[HS-WORKER-001] Home search processing worker started.");

        await foreach (var request in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessHomeSearchAsync(request, stoppingToken);
                healthTracker.RecordActivity(nameof(HomeSearchProcessingWorker));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                HomeSearchDiagnostics.SearchFailed.Add(1);
                logger.LogError(ex,
                    "[HS-WORKER-002] Unhandled error processing home search for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
                    request.Lead.Id, request.AgentId, request.CorrelationId);
            }
        }

        logger.LogInformation("[HS-WORKER-003] Home search processing worker stopping.");
    }

    private async Task ProcessHomeSearchAsync(HomeSearchProcessingRequest request, CancellationToken ct)
    {
        var (agentId, lead, correlationId) = request;
        var pipelineStart = Stopwatch.GetTimestamp();

        using var activity = HomeSearchDiagnostics.ActivitySource.StartActivity("home_search.process");
        activity?.SetTag("lead.id", lead.Id.ToString());
        activity?.SetTag("lead.agent_id", agentId);
        activity?.SetTag("correlation.id", correlationId);

        logger.LogInformation(
            "[HS-WORKER-010] Starting home search pipeline for lead {LeadId}, agent {AgentId}. CorrelationId: {CorrelationId}",
            lead.Id, agentId, correlationId);

        // Step 1: Fetch and curate listings
        var listings = await FetchListingsAsync(lead, correlationId, ct);

        if (listings.Count == 0)
        {
            logger.LogWarning(
                "[HS-WORKER-011] No listings found for lead {LeadId}. Skipping notification. CorrelationId: {CorrelationId}",
                lead.Id, correlationId);
            return;
        }

        // Step 2: Update lead store with search ID
        var searchId = $"search-{lead.Id}";
        await leadStore.UpdateHomeSearchIdAsync(agentId, lead.Id, searchId, ct);

        // Step 3: Notify buyer (email + Drive storage)
        await NotifyBuyerAsync(agentId, lead, listings, correlationId, ct);

        HomeSearchDiagnostics.SearchCompleted.Add(1);
        var totalMs = Stopwatch.GetElapsedTime(pipelineStart).TotalMilliseconds;
        HomeSearchDiagnostics.TotalDuration.Record(totalMs);

        logger.LogInformation(
            "[HS-WORKER-012] Home search pipeline complete for lead {LeadId} in {DurationMs}ms. Listings: {ListingCount}. CorrelationId: {CorrelationId}",
            lead.Id, totalMs, listings.Count, correlationId);
    }

    private async Task<List<Listing>> FetchListingsAsync(Lead lead, string correlationId, CancellationToken ct)
    {
        var sw = Stopwatch.GetTimestamp();

        try
        {
            var criteria = new HomeSearchCriteria
            {
                Area = string.IsNullOrWhiteSpace(lead.BuyerDetails!.State)
                    ? lead.BuyerDetails.City
                    : $"{lead.BuyerDetails.City}, {lead.BuyerDetails.State}",
                MinPrice = lead.BuyerDetails.MinBudget,
                MaxPrice = lead.BuyerDetails.MaxBudget,
                MinBeds = lead.BuyerDetails.Bedrooms,
                MinBaths = lead.BuyerDetails.Bathrooms
            };

            var listings = await homeSearchProvider.SearchAsync(criteria, ct);
            HomeSearchDiagnostics.ListingsFound.Record(listings.Count);

            logger.LogInformation(
                "[HS-WORKER-020] Fetched {ListingCount} listings for lead {LeadId}. Duration: {DurationMs}ms. CorrelationId: {CorrelationId}",
                listings.Count, lead.Id, Stopwatch.GetElapsedTime(sw).TotalMilliseconds, correlationId);

            return listings;
        }
        finally
        {
            HomeSearchDiagnostics.FetchDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    private async Task NotifyBuyerAsync(
        string agentId, Lead lead, List<Listing> listings,
        string correlationId, CancellationToken ct)
    {
        try
        {
            await homeSearchNotifier.NotifyBuyerAsync(agentId, lead, listings, correlationId, ct);

            logger.LogInformation(
                "[HS-WORKER-030] Buyer notification complete for lead {LeadId}. CorrelationId: {CorrelationId}",
                lead.Id, correlationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[HS-WORKER-031] Buyer notification failed for lead {LeadId}. CorrelationId: {CorrelationId}",
                lead.Id, correlationId);
        }
    }
}
