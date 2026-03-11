namespace RealEstateStar.Api.Features.Onboarding;

public sealed record ScrapedProfile
{
    // Identity
    public string? Name { get; init; }
    public string? Title { get; init; }
    public string? Tagline { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? PhotoUrl { get; init; }

    // Brokerage & licensing
    public string? Brokerage { get; init; }
    public string? BrokerageLogoUrl { get; init; }
    public string? LicenseId { get; init; }
    public string? State { get; init; }
    public string? OfficeAddress { get; init; }

    // Service details
    public string[]? ServiceAreas { get; init; }
    public string[]? Specialties { get; init; }
    public string[]? Designations { get; init; }
    public string[]? Languages { get; init; }
    public string? Bio { get; init; }

    // Stats
    public int? YearsExperience { get; init; }
    public int? HomesSold { get; init; }
    public double? AvgRating { get; init; }
    public int? ReviewCount { get; init; }
    public double? AvgListPrice { get; init; }

    // Branding (inferred from page or manually set)
    public string? PrimaryColor { get; init; }
    public string? AccentColor { get; init; }
    public string? LogoUrl { get; init; }

    // Social & web presence
    public string? WebsiteUrl { get; init; }
    public string? FacebookUrl { get; init; }
    public string? InstagramUrl { get; init; }
    public string? LinkedInUrl { get; init; }

    // Testimonials (first few from profile page)
    public Testimonial[]? Testimonials { get; init; }

    // Recent sales (for credibility section on website)
    public RecentSale[]? RecentSales { get; init; }
}

public sealed record Testimonial
{
    public string? ReviewerName { get; init; }
    public string? Text { get; init; }
    public double? Rating { get; init; }
    public string? Date { get; init; }
}

public sealed record RecentSale
{
    public string? Address { get; init; }
    public double? Price { get; init; }
    public string? Date { get; init; }
    public string? PhotoUrl { get; init; }
}
