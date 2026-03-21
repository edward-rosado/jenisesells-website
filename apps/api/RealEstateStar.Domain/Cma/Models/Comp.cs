using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Cma.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompSource
{
    Zillow,
    RealtorCom,
    Redfin
}

public class Comp
{
    public required string Address { get; init; }
    public required decimal SalePrice { get; init; }
    public required DateOnly SaleDate { get; init; }
    public required int Beds { get; init; }
    public required int Baths { get; init; }
    public required int Sqft { get; init; }
    public int? DaysOnMarket { get; init; }
    public required double DistanceMiles { get; init; }
    public required CompSource Source { get; init; }

    public decimal PricePerSqft => Sqft > 0 ? SalePrice / Sqft : 0;
}
