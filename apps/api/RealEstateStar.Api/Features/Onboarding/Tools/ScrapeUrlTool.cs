using System.Text.Json;
using RealEstateStar.Api.Features.Onboarding.Services;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class ScrapeUrlTool(IProfileScraper scraper) : IOnboardingTool
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
        return $"Successfully scraped profile: {profile.Name ?? "Unknown"} from {url}";
    }
}
