namespace RealEstateStar.Api.Features.Leads;

public record SellerDetails
{
    public required string Address { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string Zip { get; init; }
    public int? Beds { get; init; }
    public int? Baths { get; init; }
    public int? Sqft { get; init; }
    public string? PropertyType { get; init; }
    public string? Condition { get; init; }
    public decimal? AskingPrice { get; init; }
}
