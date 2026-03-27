namespace RealEstateStar.Domain.Leads.Models;

public record BuyerDetails
{
    public required string City { get; init; }
    public required string State { get; init; }
    public decimal? MinBudget { get; init; }
    public decimal? MaxBudget { get; init; }
    public int? Bedrooms { get; init; }
    public int? Bathrooms { get; init; }
    public string? PreApproved { get; init; }
    public decimal? PreApprovalAmount { get; init; }
    public List<string>? PropertyTypes { get; init; }
    public List<string>? MustHaves { get; init; }
    public string? Notes { get; init; }
}
