namespace RealEstateStar.Domain.Leads.Models;

public record HomeSearchCriteria
{
    public required string Area { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public int? MinBeds { get; init; }
    public int? MinBaths { get; init; }
}
