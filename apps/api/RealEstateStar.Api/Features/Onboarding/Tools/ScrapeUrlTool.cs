using System.Text;
using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class ScrapeUrlTool(IProfileScraperService scraper, OnboardingStateMachine stateMachine) : IOnboardingTool
{
    public string Name => "scrape_url";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        var url = parameters.GetProperty("url").GetString()
            ?? throw new ArgumentException("Missing 'url' parameter");

        var profile = await scraper.ScrapeAsync(url, ct);
        if (profile is null)
            return "Could not extract a profile from that URL. Please try a different link.";

        session.Profile = profile;

        // Auto-advance to GenerateSite — skip confirmation/branding, go straight to site build
        if (stateMachine.CanAdvance(session, OnboardingState.GenerateSite))
            stateMachine.Advance(session, OnboardingState.GenerateSite);

        // Return a rich summary so Claude knows exactly what was extracted
        return BuildResultSummary(profile, url);
    }

    private static string BuildResultSummary(ScrapedProfile p, string url)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"SUCCESS: Scraped profile from {url}. Here is everything extracted:");
        sb.AppendLine();

        if (p.Name is not null) sb.AppendLine($"Name: {p.Name}");
        if (p.Title is not null) sb.AppendLine($"Title: {p.Title}");
        if (p.Tagline is not null) sb.AppendLine($"Tagline: {p.Tagline}");
        if (p.Phone is not null) sb.AppendLine($"Phone: {p.Phone}");
        if (p.Email is not null) sb.AppendLine($"Email: {p.Email}");
        if (p.Brokerage is not null) sb.AppendLine($"Brokerage: {p.Brokerage}");
        if (p.State is not null) sb.AppendLine($"State: {p.State}");
        if (p.OfficeAddress is not null) sb.AppendLine($"Office: {p.OfficeAddress}");
        if (p.LicenseId is not null) sb.AppendLine($"License: {p.LicenseId}");
        if (p.ServiceAreas is not null) sb.AppendLine($"Service Areas: {string.Join(", ", p.ServiceAreas)}");
        if (p.Specialties is not null) sb.AppendLine($"Specialties: {string.Join(", ", p.Specialties)}");
        if (p.Designations is not null) sb.AppendLine($"Designations: {string.Join(", ", p.Designations)}");
        if (p.Languages is not null) sb.AppendLine($"Languages: {string.Join(", ", p.Languages)}");
        if (p.YearsExperience is not null) sb.AppendLine($"Years Experience: {p.YearsExperience}");
        if (p.HomesSold is not null) sb.AppendLine($"Homes Sold: {p.HomesSold}");
        if (p.AvgRating is not null) sb.AppendLine($"Rating: {p.AvgRating}/5 ({p.ReviewCount ?? 0} reviews)");
        if (p.AvgListPrice is not null) sb.AppendLine($"Avg List Price: ${p.AvgListPrice:N0}");
        if (p.PrimaryColor is not null) sb.AppendLine($"Brand Primary Color: {p.PrimaryColor}");
        if (p.AccentColor is not null) sb.AppendLine($"Brand Accent Color: {p.AccentColor}");
        if (p.PhotoUrl is not null) sb.AppendLine($"Photo: {p.PhotoUrl}");
        if (p.WebsiteUrl is not null) sb.AppendLine($"Website: {p.WebsiteUrl}");
        if (p.FacebookUrl is not null) sb.AppendLine($"Facebook: {p.FacebookUrl}");
        if (p.InstagramUrl is not null) sb.AppendLine($"Instagram: {p.InstagramUrl}");
        if (p.LinkedInUrl is not null) sb.AppendLine($"LinkedIn: {p.LinkedInUrl}");
        if (p.Bio is not null) sb.AppendLine($"Bio: {p.Bio}");
        if (p.Testimonials is { Length: > 0 }) sb.AppendLine($"Testimonials: {p.Testimonials.Length} reviews scraped");
        if (p.RecentSales is { Length: > 0 }) sb.AppendLine($"Recent Sales: {p.RecentSales.Length} listings found");

        // Flag what's missing
        var missing = new List<string>();
        if (p.Name is null) missing.Add("name");
        if (p.Phone is null) missing.Add("phone");
        if (p.Email is null) missing.Add("email");
        if (p.Brokerage is null) missing.Add("brokerage");
        if (p.State is null) missing.Add("state");

        if (missing.Count > 0)
            sb.AppendLine($"\nMissing critical fields: {string.Join(", ", missing)}. Ask the agent to provide these.");
        else
            sb.AppendLine("\nAll critical fields present. Profile is ready for confirmation.");

        return sb.ToString();
    }
}
