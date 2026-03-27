namespace RealEstateStar.Domain.Cma.Models;

public record RentCastValuation
{
    public required decimal Price { get; init; }
    public required decimal PriceRangeLow { get; init; }
    public required decimal PriceRangeHigh { get; init; }
    public required IReadOnlyList<RentCastComp> Comparables { get; init; }
    public RentCastSubjectProperty? SubjectProperty { get; init; }
}

public record RentCastSubjectProperty
{
    public required string FormattedAddress { get; init; }
    public string? PropertyType { get; init; }
    public int? Bedrooms { get; init; }
    public decimal? Bathrooms { get; init; }
    public int? SquareFootage { get; init; }
    public int? YearBuilt { get; init; }
    public int? LotSize { get; init; }
}

public record RentCastComp
{
    public required string FormattedAddress { get; init; }
    public string? PropertyType { get; init; }
    public int? Bedrooms { get; init; }
    public decimal? Bathrooms { get; init; }   // decimal because RentCast returns 2.5
    public int? SquareFootage { get; init; }
    public decimal? Price { get; init; }
    public DateTimeOffset? ListedDate { get; init; }
    public DateTimeOffset? RemovedDate { get; init; }
    public int? DaysOnMarket { get; init; }
    public double? Distance { get; init; }
    public double? Correlation { get; init; }
    public string? Status { get; init; }
}
