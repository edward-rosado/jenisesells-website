using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Api.Features.Onboarding.Tools;
using System.Text.Json;
using RealEstateStar.Api.Features.Onboarding;

namespace RealEstateStar.Api.Tests.Features.Onboarding;

public class OnboardingMappersTests
{
    private static ScrapedProfile FullProfile() => new()
    {
        Name = "Jane Doe",
        Title = "Broker Associate",
        Tagline = "Making Dreams Happen",
        Phone = "555-1234",
        Email = "jane@remax.com",
        PhotoUrl = "https://example.com/photo.jpg",
        Brokerage = "RE/MAX",
        BrokerageLogoUrl = "https://example.com/remax.png",
        LicenseId = "NJ-12345",
        State = "NJ",
        OfficeAddress = "100 Broad St, Newark NJ 07102",
        ServiceAreas = ["Newark", "Jersey City"],
        Specialties = ["Luxury Homes", "First-Time Buyers"],
        Designations = ["CRS", "ABR"],
        Languages = ["English", "Spanish"],
        Bio = "Jane is a top-performing agent with 15 years of experience.",
        YearsExperience = 15,
        HomesSold = 200,
        AvgRating = 4.9,
        ReviewCount = 50,
        AvgListPrice = 450000,
        PrimaryColor = "#1e40af",
        AccentColor = "#10b981",
        LogoUrl = "https://example.com/logo.png",
        WebsiteUrl = "https://janedoe.com",
        FacebookUrl = "https://facebook.com/janedoe",
        InstagramUrl = "https://instagram.com/janedoe",
        LinkedInUrl = "https://linkedin.com/in/janedoe",
        Testimonials =
        [
            new Testimonial { ReviewerName = "John Smith", Text = "Jane was amazing!", Rating = 5.0, Date = "2025-01" },
            new Testimonial { ReviewerName = "Sarah Lee", Text = "Best agent ever!", Rating = 4.5, Date = "2025-02" },
        ],
        RecentSales =
        [
            new RecentSale { Address = "123 Main St, East Brunswick, NJ 07102", Price = 500000, Date = "2025-01", PhotoUrl = "https://example.com/sale1.jpg" },
            new RecentSale { Address = "456 Oak Ave, Newark, NJ 07101", Price = 350000, Date = "2025-02" },
        ],
    };

    private static ScrapedProfile MinimalProfile() => new()
    {
        Name = "Test Agent",
        Phone = "555-0000",
        Email = "test@example.com",
        State = "TX",
    };

    // ===== ToAgentConfig tests =====

    [Fact]
    public void ToAgentConfig_SetsIdFromSlug()
    {
        var config = OnboardingMappers.ToAgentConfig("jane-doe", FullProfile());
        Assert.Equal("jane-doe", config.Id);
    }

    [Fact]
    public void ToAgentConfig_MapsIdentityFields()
    {
        var config = OnboardingMappers.ToAgentConfig("jane-doe", FullProfile());

        Assert.Equal("Jane Doe", config.Identity.Name);
        Assert.Equal("Broker Associate", config.Identity.Title);
        Assert.Equal("555-1234", config.Identity.Phone);
        Assert.Equal("jane@remax.com", config.Identity.Email);
        Assert.Equal("RE/MAX", config.Identity.Brokerage);
        Assert.Equal("NJ-12345", config.Identity.LicenseId);
        Assert.Equal("https://janedoe.com", config.Identity.Website);
        Assert.Equal(["English", "Spanish"], config.Identity.Languages);
        Assert.Equal("Making Dreams Happen", config.Identity.Tagline);
    }

    [Fact]
    public void ToAgentConfig_MapsLocationFields()
    {
        var config = OnboardingMappers.ToAgentConfig("jane-doe", FullProfile());

        Assert.Equal("NJ", config.Location.State);
        Assert.Equal("100 Broad St, Newark NJ 07102", config.Location.OfficeAddress);
        Assert.Equal(["Newark", "Jersey City"], config.Location.ServiceAreas);
    }

