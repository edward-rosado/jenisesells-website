namespace RealEstateStar.Clients.Scraper;

public class ScraperOptions
{
    public string ApiKey { get; init; } = "";
    public string BaseUrl { get; init; } = "https://api.scraperapi.com";
    public bool RenderJavaScript { get; init; } = true;
    public int TimeoutSeconds { get; init; } = 30;
    public int MonthlyLimitWarningPercent { get; init; } = 70;
    public int CircuitBreakerResetSeconds { get; init; } = 600;
}
