using System.Text.Json;
using FluentAssertions;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class ScrapeUrlToolTests
{
    private readonly Mock<IProfileScraper> _scraper = new();
    private readonly ScrapeUrlTool _tool;

    public ScrapeUrlToolTests()
    {
        _tool = new ScrapeUrlTool(_scraper.Object);
    }

    [Fact]
    public void Name_ReturnsScrapeUrl()
    {
        _tool.Name.Should().Be("scrape_url");
    }

    [Fact]
    public async Task ExecuteAsync_MissingUrlProperty_ThrowsKeyNotFoundException()
    {
        var session = OnboardingSession.Create(null);
        var parameters = JsonSerializer.Deserialize<JsonElement>("{}");

        var act = () => _tool.ExecuteAsync(parameters, session, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_NullUrlValue_ThrowsArgumentException()
    {
        var session = OnboardingSession.Create(null);
        var parameters = JsonSerializer.Deserialize<JsonElement>("{\"url\": null}");

        var act = () => _tool.ExecuteAsync(parameters, session, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*url*");
    }

    [Fact]
    public async Task ExecuteAsync_ScraperReturnsNull_ReturnsFriendlyError()
    {
        var session = OnboardingSession.Create(null);
        var parameters = JsonSerializer.Deserialize<JsonElement>("{\"url\": \"https://zillow.com/profile/nobody\"}");

        _scraper
            .Setup(s => s.ScrapeAsync("https://zillow.com/profile/nobody", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScrapedProfile?)null);

        var result = await _tool.ExecuteAsync(parameters, session, CancellationToken.None);

        result.Should().Contain("Could not extract a profile");
        session.Profile.Should().BeNull("profile should not be set when scraping fails");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulScrape_SetsSessionProfileAndReturnsSuccessMessage()
    {
        var session = OnboardingSession.Create(null);
        var parameters = JsonSerializer.Deserialize<JsonElement>("{\"url\": \"https://zillow.com/profile/janedoe\"}");

        var profile = new ScrapedProfile
        {
            Name = "Jane Doe",
            Brokerage = "RE/MAX Elite",
            State = "NJ",
            Phone = "555-1234"
        };

        _scraper
            .Setup(s => s.ScrapeAsync("https://zillow.com/profile/janedoe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _tool.ExecuteAsync(parameters, session, CancellationToken.None);

        result.Should().Contain("Successfully scraped");
        result.Should().Contain("Jane Doe");
        session.Profile.Should().BeSameAs(profile);
    }

    [Fact]
    public async Task ExecuteAsync_ProfileWithNullName_ReportsUnknown()
    {
        var session = OnboardingSession.Create(null);
        var parameters = JsonSerializer.Deserialize<JsonElement>("{\"url\": \"https://realtor.com/agent/anon\"}");

        var profile = new ScrapedProfile { Name = null, Brokerage = "Century 21" };

        _scraper
            .Setup(s => s.ScrapeAsync("https://realtor.com/agent/anon", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _tool.ExecuteAsync(parameters, session, CancellationToken.None);

        result.Should().Contain("Unknown");
        session.Profile.Should().BeSameAs(profile);
    }
}
