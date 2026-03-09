namespace RealEstateStar.Api.Models;

public class LeadResearch
{
    public string? Occupation { get; init; }
    public string? Employer { get; init; }
    public string? LinkedInUrl { get; init; }
    public DateOnly? PurchaseDate { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? TaxAssessment { get; init; }
    public decimal? AnnualPropertyTax { get; init; }
    public decimal? EstimatedEquityLow { get; init; }
    public decimal? EstimatedEquityHigh { get; init; }
    public string? LifeEventInsight { get; init; }
    public string? NeighborhoodContext { get; init; }
    public int? YearBuilt { get; init; }
    public decimal? LotSize { get; init; }
    public string? LotSizeUnit { get; init; }
}
