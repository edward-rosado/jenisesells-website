using System.Globalization;
using System.Text.Json.Serialization;
using RealEstateStar.Domain.Onboarding.Models;

namespace RealEstateStar.Workers.Onboarding;

/// <summary>
/// Maps a ScrapedProfile to the agent config/content JSON structures expected by agent-site.
/// Extracted from OnboardingMappers in RealEstateStar.Api — HTTP mappers remain in Api.
/// </summary>
public static class SiteConfigMappers
{
    /// <summary>
    /// Maps a ScrapedProfile to the agent config JSON structure expected by agent-site.
    /// Schema: config/agent.schema.json — all keys use snake_case.
    /// </summary>
    public static AgentConfigDto ToAgentConfig(string agentSlug, ScrapedProfile profile) => new()
    {
        Id = agentSlug,
        Identity = new AgentIdentityDto
        {
            Name = profile.Name ?? "Agent",
            Title = profile.Title ?? "REALTOR\u00ae",
            LicenseId = profile.LicenseId,
            Brokerage = profile.Brokerage,
            Phone = profile.Phone ?? "",
            Email = profile.Email ?? "",
            Website = profile.WebsiteUrl,
            Languages = profile.Languages ?? ["English"],
            Tagline = profile.Tagline,
        },
        Location = new AgentLocationDto
        {
            State = profile.State ?? "XX",
            OfficeAddress = profile.OfficeAddress,
            ServiceAreas = profile.ServiceAreas ?? [],
        },
        Branding = new AgentBrandingDto
        {
            PrimaryColor = profile.PrimaryColor ?? "#1e40af",
            AccentColor = profile.AccentColor ?? "#10b981",
            LogoUrl = profile.LogoUrl,
        },
    };

    /// <summary>
    /// Maps a ScrapedProfile to the agent content JSON structure expected by agent-site.
    /// Schema: config/agent-content.schema.json
    /// </summary>
    public static AgentContentDto ToAgentContent(string agentSlug, ScrapedProfile profile)
    {
        var name = profile.Name ?? "Your Agent";
        var tagline = profile.Tagline ?? "Your Trusted Real Estate Professional";
        var serviceAreaText = profile.ServiceAreas is { Length: > 0 }
            ? string.Join(", ", profile.ServiceAreas)
            : profile.State ?? "your area";

        return new AgentContentDto
        {
            Template = "emerald-classic",
            Sections = new AgentSectionsDto
            {
                Hero = new SectionDto<HeroDataDto>
                {
                    Enabled = true,
                    Data = new HeroDataDto
                    {
                        Headline = "Sell Your Home with Confidence",
                        Tagline = tagline,
                        CtaText = "Get Your Free Home Value",
                        CtaLink = "#cma-form",
                    },
                },
                Stats = BuildStatsSection(profile),
                Services = new SectionDto<ItemsWrapper<ServiceItemDto>>
                {
                    Enabled = true,
                    Data = new ItemsWrapper<ServiceItemDto>
                    {
                        Items =
                        [
                            new() { Title = "Expert Market Analysis", Description = $"{name} provides a detailed analysis of your local market to price your home right." },
                            new() { Title = "Strategic Marketing Plan", Description = "Professional photography, virtual tours, and targeted online advertising." },
                            new() { Title = "Negotiation & Closing", Description = "Skilled negotiation to get you the best possible price and smooth closing." },
                        ],
                    },
                },
                HowItWorks = new SectionDto<StepsWrapper>
                {
                    Enabled = true,
                    Data = new StepsWrapper
                    {
                        Steps =
                        [
                            new() { Number = 1, Title = "Submit Your Info", Description = "Fill out the quick form below with your property details." },
                            new() { Number = 2, Title = "Get Your Free Report", Description = "Receive a professional Comparative Market Analysis within minutes." },
                            new() { Number = 3, Title = "Schedule a Walkthrough", Description = $"Meet with {name} to discuss your selling strategy." },
                        ],
                    },
                },
                SoldHomes = BuildSoldHomesSection(profile),
                Testimonials = BuildTestimonialsSection(profile),
                CmaForm = new SectionDto<CmaFormDataDto>
                {
                    Enabled = true,
                    Data = new CmaFormDataDto
                    {
                        Title = "What's Your Home Worth?",
                        Subtitle = "Get a free, professional Comparative Market Analysis",
                    },
                },
                About = new SectionDto<AboutDataDto>
                {
                    Enabled = true,
                    Data = new AboutDataDto
                    {
                        Bio = BuildBio(name, serviceAreaText, profile),
                        Credentials = BuildCredentials(profile),
                    },
                },
                CityPages = new SectionDto<CitiesWrapper>
                {
                    Enabled = false,
                    Data = new CitiesWrapper { Cities = [] },
                },
            },
        };
    }

