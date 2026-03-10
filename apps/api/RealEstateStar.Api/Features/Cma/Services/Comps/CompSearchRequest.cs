namespace RealEstateStar.Api.Features.Cma.Services.Comps;

public record CompSearchRequest
{
    public required string Address { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string Zip { get; init; }
    public int? Beds { get; init; }
    public int? Baths { get; init; }
    public int? SqFt { get; init; }
}
