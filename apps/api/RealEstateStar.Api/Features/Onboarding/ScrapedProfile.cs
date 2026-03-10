namespace RealEstateStar.Api.Features.Onboarding;

public sealed record ScrapedProfile
{
    public string? Name { get; init; }
    public string? Title { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? PhotoUrl { get; init; }
    public string? Brokerage { get; init; }
    public string? LicenseId { get; init; }
    public string? State { get; init; }
    public string? OfficeAddress { get; init; }
    public string[]? ServiceAreas { get; init; }
    public string? Bio { get; init; }
    public string? PrimaryColor { get; init; }
    public string? AccentColor { get; init; }
    public string? LogoUrl { get; init; }
    public int? YearsExperience { get; init; }
    public int? HomesSold { get; init; }
    public double? AvgRating { get; init; }
}