    private static SectionDto<ItemsWrapper<StatItemDto>> BuildStatsSection(ScrapedProfile profile)
    {
        var items = new List<StatItemDto>();

        if (profile.HomesSold is > 0)
            items.Add(new StatItemDto { Value = $"{profile.HomesSold}+", Label = "Homes Sold" });
        if (profile.AvgRating is > 0)
        {
            var label = profile.ReviewCount is > 0
                ? $"Rating ({profile.ReviewCount} Reviews)"
                : "Average Rating";
            items.Add(new StatItemDto { Value = profile.AvgRating.Value.ToString("F1"), Label = label });
        }
        if (profile.YearsExperience is > 0)
            items.Add(new StatItemDto { Value = $"{profile.YearsExperience}+", Label = "Years of Experience" });

        return new SectionDto<ItemsWrapper<StatItemDto>>
        {
            Enabled = items.Count > 0,
            Data = new ItemsWrapper<StatItemDto> { Items = items.ToArray() },
        };
    }

    private static SectionDto<ItemsWrapper<SoldHomeItemDto>> BuildSoldHomesSection(ScrapedProfile profile)
    {
        if (profile.RecentSales is not { Length: > 0 })
            return new SectionDto<ItemsWrapper<SoldHomeItemDto>>
            {
                Enabled = false,
                Data = new ItemsWrapper<SoldHomeItemDto> { Items = [] },
            };

        var items = profile.RecentSales.Select(s => new SoldHomeItemDto
        {
            Address = s.Address ?? "Address not available",
            City = ExtractCity(s.Address),
            State = profile.State ?? "",
            Price = s.Price is > 0 ? s.Price.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US")) : "",
            SoldDate = s.Date,
        }).ToArray();

        return new SectionDto<ItemsWrapper<SoldHomeItemDto>>
        {
            Enabled = true,
            Data = new ItemsWrapper<SoldHomeItemDto> { Items = items },
        };
    }

    private static SectionDto<ItemsWrapper<TestimonialItemDto>> BuildTestimonialsSection(ScrapedProfile profile)
    {
        if (profile.Testimonials is not { Length: > 0 })
            return new SectionDto<ItemsWrapper<TestimonialItemDto>>
            {
                Enabled = false,
                Data = new ItemsWrapper<TestimonialItemDto> { Items = [] },
            };

        var items = profile.Testimonials.Select(t => new TestimonialItemDto
        {
            Text = t.Text ?? "",
            Reviewer = t.ReviewerName ?? "Anonymous",
            Rating = (int)(t.Rating ?? 5),
        }).ToArray();

        return new SectionDto<ItemsWrapper<TestimonialItemDto>>
        {
            Enabled = true,
            Data = new ItemsWrapper<TestimonialItemDto> { Items = items },
        };
    }

    private static string BuildBio(string name, string serviceAreaText, ScrapedProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Bio))
            return profile.Bio;

        var experience = profile.YearsExperience is > 0
            ? $"With {profile.YearsExperience}+ years of experience, "
            : "";
        return $"{experience}{name} is a dedicated real estate professional serving {serviceAreaText}. Contact {name} today to learn how they can help you achieve your real estate goals.";
    }

    private static string[] BuildCredentials(ScrapedProfile profile)
    {
        var creds = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.LicenseId) && !string.IsNullOrWhiteSpace(profile.State))
            creds.Add($"{profile.State} Licensed REALTOR\u00ae");
        if (profile.Designations is { Length: > 0 })
            creds.AddRange(profile.Designations);
        if (profile.Languages is { Length: > 1 })
            creds.Add($"Bilingual: {string.Join(" & ", profile.Languages)}");
        return creds.ToArray();
    }

    /// <summary>
    /// Extracts a city name from an address string (best-effort).
    /// Falls back to empty string if parsing fails.
    /// </summary>
    internal static string ExtractCity(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "";

        // Try to find the city from a pattern like "123 Main St, East Brunswick, NJ 07102"
        var parts = address.Split(',');
        if (parts.Length >= 2)
            return parts[1].Trim();

        return "";
    }
}

// --- DTOs for agent config JSON (snake_case to match agent.schema.json) ---

public sealed record AgentConfigDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("identity")]
    public required AgentIdentityDto Identity { get; init; }

    [JsonPropertyName("location")]
    public required AgentLocationDto Location { get; init; }

    [JsonPropertyName("branding")]
    public required AgentBrandingDto Branding { get; init; }
}

