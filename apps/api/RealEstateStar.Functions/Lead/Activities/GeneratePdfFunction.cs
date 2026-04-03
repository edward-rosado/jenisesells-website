using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Activities.Pdf;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Functions.Lead.Models;

namespace RealEstateStar.Functions.Lead.Activities;

/// <summary>
/// Generates a CMA PDF for the given lead and CMA result, then stores it via <see cref="PdfActivity"/>.
/// Wraps the existing <see cref="PdfActivity"/> — no PDF logic lives here.
/// </summary>
public sealed class GeneratePdfFunction(
    ILeadStore leadStore,
    IAccountConfigService accountConfigService,
    PdfActivity pdfActivity,
    ILogger<GeneratePdfFunction> logger)
{
    [Function("GeneratePdf")]
    public async Task<GeneratePdfOutput> RunAsync(
        [ActivityTrigger] GeneratePdfInput input,
        CancellationToken ct)
    {
        var leadId = Guid.Parse(input.LeadId);
        var lead = await leadStore.GetAsync(input.AgentId, leadId, ct)
            ?? throw new InvalidOperationException(
                $"[PDF-F-001] Lead {input.LeadId} not found. CorrelationId={input.CorrelationId}");

        var accountConfig = await accountConfigService.GetAccountAsync(input.AgentId, ct);

        var cmaResult = input.CmaResult;

        // Reconstruct CmaAnalysis from the flat fields stored in CmaWorkerResult
        var analysis = new CmaAnalysis
        {
            ValueLow = cmaResult.PriceRangeLow ?? cmaResult.EstimatedValue ?? 0m,
            ValueMid = cmaResult.EstimatedValue ?? 0m,
            ValueHigh = cmaResult.PriceRangeHigh ?? cmaResult.EstimatedValue ?? 0m,
            MarketNarrative = cmaResult.MarketAnalysis ?? string.Empty,
            PricingStrategy = cmaResult.PricingStrategy,
            MarketTrend = "Stable",
            MedianDaysOnMarket = 0
        };

        // Reconstruct comp objects from summaries (PdfActivity needs Comp list for table rendering)
        var comps = (cmaResult.Comps ?? []).Select(c => new Comp
        {
            Address = c.Address,
            SalePrice = c.Price,
            SaleDate = c.SaleDate ?? DateOnly.MinValue, // MinValue = unknown; PDF generator renders "N/A"
            Beds = c.Beds ?? 0,
            Baths = (int)Math.Round(c.Baths ?? 0m, MidpointRounding.AwayFromZero),
            Sqft = c.Sqft ?? 0,
            DaysOnMarket = c.DaysOnMarket,
            DistanceMiles = c.Distance ?? 0,
            Source = CompSource.RentCast
        }).ToList();

        var reportType = comps.Count >= 3 ? ReportType.Standard : ReportType.Lean;

        logger.LogInformation("[PDF-F-010] Generating PDF for lead {LeadId}. Comps={Count}, ReportType={Type}. CorrelationId={CorrelationId}",
            input.LeadId, comps.Count, reportType, input.CorrelationId);

        var storagePath = await pdfActivity.ExecuteAsync(
            lead, analysis, comps, accountConfig, reportType,
            logoBytes: null, headshotBytes: null,
            correlationId: input.CorrelationId, ct: ct,
            locale: input.Locale);

        logger.LogInformation("[PDF-F-020] PDF stored at {Path}. LeadId={LeadId}. CorrelationId={CorrelationId}",
            storagePath, input.LeadId, input.CorrelationId);

        return new GeneratePdfOutput { PdfStoragePath = storagePath };
    }
}
