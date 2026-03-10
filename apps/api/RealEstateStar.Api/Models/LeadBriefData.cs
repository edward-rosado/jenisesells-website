namespace RealEstateStar.Api.Models;

public record LeadBriefData
{
    public required string LeadName { get; init; }
    public required string Address { get; init; }
    public required string Timeline { get; init; }
    public required DateTime SubmittedAt { get; init; }
    public string? Occupation { get; init; }
    public string? Employer { get; init; }
    public DateOnly? PurchaseDate { get; init; }
    public decimal? PurchasePrice { get; init; }
    public string? OwnershipDuration { get; init; }
    public string? EquityRange { get; init; }
    public string? LifeEvent { get; init; }
    public int? Beds { get; init; }
    public int? Baths { get; init; }
    public int? Sqft { get; init; }
    public int? YearBuilt { get; init; }
    public string? LotSize { get; init; }
    public decimal? TaxAssessment { get; init; }
    public decimal? AnnualTax { get; init; }
    public required int CompCount { get; init; }
    public required string SearchRadius { get; init; }
    public required string ValueRange { get; init; }
    public required int MedianDom { get; init; }
    public required string MarketTrend { get; init; }
    public required List<string> ConversationStarters { get; init; }
    public required string LeadEmail { get; init; }
    public required string LeadPhone { get; init; }
    public required string PdfLink { get; init; }
}
