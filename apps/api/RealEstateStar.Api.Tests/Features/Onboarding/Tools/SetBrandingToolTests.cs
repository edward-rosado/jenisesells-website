using System.Text.Json;
using FluentAssertions;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class SetBrandingToolTests
{
    private static SetBrandingTool CreateTool() => new(new OnboardingStateMachine());

    private static JsonElement ParseJson(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    // ── Name property ──────────────────────────────────────────────────────────

    [Fact]
    public void Name_ReturnsSetBranding()
    {
        var tool = CreateTool();

        tool.Name.Should().Be("set_branding");
    }

    // ── Field mapping ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SetsBrandingFieldsWhenParametersPresent()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;
        var json = ParseJson("""
            {
                "primaryColor": "#003366",
                "accentColor": "#FFD700",
                "logoUrl": "https://example.com/logo.png"
            }
            """);

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.Profile.Should().NotBeNull();
        session.Profile!.PrimaryColor.Should().Be("#003366");
        session.Profile.AccentColor.Should().Be("#FFD700");
        session.Profile.LogoUrl.Should().Be("https://example.com/logo.png");
    }

    [Fact]
    public async Task ExecuteAsync_PreservesExistingBrandingWhenParametersMissing()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;
        session.Profile = new ScrapedProfile
        {
            PrimaryColor = "#111111",
            AccentColor = "#222222",
            LogoUrl = "https://example.com/old-logo.png",
        };
        // Only update primaryColor — accent and logoUrl should be preserved
        var json = ParseJson("""{"primaryColor":"#FFFFFF"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.Profile!.PrimaryColor.Should().Be("#FFFFFF");
        session.Profile.AccentColor.Should().Be("#222222");
        session.Profile.LogoUrl.Should().Be("https://example.com/old-logo.png");
    }

    [Fact]
    public async Task ExecuteAsync_CreatesNewProfileWhenSessionProfileIsNull()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;
        session.Profile = null;
        var json = ParseJson("""{"primaryColor":"#AABBCC"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.Profile.Should().NotBeNull();
        session.Profile!.PrimaryColor.Should().Be("#AABBCC");
    }

    [Fact]
    public async Task ExecuteAsync_PreservesPrimaryColorWhenPrimaryColorParameterMissing()
    {
        // Covers the "primaryColor NOT present" branch — the one field omitted in all other missing-param tests
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;
        session.Profile = new ScrapedProfile { PrimaryColor = "#PRESERVED" };
        // JSON has accentColor and logoUrl but not primaryColor
        var json = ParseJson("""{"accentColor":"#CCCCCC","logoUrl":"https://example.com/logo.png"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.Profile!.PrimaryColor.Should().Be("#PRESERVED");
        session.Profile.AccentColor.Should().Be("#CCCCCC");
        session.Profile.LogoUrl.Should().Be("https://example.com/logo.png");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyParameters_PreservesAllExistingBranding()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;
        session.Profile = new ScrapedProfile
        {
            PrimaryColor = "#AAA",
            AccentColor = "#BBB",
            LogoUrl = "https://example.com/logo.png",
        };
        var json = ParseJson("{}");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.Profile!.PrimaryColor.Should().Be("#AAA");
        session.Profile.AccentColor.Should().Be("#BBB");
        session.Profile.LogoUrl.Should().Be("https://example.com/logo.png");
    }

    // ── State machine transitions ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AdvancesFromCollectBrandingToConnectGoogle()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;
        var json = ParseJson("""{"primaryColor":"#003366","accentColor":"#FFD700"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.CurrentState.Should().Be(OnboardingState.ConnectGoogle);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotAdvanceWhenNotInCollectBrandingState()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        // ScrapeProfile cannot transition to ConnectGoogle, so CanAdvance returns false
        session.CurrentState = OnboardingState.ScrapeProfile;
        var json = ParseJson("""{"primaryColor":"#003366"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.CurrentState.Should().Be(OnboardingState.ScrapeProfile);
    }

    // ── Result message ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ResultIncludesColorsAndState()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;
        var json = ParseJson("""{"primaryColor":"#FF0000","accentColor":"#0000FF"}""");

        var result = await tool.ExecuteAsync(json, session, CancellationToken.None);

        result.Should().Contain("#FF0000");
        result.Should().Contain("#0000FF");
        result.Should().StartWith("SUCCESS:");
    }
}
