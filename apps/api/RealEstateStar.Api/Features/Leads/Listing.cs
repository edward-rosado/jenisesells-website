namespace RealEstateStar.Api.Features.Leads;

public record Listing(
    string Address,
    string City,
    string State,
    string Zip,
    decimal Price,
    int Beds,
    decimal Baths,
    int? Sqft,
    string? WhyThisFits,
    string? ListingUrl);
