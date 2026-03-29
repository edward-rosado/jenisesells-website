namespace RealEstateStar.Domain.Activation.Models;

public sealed record AgentDiscovery(
    byte[]? HeadshotBytes,
    byte[]? LogoBytes,
    string? Phone,
    IReadOnlyList<DiscoveredWebsite> Websites,
    IReadOnlyList<Review> Reviews,
    IReadOnlyList<ThirdPartyProfile> Profiles,
    string? Ga4MeasurementId,
    bool WhatsAppEnabled);

public sealed record DiscoveredWebsite(
    string Url,
    string Source,
    string? Html);

public sealed record Review(
    string Text,
    int Rating,
    string Reviewer,
    string Source,
    DateTime? Date);

public sealed record ThirdPartyProfile(
    string Platform,
    string? Bio,
    IReadOnlyList<Review> Reviews,
    int? SalesCount,
    int? ActiveListingCount,
    int? YearsExperience,
    IReadOnlyList<string> Specialties,
    IReadOnlyList<string> ServiceAreas,
    IReadOnlyList<ListingInfo> RecentSales,
    IReadOnlyList<ListingInfo> ActiveListings);

public sealed record ListingInfo(
    string Address,
    string City,
    string State,
    string Price,
    string? Status,
    int? Beds,
    int? Baths,
    int? Sqft,
    string? ImageUrl,
    DateTime? Date);

public sealed record BrandingKit(
    IReadOnlyList<ColorEntry> Colors,
    IReadOnlyList<FontEntry> Fonts,
    IReadOnlyList<LogoVariant> Logos,
    string? RecommendedTemplate,
    string? TemplateReason);

public sealed record ColorEntry(string Role, string Hex, string Source, string Usage);
public sealed record FontEntry(string Role, string Family, string Weight, string Source);
public sealed record LogoVariant(string Variant, string FileName, byte[] Bytes, string Source);
