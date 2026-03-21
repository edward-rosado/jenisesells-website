using RealEstateStar.Domain.Onboarding.Models;
namespace RealEstateStar.Domain.Onboarding.Interfaces;

public interface IProfileScraperService
{
    Task<ScrapedProfile?> ScrapeAsync(string url, CancellationToken ct);
}