    [Fact]
    public void ToAgentConfig_MapsBrandingFields()
    {
        var config = OnboardingMappers.ToAgentConfig("jane-doe", FullProfile());

        Assert.Equal("#1e40af", config.Branding.PrimaryColor);
        Assert.Equal("#10b981", config.Branding.AccentColor);
        Assert.Equal("https://example.com/logo.png", config.Branding.LogoUrl);
    }

    [Fact]
    public void ToAgentConfig_DefaultsForMinimalProfile()
    {
        var config = OnboardingMappers.ToAgentConfig("test-agent", MinimalProfile());

        Assert.Equal("test-agent", config.Id);
        Assert.Equal("Test Agent", config.Identity.Name);
        Assert.Equal("REALTOR\u00ae", config.Identity.Title);
        Assert.Equal(["English"], config.Identity.Languages);
        Assert.Equal("TX", config.Location.State);
        Assert.Empty(config.Location.ServiceAreas);
        Assert.Equal("#1e40af", config.Branding.PrimaryColor);
        Assert.Equal("#10b981", config.Branding.AccentColor);
        Assert.Null(config.Branding.LogoUrl);
    }

    [Fact]
    public void ToAgentConfig_NullName_DefaultsToAgent()
    {
        var profile = new ScrapedProfile();
        var config = OnboardingMappers.ToAgentConfig("agent", profile);

        Assert.Equal("Agent", config.Identity.Name);
    }

    [Fact]
    public void ToAgentConfig_NullState_DefaultsToXX()
    {
        var profile = new ScrapedProfile { Name = "Test" };
        var config = OnboardingMappers.ToAgentConfig("test", profile);

        Assert.Equal("XX", config.Location.State);
    }

    [Fact]
    public void ToAgentConfig_SerializesToSnakeCase()
    {
        var config = OnboardingMappers.ToAgentConfig("jane-doe", FullProfile());
        var json = JsonSerializer.Serialize(config, SiteDeployService.JsonOptions);

        // Verify snake_case keys
        Assert.Contains("\"license_id\"", json);
        Assert.Contains("\"office_address\"", json);
        Assert.Contains("\"service_areas\"", json);
        Assert.Contains("\"primary_color\"", json);
        Assert.Contains("\"accent_color\"", json);
        Assert.Contains("\"logo_url\"", json);

        // Verify no camelCase keys leaked through
        Assert.DoesNotContain("\"licenseId\"", json);
        Assert.DoesNotContain("\"officeAddress\"", json);
        Assert.DoesNotContain("\"serviceAreas\"", json);
        Assert.DoesNotContain("\"primaryColor\"", json);
        Assert.DoesNotContain("\"accentColor\"", json);
        Assert.DoesNotContain("\"logoUrl\"", json);
    }

