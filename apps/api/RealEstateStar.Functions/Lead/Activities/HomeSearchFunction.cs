using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Functions.Lead.Models;

namespace RealEstateStar.Functions.Lead.Activities;

/// <summary>
/// Activity function that executes the HomeSearch pipeline (fetch listings).
/// Replaces <c>HomeSearchProcessingWorker</c> (Channel-based BackgroundService).
/// The Channel&lt;T&gt; backpressure is no longer needed — Azure Queue + Functions
/// runtime auto-scales by queue depth.
/// </summary>
/// <remarks>
/// TODO(phase-4): Mark <c>HomeSearchProcessingWorker</c> for removal after this is live.
/// </remarks>
public sealed class HomeSearchFunction(
    ILeadStore leadStore,
    IHomeSearchProvider homeSearchProvider,
    ILogger<HomeSearchFunction> logger)
{
    [Function("HomeSearch")]
    public async Task<string> RunAsync(
        [ActivityTrigger] HomeSearchFunctionInput input,
        CancellationToken ct)
    {
        var leadId = Guid.Parse(input.LeadId);
        var lead = await leadStore.GetAsync(input.AgentId, leadId, ct)
            ?? throw new InvalidOperationException(
                $"[HS-F-001] Lead {input.LeadId} not found. CorrelationId={input.CorrelationId}");

        var buyer = lead.BuyerDetails
            ?? throw new InvalidOperationException(
                $"[HS-F-002] Buyer details missing for lead {input.LeadId}. CorrelationId={input.CorrelationId}");

        var criteria = new HomeSearchCriteria
        {
            Area = string.IsNullOrWhiteSpace(buyer.State)
                ? buyer.City
                : $"{buyer.City}, {buyer.State}",
            MinPrice = buyer.MinBudget,
            MaxPrice = buyer.MaxBudget,
            MinBeds = buyer.Bedrooms,
            MinBaths = buyer.Bathrooms
        };

        List<Listing> listings;
        try
        {
            listings = await homeSearchProvider.SearchAsync(criteria, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[HS-F-010] HomeSearch failed for lead {LeadId}. CorrelationId={CorrelationId}",
                input.LeadId, input.CorrelationId);
            return JsonSerializer.Serialize(new HomeSearchFunctionOutput
            {
                Result = new HomeSearchWorkerResult(input.LeadId, false, ex.Message, null, null)
            });
        }

        if (listings.Count == 0)
        {
            logger.LogWarning("[HS-F-011] No listings found for lead {LeadId}. CorrelationId={CorrelationId}",
                input.LeadId, input.CorrelationId);
            return JsonSerializer.Serialize(new HomeSearchFunctionOutput
            {
                Result = new HomeSearchWorkerResult(input.LeadId, true, null, [], null)
            });
        }

        var summaries = listings.Select(l => new ListingSummary(
            Address: $"{l.Address}, {l.City}, {l.State} {l.Zip}",
            Price: l.Price,
            Beds: l.Beds,
            Baths: l.Baths,
            Sqft: l.Sqft,
            Status: null,
            Url: l.ListingUrl)).ToList();

        logger.LogInformation("[HS-F-020] HomeSearch completed for lead {LeadId}. Listings={Count}. CorrelationId={CorrelationId}",
            input.LeadId, listings.Count, input.CorrelationId);

        return JsonSerializer.Serialize(new HomeSearchFunctionOutput
        {
            Result = new HomeSearchWorkerResult(input.LeadId, true, null, summaries, null)
        });
    }
}
