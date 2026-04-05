using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Functions.Lead.Models;

namespace RealEstateStar.Functions.Lead.Activities;

/// <summary>
/// Activity function that executes the CMA pipeline (fetch comps + analyze).
/// Replaces <c>CmaProcessingWorker</c> (Channel-based BackgroundService).
/// The Channel&lt;T&gt; backpressure is no longer needed — Azure Queue + Functions
/// runtime auto-scales by queue depth.
/// </summary>
/// <remarks>
/// TODO(phase-4): Mark <c>CmaProcessingWorker</c> for removal after this is live.
/// </remarks>
public sealed class CmaProcessingFunction(
    ILeadStore leadStore,
    ICompAggregator compAggregator,
    ICmaAnalyzer cmaAnalyzer,
    ILogger<CmaProcessingFunction> logger)
{
    [Function("CmaProcessing")]
    public async Task<string> RunAsync(
        [ActivityTrigger] CmaFunctionInput input,
        CancellationToken ct)
    {
        var leadId = Guid.Parse(input.LeadId);
        var lead = await leadStore.GetAsync(input.AgentId, leadId, ct)
            ?? throw new InvalidOperationException(
                $"[CMA-F-001] Lead {input.LeadId} not found. CorrelationId={input.CorrelationId}");

        var seller = lead.SellerDetails
            ?? throw new InvalidOperationException(
                $"[CMA-F-002] Seller details missing for lead {input.LeadId}. CorrelationId={input.CorrelationId}");

        var searchRequest = new CompSearchRequest
        {
            Address = seller.Address,
            City = seller.City,
            State = seller.State,
            Zip = seller.Zip,
            Beds = seller.Beds,
            Baths = seller.Baths,
            SqFt = seller.Sqft,
            PropertyType = seller.PropertyType
        };

        List<Comp> comps;
        try
        {
            comps = await compAggregator.FetchCompsAsync(searchRequest, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CMA-F-010] FetchComps failed for lead {LeadId}. CorrelationId={CorrelationId}",
                input.LeadId, input.CorrelationId);
            return JsonSerializer.Serialize(new CmaFunctionOutput
            {
                Result = new CmaWorkerResult(input.LeadId, false, ex.Message, null, null, null, null, null)
            });
        }

        if (comps.Count == 0)
        {
            logger.LogWarning("[CMA-F-011] No comps found for lead {LeadId}. CorrelationId={CorrelationId}",
                input.LeadId, input.CorrelationId);
            return JsonSerializer.Serialize(new CmaFunctionOutput
            {
                Result = new CmaWorkerResult(input.LeadId, false, "No comparable sales found", null, null, null, null, null)
            });
        }

        CmaAnalysis analysis;
        try
        {
            analysis = await cmaAnalyzer.AnalyzeAsync(lead, comps, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CMA-F-012] CMA analysis failed for lead {LeadId}. CorrelationId={CorrelationId}",
                input.LeadId, input.CorrelationId);
            return JsonSerializer.Serialize(new CmaFunctionOutput
            {
                Result = new CmaWorkerResult(input.LeadId, false, ex.Message, null, null, null, null, null)
            });
        }

        var compSummaries = comps.Select(c => new CompSummary(
            c.Address, c.SalePrice, c.Beds, c.Baths, c.Sqft, c.DaysOnMarket, c.DistanceMiles, c.SaleDate)).ToList();

        logger.LogInformation("[CMA-F-020] CMA completed for lead {LeadId}. Value={Value}, Comps={Count}. CorrelationId={CorrelationId}",
            input.LeadId, analysis.ValueMid, comps.Count, input.CorrelationId);

        return JsonSerializer.Serialize(new CmaFunctionOutput
        {
            Result = new CmaWorkerResult(
                input.LeadId, true, null,
                analysis.ValueMid, analysis.ValueLow, analysis.ValueHigh,
                compSummaries, analysis.MarketNarrative, analysis.PricingStrategy)
        });
    }
}
