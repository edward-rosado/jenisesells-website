using Xunit;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Workers.Onboarding;
using RealEstateStar.Workers.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Notifications.WhatsApp;
using System.Text.Json;
using FluentAssertions;
using Moq;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class ScrapeUrlToolTests
{
    private readonly Mock<IProfileScraperService> _scraper = new();
    private readonly OnboardingStateMachine _stateMachine = new();
    private readonly ScrapeUrlTool _tool;

    public ScrapeUrlToolTests()
    {
        _tool = new ScrapeUrlTool(_scraper.Object, _stateMachine);
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

        result.Should().Contain("SUCCESS:");
        result.Should().Contain("Jane Doe");
        result.Should().Contain("RE/MAX Elite");
        result.Should().Contain("555-1234");
        session.Profile.Should().BeSameAs(profile);
        session.CurrentState.Should().Be(OnboardingState.GenerateSite, "scrape should auto-advance to GenerateSite");
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

        result.Should().Contain("Missing critical fields");
        result.Should().Contain("name");
        session.Profile.Should().BeSameAs(profile);
    }

    [Fact]
    public async Task ExecuteAsync_ProfileWithNullBrokerage_ReportsMissingBrokerage()
    {
        var session = OnboardingSession.Create(null);
        var parameters = JsonSerializer.Deserialize<JsonElement>("{\"url\": \"https://realtor.com/agent/nobroker\"}");

        var profile = new ScrapedProfile
        {
            Name = "Agent NoBroker",
            Phone = "555-9999",
            Email = "nobroker@example.com",
            Brokerage = null,  // null brokerage — covers the missing-check branch
            State = "NJ",
        };

        _scraper
            .Setup(s => s.ScrapeAsync("https://realtor.com/agent/nobroker", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _tool.ExecuteAsync(parameters, session, CancellationToken.None);

        result.Should().Contain("Missing critical fields");
        result.Should().Contain("brokerage");
        result.Should().NotContain("Brokerage:", "null brokerage should not appear in the field list");
        session.Profile.Should().BeSameAs(profile);
    }

    // ── Full profile: all fields present → "All critical fields present" ──

    [Fact]
    public async Task ExecuteAsync_FullProfile_ReportsAllFieldsAndAllCriticalPresent()
    {
        var session = OnboardingSession.Create(null);
        var parameters = JsonSerializer.Deserialize<JsonElement>("{\"url\": \"https://zillow.com/profile/full\"}");

        var profile = new ScrapedProfile
        {
            Name = "Jenise Buckalew",
            Title = "REALTOR",
            Tagline = "Your NJ home expert",
            Phone = "201-555-0100",
            Email = "jenise@example.com",
            Brokerage = "Keller Williams",
            State = "NJ",
            OfficeAddress = "123 Main St, Montclair, NJ",
            LicenseId = "NJ-12345",
            ServiceAreas = ["Montclair", "Glen Ridge"],
            Specialties = ["First-Time Buyers", "Luxury Homes"],
            Designations = ["ABR", "CRS"],
            Languages = ["English", "Spanish"],
            YearsExperience = 15,
            HomesSold = 200,
            AvgRating = 4.9,
            ReviewCount = 87,
            AvgListPrice = 650000,
            PrimaryColor = "#1a2b3c",
            AccentColor = "#ffcc00",
            PhotoUrl = "https://example.com/photo.jpg",
            WebsiteUrl = "https://jenise.com",
            FacebookUrl = "https://facebook.com/jenise",
            InstagramUrl = "https://instagram.com/jenise",
            LinkedInUrl = "https://linkedin.com/in/jenise",
            Bio = "Helping NJ families find their dream home since 2009.",
            Testimonials =
            [
                new Testimonial { ReviewerName = "Alice", Text = "Great agent!", Rating = 5.0 },
                new Testimonial { ReviewerName = "Bob", Text = "Highly recommend", Rating = 4.8 }
            ],
            RecentSales =
            [
                new RecentSale { Address = "5 Oak Ave", Price = 700000, Date = "2025-01" },
                new RecentSale { Address = "10 Elm St", Price = 580000, Date = "2025-03" }
            ]
        };

        _scraper
            .Setup(s => s.ScrapeAsync("https://zillow.com/profile/full", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _tool.ExecuteAsync(parameters, session, CancellationToken.None);

        result.Should().Contain("Jenise Buckalew");
        result.Should().Contain("REALTOR");
        result.Should().Contain("Your NJ home expert");
        result.Should().Contain("jenise@example.com");
        result.Should().Contain("123 Main St");
        result.Should().Contain("NJ-12345");
        result.Should().Contain("Montclair");
        result.Should().Contain("First-Time Buyers");
        result.Should().Contain("ABR");
        result.Should().Contain("English");
        result.Should().Contain("15");
        result.Should().Contain("200");
        result.Should().Contain("4.9");
        result.Should().Contain("650,000");
        result.Should().Contain("#1a2b3c");
        result.Should().Contain("#ffcc00");
        result.Should().Contain("https://jenise.com");
        result.Should().Contain("All critical fields present",
            "summary should confirm all critical fields when name/phone/email/brokerage/state are all set");
    }

    // ── Testimonials and recent sales counts appear in summary ──

    [Fact]
    public async Task ExecuteAsync_WithTestimonialsAndSales_IncludesCounts()
    {
        var session = OnboardingSession.Create(null);
        var parameters = JsonSerializer.Deserialize<JsonElement>("{\"url\": \"https://zillow.com/profile/reviews\"}");

        var profile = new ScrapedProfile
        {
            Name = "Agent One",
            Phone = "555-0001",
            Email = "agent@example.com",
            Brokerage = "Exit Realty",
            State = "NY",
            Testimonials =
            [
                new Testimonial { ReviewerName = "C1", Text = "Excellent", Rating = 5.0 },
                new Testimonial { ReviewerName = "C2", Text = "Top notch", Rating = 5.0 },
                new Testimonial { ReviewerName = "C3", Text = "Wonderful", Rating = 4.5 }
            ],
            RecentSales =
            [
                new RecentSale { Address = "1 A St", Price = 400000 },
                new RecentSale { Address = "2 B St", Price = 450000 },
                new RecentSale { Address = "3 C St", Price = 500000 },
                new RecentSale { Address = "4 D St", Price = 520000 }
            ]
        };

        _scraper
            .Setup(s => s.ScrapeAsync("https://zillow.com/profile/reviews", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _tool.ExecuteAsync(parameters, session, CancellationToken.None);

        result.Should().Contain("3 reviews scraped", "testimonial count should appear");
        result.Should().Contain("4 listings found", "recent sales count should appear");
    }

    // ── Social URLs appear in summary ──

    [Fact]
    public async Task ExecuteAsync_WithSocialUrls_IncludesThemInSummary()
    {
        var session = OnboardingSession.Create(null);
        var parameters = JsonSerializer.Deserialize<JsonElement>("{\"url\": \"https://realtor.com/agent/social\"}");

        var profile = new ScrapedProfile
        {
            Name = "Social Agent",
            Phone = "555-0002",
            Email = "social@example.com",
            Brokerage = "Social Realty",
            State = "CA",
            FacebookUrl = "https://facebook.com/socialagent",
            InstagramUrl = "https://instagram.com/socialagent",
            LinkedInUrl = "https://linkedin.com/in/socialagent",
            PhotoUrl = "https://example.com/social-photo.jpg",
            WebsiteUrl = "https://socialagent.com"
        };

        _scraper
            .Setup(s => s.ScrapeAsync("https://realtor.com/agent/social", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _tool.ExecuteAsync(parameters, session, CancellationToken.None);

        result.Should().Contain("https://facebook.com/socialagent", "Facebook URL should appear in summary");
        result.Should().Contain("https://instagram.com/socialagent", "Instagram URL should appear in summary");
        result.Should().Contain("https://linkedin.com/in/socialagent", "LinkedIn URL should appear in summary");
        result.Should().Contain("https://example.com/social-photo.jpg", "Photo URL should appear in summary");
        result.Should().Contain("https://socialagent.com", "Website URL should appear in summary");
    }
}
