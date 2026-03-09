using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Research;

public class LeadResearchService(HttpClient httpClient, ILogger<LeadResearchService>? logger = null) : ILeadResearchService
{
    public async Task<LeadResearch> ResearchAsync(Lead lead, CancellationToken ct)
    {
        var publicRecordsTask = FetchPublicRecordsAsync(lead, ct);
        var linkedInTask = FetchLinkedInAsync(lead, ct);
        var neighborhoodTask = FetchNeighborhoodAsync(lead, ct);

        await Task.WhenAll(publicRecordsTask, linkedInTask, neighborhoodTask);

        var publicRecords = publicRecordsTask.Result;
        var linkedIn = linkedInTask.Result;
        var neighborhood = neighborhoodTask.Result;

        var research = new LeadResearch
        {
            Occupation = linkedIn?.Occupation,
            Employer = linkedIn?.Employer,
            LinkedInUrl = linkedIn?.LinkedInUrl,
            PurchaseDate = publicRecords?.PurchaseDate,
            PurchasePrice = publicRecords?.PurchasePrice,
            TaxAssessment = publicRecords?.TaxAssessment,
            AnnualPropertyTax = publicRecords?.AnnualPropertyTax,
            YearBuilt = publicRecords?.YearBuilt,
            LotSize = publicRecords?.LotSize,
            LotSizeUnit = publicRecords?.LotSizeUnit,
            NeighborhoodContext = neighborhood?.NeighborhoodContext,
            LifeEventInsight = linkedIn?.LifeEventInsight
        };

        if (research.PurchasePrice.HasValue && research.TaxAssessment.HasValue)
        {
            var (low, high) = EstimateEquity(research.PurchasePrice.Value, research.TaxAssessment.Value);
            research = new LeadResearch
            {
                Occupation = research.Occupation,
                Employer = research.Employer,
                LinkedInUrl = research.LinkedInUrl,
                PurchaseDate = research.PurchaseDate,
                PurchasePrice = research.PurchasePrice,
                TaxAssessment = research.TaxAssessment,
                AnnualPropertyTax = research.AnnualPropertyTax,
                YearBuilt = research.YearBuilt,
                LotSize = research.LotSize,
                LotSizeUnit = research.LotSizeUnit,
                NeighborhoodContext = research.NeighborhoodContext,
                LifeEventInsight = research.LifeEventInsight,
                EstimatedEquityLow = low,
                EstimatedEquityHigh = high
            };
        }

        return research;
    }

    private async Task<LeadResearch?> FetchPublicRecordsAsync(Lead lead, CancellationToken ct)
    {
        try
        {
            logger?.LogInformation("Fetching public records for {Address}", lead.FullAddress);
            var url = $"https://api.publicrecords.example.com/property?address={Uri.EscapeDataString(lead.FullAddress)}";
            var response = await httpClient.GetStringAsync(url, ct);
            logger?.LogInformation("Public records fetched for {Address}", lead.FullAddress);
            // TODO: Parse actual response when API is integrated
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to fetch public records for {Address}", lead.FullAddress);
            return null;
        }
    }

    private async Task<LeadResearch?> FetchLinkedInAsync(Lead lead, CancellationToken ct)
    {
        try
        {
            logger?.LogInformation("Fetching LinkedIn data for {Name}", lead.FullName);
            var url = $"https://api.linkedin.example.com/profile?name={Uri.EscapeDataString(lead.FullName)}";
            var response = await httpClient.GetStringAsync(url, ct);
            logger?.LogInformation("LinkedIn data fetched for {Name}", lead.FullName);
            // TODO: Parse actual response when API is integrated
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to fetch LinkedIn data for {Name}", lead.FullName);
            return null;
        }
    }

    private async Task<LeadResearch?> FetchNeighborhoodAsync(Lead lead, CancellationToken ct)
    {
        try
        {
            logger?.LogInformation("Fetching neighborhood data for {City}, {State}", lead.City, lead.State);
            var url = $"https://api.neighborhood.example.com/context?city={Uri.EscapeDataString(lead.City)}&state={Uri.EscapeDataString(lead.State)}&zip={Uri.EscapeDataString(lead.Zip)}";
            var response = await httpClient.GetStringAsync(url, ct);
            logger?.LogInformation("Neighborhood data fetched for {City}, {State}", lead.City, lead.State);
            // TODO: Parse actual response when API is integrated
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to fetch neighborhood data for {City}, {State}", lead.City, lead.State);
            return null;
        }
    }

    public static string CalculateOwnershipDuration(DateOnly purchaseDate)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var years = today.Year - purchaseDate.Year;

        if (today.Month < purchaseDate.Month ||
            (today.Month == purchaseDate.Month && today.Day < purchaseDate.Day))
        {
            years--;
        }

        return years == 1 ? "1 year" : $"{years} years";
    }

    public static (decimal low, decimal high) EstimateEquity(decimal purchasePrice, decimal currentValueMid)
    {
        var remainingMortgage = purchasePrice * 0.65m;
        var equity = currentValueMid - remainingMortgage;
        var variance = equity * 0.15m;

        return (equity - variance, equity + variance);
    }
}
