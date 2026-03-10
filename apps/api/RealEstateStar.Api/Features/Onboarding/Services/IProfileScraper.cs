namespace RealEstateStar.Api.Features.Onboarding.Services;

public interface IProfileScraper
{
    Task<ScrapedProfile?> ScrapeAsync(string url, CancellationToken ct);
}