public sealed record AgentIdentityDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("license_id")]
    public string? LicenseId { get; init; }

    [JsonPropertyName("brokerage")]
    public string? Brokerage { get; init; }

    [JsonPropertyName("phone")]
    public required string Phone { get; init; }

    [JsonPropertyName("email")]
    public required string Email { get; init; }

    [JsonPropertyName("website")]
    public string? Website { get; init; }

    [JsonPropertyName("languages")]
    public string[] Languages { get; init; } = ["English"];

    [JsonPropertyName("tagline")]
    public string? Tagline { get; init; }
}

public sealed record AgentLocationDto
{
    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("office_address")]
    public string? OfficeAddress { get; init; }

    [JsonPropertyName("service_areas")]
    public string[] ServiceAreas { get; init; } = [];
}

public sealed record AgentBrandingDto
{
    [JsonPropertyName("primary_color")]
    public string PrimaryColor { get; init; } = "#1e40af";

    [JsonPropertyName("accent_color")]
    public string AccentColor { get; init; } = "#10b981";

    [JsonPropertyName("logo_url")]
    public string? LogoUrl { get; init; }
}

// --- DTOs for agent content JSON (snake_case to match agent-site types.ts) ---

public sealed record AgentContentDto
{
    [JsonPropertyName("template")]
    public required string Template { get; init; }

    [JsonPropertyName("sections")]
    public required AgentSectionsDto Sections { get; init; }
}

public sealed record AgentSectionsDto
{
    [JsonPropertyName("hero")]
    public required SectionDto<HeroDataDto> Hero { get; init; }

    [JsonPropertyName("stats")]
    public required SectionDto<ItemsWrapper<StatItemDto>> Stats { get; init; }

    [JsonPropertyName("services")]
    public required SectionDto<ItemsWrapper<ServiceItemDto>> Services { get; init; }

    [JsonPropertyName("how_it_works")]
    public required SectionDto<StepsWrapper> HowItWorks { get; init; }

    [JsonPropertyName("sold_homes")]
    public required SectionDto<ItemsWrapper<SoldHomeItemDto>> SoldHomes { get; init; }

    [JsonPropertyName("testimonials")]
    public required SectionDto<ItemsWrapper<TestimonialItemDto>> Testimonials { get; init; }

    [JsonPropertyName("cma_form")]
    public required SectionDto<CmaFormDataDto> CmaForm { get; init; }

    [JsonPropertyName("about")]
    public required SectionDto<AboutDataDto> About { get; init; }

    [JsonPropertyName("city_pages")]
    public required SectionDto<CitiesWrapper> CityPages { get; init; }
}

public sealed record SectionDto<T>
{
    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    [JsonPropertyName("data")]
    public required T Data { get; init; }
}

public sealed record HeroDataDto
{
    [JsonPropertyName("headline")]
    public required string Headline { get; init; }

    [JsonPropertyName("tagline")]
    public required string Tagline { get; init; }

    [JsonPropertyName("cta_text")]
    public required string CtaText { get; init; }

    [JsonPropertyName("cta_link")]
    public required string CtaLink { get; init; }
}

public sealed record StatItemDto
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }
}

public sealed record ServiceItemDto
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }
}

public sealed record StepItemDto
{
    [JsonPropertyName("number")]
    public required int Number { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }
}

public sealed record SoldHomeItemDto
{
    [JsonPropertyName("address")]
    public required string Address { get; init; }

    [JsonPropertyName("city")]
    public required string City { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("price")]
    public required string Price { get; init; }

    [JsonPropertyName("sold_date")]
    public string? SoldDate { get; init; }
}

public sealed record TestimonialItemDto
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("reviewer")]
    public required string Reviewer { get; init; }

    [JsonPropertyName("rating")]
    public required int Rating { get; init; }
}

public sealed record CmaFormDataDto
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("subtitle")]
    public required string Subtitle { get; init; }
}

public sealed record AboutDataDto
{
    [JsonPropertyName("bio")]
    public required string Bio { get; init; }

    [JsonPropertyName("credentials")]
    public string[] Credentials { get; init; } = [];
}

public sealed record CityPageDataDto
{
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    [JsonPropertyName("city")]
    public required string City { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("county")]
    public required string County { get; init; }

    [JsonPropertyName("highlights")]
    public string[] Highlights { get; init; } = [];

    [JsonPropertyName("market_snapshot")]
    public required string MarketSnapshot { get; init; }
}

public sealed record ItemsWrapper<T>
{
    [JsonPropertyName("items")]
    public required T[] Items { get; init; }
}

public sealed record StepsWrapper
{
    [JsonPropertyName("steps")]
    public required StepItemDto[] Steps { get; init; }
}

public sealed record CitiesWrapper
{
    [JsonPropertyName("cities")]
    public required CityPageDataDto[] Cities { get; init; }
}