    [Fact]
    public void ToAgentConfig_RoundTrip_DeserializesCorrectly()
    {
        var original = OnboardingMappers.ToAgentConfig("jane-doe", FullProfile());
        var json = JsonSerializer.Serialize(original, SiteDeployService.JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AgentConfigDto>(json, SiteDeployService.JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Identity.Name, deserialized.Identity.Name);
        Assert.Equal(original.Location.State, deserialized.Location.State);
        Assert.Equal(original.Branding.PrimaryColor, deserialized.Branding.PrimaryColor);
    }

    [Fact]
    public void ToAgentConfig_NullsOmittedInJson()
    {
        var config = OnboardingMappers.ToAgentConfig("test", MinimalProfile());
        var json = JsonSerializer.Serialize(config, SiteDeployService.JsonOptions);

        // Null fields should be omitted (DefaultIgnoreCondition.WhenWritingNull)
        Assert.DoesNotContain("\"brokerage\"", json);
        Assert.DoesNotContain("\"website\"", json);
        Assert.DoesNotContain("\"tagline\"", json);
        Assert.DoesNotContain("\"logo_url\"", json);
    }

    // ===== ToAgentContent tests =====

    [Fact]
    public void ToAgentContent_SetsTemplate()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());
        Assert.Equal("emerald-classic", content.Template);
    }

    [Fact]
    public void ToAgentContent_HeroSectionEnabled()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());

        Assert.True(content.Sections.Hero.Enabled);
        Assert.Equal("Sell Your Home with Confidence", content.Sections.Hero.Data.Headline);
        Assert.Equal("Making Dreams Happen", content.Sections.Hero.Data.Tagline);
        Assert.Equal("Get Your Free Home Value", content.Sections.Hero.Data.CtaText);
        Assert.Equal("#cma-form", content.Sections.Hero.Data.CtaLink);
    }

    [Fact]
    public void ToAgentContent_HeroUsesDefaultTagline_WhenProfileTaglineNull()
    {
        var content = OnboardingMappers.ToAgentContent("test", MinimalProfile());
        Assert.Equal("Your Trusted Real Estate Professional", content.Sections.Hero.Data.Tagline);
    }

    [Fact]
    public void ToAgentContent_StatsEnabled_WhenDataPresent()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());

        Assert.True(content.Sections.Stats.Enabled);
        Assert.Equal(3, content.Sections.Stats.Data.Items.Length);

        // HomesSold stat
        Assert.Contains(content.Sections.Stats.Data.Items, s => s.Value == "200+" && s.Label == "Homes Sold");
        // Rating stat
        Assert.Contains(content.Sections.Stats.Data.Items, s => s.Value == "4.9" && s.Label == "Rating (50 Reviews)");
        // Years experience stat
        Assert.Contains(content.Sections.Stats.Data.Items, s => s.Value == "15+" && s.Label == "Years of Experience");
    }

    [Fact]
    public void ToAgentContent_StatsDisabled_WhenNoDataPresent()
    {
        var content = OnboardingMappers.ToAgentContent("test", MinimalProfile());

        Assert.False(content.Sections.Stats.Enabled);
        Assert.Empty(content.Sections.Stats.Data.Items);
    }

    [Fact]
    public void ToAgentContent_StatsRating_WithoutReviewCount()
    {
        var profile = MinimalProfile() with { AvgRating = 4.8 };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.True(content.Sections.Stats.Enabled);
        Assert.Contains(content.Sections.Stats.Data.Items, s => s.Label == "Average Rating");
    }

    [Fact]
    public void ToAgentContent_ServicesEnabled()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());

        Assert.True(content.Sections.Services.Enabled);
        Assert.Equal(3, content.Sections.Services.Data.Items.Length);
        Assert.Contains("Jane Doe", content.Sections.Services.Data.Items[0].Description);
    }

    [Fact]
    public void ToAgentContent_HowItWorksEnabled()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());

        Assert.True(content.Sections.HowItWorks.Enabled);
        Assert.Equal(3, content.Sections.HowItWorks.Data.Steps.Length);
        Assert.Equal(1, content.Sections.HowItWorks.Data.Steps[0].Number);
        Assert.Contains("Jane Doe", content.Sections.HowItWorks.Data.Steps[2].Description);
    }

    [Fact]
    public void ToAgentContent_TestimonialsEnabled_WhenPresent()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());

        Assert.True(content.Sections.Testimonials.Enabled);
        Assert.Equal(2, content.Sections.Testimonials.Data.Items.Length);
        Assert.Equal("John Smith", content.Sections.Testimonials.Data.Items[0].Reviewer);
        Assert.Equal("Jane was amazing!", content.Sections.Testimonials.Data.Items[0].Text);
        Assert.Equal(5, content.Sections.Testimonials.Data.Items[0].Rating);
    }

    [Fact]
    public void ToAgentContent_TestimonialsDisabled_WhenNone()
    {
        var content = OnboardingMappers.ToAgentContent("test", MinimalProfile());

        Assert.False(content.Sections.Testimonials.Enabled);
        Assert.Empty(content.Sections.Testimonials.Data.Items);
    }

    [Fact]
    public void ToAgentContent_TestimonialRating_TruncatedToInt()
    {
        var profile = MinimalProfile() with
        {
            Testimonials = [new Testimonial { ReviewerName = "Bob", Text = "Good", Rating = 4.5 }],
        };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.Equal(4, content.Sections.Testimonials.Data.Items[0].Rating);
    }

    [Fact]
    public void ToAgentContent_TestimonialDefaults_NullFields()
    {
        var profile = MinimalProfile() with
        {
            Testimonials = [new Testimonial { Text = null, ReviewerName = null, Rating = null }],
        };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.Equal("Anonymous", content.Sections.Testimonials.Data.Items[0].Reviewer);
        Assert.Equal("", content.Sections.Testimonials.Data.Items[0].Text);
        Assert.Equal(5, content.Sections.Testimonials.Data.Items[0].Rating);
    }

    [Fact]
    public void ToAgentContent_SoldHomesEnabled_WhenPresent()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());

        Assert.True(content.Sections.SoldHomes.Enabled);
        Assert.Equal(2, content.Sections.SoldHomes.Data.Items.Length);
        Assert.Equal("123 Main St, East Brunswick, NJ 07102", content.Sections.SoldHomes.Data.Items[0].Address);
        Assert.Equal("East Brunswick", content.Sections.SoldHomes.Data.Items[0].City);
        Assert.Equal("NJ", content.Sections.SoldHomes.Data.Items[0].State);
        Assert.Equal("$500,000", content.Sections.SoldHomes.Data.Items[0].Price);
    }

    [Fact]
    public void ToAgentContent_SoldHomesDisabled_WhenNone()
    {
        var content = OnboardingMappers.ToAgentContent("test", MinimalProfile());

        Assert.False(content.Sections.SoldHomes.Enabled);
        Assert.Empty(content.Sections.SoldHomes.Data.Items);
    }

    [Fact]
    public void ToAgentContent_SoldHome_NullAddress()
    {
        var profile = MinimalProfile() with
        {
            RecentSales = [new RecentSale { Address = null, Price = 100000 }],
        };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.Equal("Address not available", content.Sections.SoldHomes.Data.Items[0].Address);
        Assert.Equal("", content.Sections.SoldHomes.Data.Items[0].City);
    }

    [Fact]
    public void ToAgentContent_CmaFormEnabled()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());

        Assert.True(content.Sections.CmaForm.Enabled);
        Assert.Equal("What's Your Home Worth?", content.Sections.CmaForm.Data.Title);
    }

    [Fact]
    public void ToAgentContent_AboutUsesScrapedBio_WhenPresent()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());

        Assert.True(content.Sections.About.Enabled);
        Assert.Equal("Jane is a top-performing agent with 15 years of experience.", content.Sections.About.Data.Bio);
    }

    [Fact]
    public void ToAgentContent_AboutGeneratesBio_WhenNoBioPresent()
    {
        var content = OnboardingMappers.ToAgentContent("test", MinimalProfile());

        Assert.True(content.Sections.About.Enabled);
        Assert.Contains("Test Agent", content.Sections.About.Data.Bio);
        Assert.Contains("TX", content.Sections.About.Data.Bio);
    }

    [Fact]
    public void ToAgentContent_AboutBio_UsesServiceAreas_WhenPresent()
    {
        var profile = MinimalProfile() with { ServiceAreas = ["Austin", "Round Rock"], Bio = null };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.Contains("Austin, Round Rock", content.Sections.About.Data.Bio);
    }

    [Fact]
    public void ToAgentContent_AboutBio_IncludesYearsExperience_WhenPresent()
    {
        var profile = MinimalProfile() with { YearsExperience = 10, Bio = null };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.Contains("10+ years", content.Sections.About.Data.Bio);
    }

    [Fact]
    public void ToAgentContent_Credentials_IncludesLicense()
    {
        var profile = MinimalProfile() with { LicenseId = "TX-999", State = "TX" };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.Contains("TX Licensed REALTOR\u00ae", content.Sections.About.Data.Credentials);
    }

    [Fact]
    public void ToAgentContent_Credentials_IncludesDesignations()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());

        Assert.Contains("CRS", content.Sections.About.Data.Credentials);
        Assert.Contains("ABR", content.Sections.About.Data.Credentials);
    }

    [Fact]
    public void ToAgentContent_Credentials_IncludesBilingual()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());

        Assert.Contains("Bilingual: English & Spanish", content.Sections.About.Data.Credentials);
    }

    [Fact]
    public void ToAgentContent_Credentials_EmptyForMinimalProfile()
    {
        var content = OnboardingMappers.ToAgentContent("test", MinimalProfile());

        // TX state + no license => no license credential. No languages > 1, no designations.
        Assert.Empty(content.Sections.About.Data.Credentials);
    }

    [Fact]
    public void ToAgentContent_CityPagesDisabled()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());

        Assert.False(content.Sections.CityPages.Enabled);
        Assert.Empty(content.Sections.CityPages.Data.Cities);
    }

    [Fact]
    public void ToAgentContent_SerializesToSnakeCase()
    {
        var content = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());
        var json = JsonSerializer.Serialize(content, SiteDeployService.JsonOptions);

        // Verify snake_case keys
        Assert.Contains("\"how_it_works\"", json);
        Assert.Contains("\"sold_homes\"", json);
        Assert.Contains("\"cma_form\"", json);
        Assert.Contains("\"city_pages\"", json);
        Assert.Contains("\"cta_text\"", json);
        Assert.Contains("\"cta_link\"", json);
        Assert.Contains("\"sold_date\"", json);
    }

    [Fact]
    public void ToAgentContent_RoundTrip_DeserializesCorrectly()
    {
        var original = OnboardingMappers.ToAgentContent("jane-doe", FullProfile());
        var json = JsonSerializer.Serialize(original, SiteDeployService.JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AgentContentDto>(json, SiteDeployService.JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Template, deserialized.Template);
        Assert.Equal(original.Sections.Hero.Data.Headline, deserialized.Sections.Hero.Data.Headline);
        Assert.Equal(original.Sections.Testimonials.Enabled, deserialized.Sections.Testimonials.Enabled);
        Assert.Equal(original.Sections.About.Data.Bio, deserialized.Sections.About.Data.Bio);
    }

    // ===== Edge case coverage for branch completeness =====

    [Fact]
    public void ToAgentContent_ServiceAreaText_EmptyArray_FallsToState()
    {
        var profile = MinimalProfile() with { ServiceAreas = [], State = "TX", Bio = null };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.Contains("TX", content.Sections.About.Data.Bio);
        Assert.DoesNotContain("your area", content.Sections.About.Data.Bio);
    }

    [Fact]
    public void ToAgentContent_ServiceAreaText_NullStateAndEmptyAreas_FallsToYourArea()
    {
        var profile = new ScrapedProfile { Name = "Test", State = null, ServiceAreas = [], Bio = null };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.Contains("your area", content.Sections.About.Data.Bio);
    }

    [Fact]
    public void ToAgentContent_SoldHome_NullPrice_EmptyString()
    {
        var profile = MinimalProfile() with
        {
            RecentSales = [new RecentSale { Address = "123 Main St, City, TX", Price = null }],
        };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.Equal("", content.Sections.SoldHomes.Data.Items[0].Price);
    }

    [Fact]
    public void ToAgentContent_SoldHome_ZeroPrice_EmptyString()
    {
        var profile = MinimalProfile() with
        {
            RecentSales = [new RecentSale { Address = "123 Main St, City, TX", Price = 0 }],
        };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.Equal("", content.Sections.SoldHomes.Data.Items[0].Price);
    }

    [Fact]
    public void ToAgentContent_SoldHome_NullState_EmptyString()
    {
        var profile = new ScrapedProfile
        {
            Name = "Test",
            State = null,
            RecentSales = [new RecentSale { Address = "123 Main St, City, XX", Price = 100000 }],
        };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.Equal("", content.Sections.SoldHomes.Data.Items[0].State);
    }

    [Fact]
    public void ToAgentContent_SoldHome_NegativePrice_EmptyString()
    {
        var profile = MinimalProfile() with
        {
            RecentSales = [new RecentSale { Address = "123 Main St, City, TX", Price = -1 }],
        };
        var content = OnboardingMappers.ToAgentContent("test", profile);

        Assert.Equal("", content.Sections.SoldHomes.Data.Items[0].Price);
    }

    // ===== ExtractCity tests =====

    [Theory]
    [InlineData("123 Main St, East Brunswick, NJ 07102", "East Brunswick")]
    [InlineData("456 Oak Ave, Newark, NJ 07101", "Newark")]
    [InlineData("789 Elm Dr", "")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void ExtractCity_ParsesCorrectly(string? address, string expectedCity)
    {
        Assert.Equal(expectedCity, OnboardingMappers.ExtractCity(address));
    }
}
