namespace RealEstateStar.Domain.Leads.Models;

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
