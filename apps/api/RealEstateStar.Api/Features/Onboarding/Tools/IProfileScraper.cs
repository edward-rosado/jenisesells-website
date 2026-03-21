namespace RealEstateStar.Api.Features.Onboarding.Tools;

public interface IProfileScraper
{
    Task<ScrapedProfile?> ScrapeAsync(string url, CancellationToken ct);
}
