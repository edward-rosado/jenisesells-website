using System.Text.Json;
using FluentAssertions;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.Onboarding;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class UpdateProfileToolTests
{
    private static UpdateProfileTool CreateTool() => new(new OnboardingStateMachine());

    private static JsonElement ParseJson(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    // ── Name property ──────────────────────────────────────────────────────────

    [Fact]
    public void Name_ReturnsUpdateProfile()
    {
        var tool = CreateTool();

        tool.Name.Should().Be("update_profile");
    }

    // ── Field mapping ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_UpdatesAllFieldsWhenParametersPresent()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        var json = ParseJson("""
            {
                "name": "Alice Smith",
                "title": "REALTOR",
                "phone": "555-0100",
                "email": "alice@example.com",
                "brokerage": "Keller Williams",
                "state": "NJ",
                "officeAddress": "123 Main St",
                "tagline": "Your home is my mission"
            }
            """);

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.Profile.Should().NotBeNull();
        session.Profile!.Name.Should().Be("Alice Smith");
        session.Profile.Title.Should().Be("REALTOR");
        session.Profile.Phone.Should().Be("555-0100");
        session.Profile.Email.Should().Be("alice@example.com");
        session.Profile.Brokerage.Should().Be("Keller Williams");
        session.Profile.State.Should().Be("NJ");
        session.Profile.OfficeAddress.Should().Be("123 Main St");
        session.Profile.Tagline.Should().Be("Your home is my mission");
    }

    [Fact]
    public async Task ExecuteAsync_PreservesExistingFieldsWhenParametersMissing()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile
        {
            Name = "Original Name",
            Title = "Original Title",
            Phone = "555-9999",
            Email = "original@example.com",
            Brokerage = "Original Brokerage",
            State = "NY",
            OfficeAddress = "999 Old St",
            Tagline = "Old tagline",
        };
        // JSON with only 'name' — all other fields should be preserved
        var json = ParseJson("""{"name":"Updated Name"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.Profile!.Name.Should().Be("Updated Name");
        session.Profile.Title.Should().Be("Original Title");
        session.Profile.Phone.Should().Be("555-9999");
        session.Profile.Email.Should().Be("original@example.com");
        session.Profile.Brokerage.Should().Be("Original Brokerage");
        session.Profile.State.Should().Be("NY");
        session.Profile.OfficeAddress.Should().Be("999 Old St");
        session.Profile.Tagline.Should().Be("Old tagline");
    }

    [Fact]
    public async Task ExecuteAsync_CreatesNewProfileWhenSessionProfileIsNull()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.Profile = null;
        var json = ParseJson("""{"name":"Brand New"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.Profile.Should().NotBeNull();
        session.Profile!.Name.Should().Be("Brand New");
    }

    [Fact]
    public async Task ExecuteAsync_PreservesNameWhenNameParameterMissing()
    {
        // Covers the "name NOT present" branch — the one field omitted in all other missing-param tests
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile { Name = "Kept Name" };
        // JSON has every field except name
        var json = ParseJson("""
            {
                "title": "Broker",
                "phone": "555-1111",
                "email": "kept@example.com",
                "brokerage": "Century 21",
                "state": "TX",
                "officeAddress": "1 Oak Ave",
                "tagline": "Find your dream home"
            }
            """);

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.Profile!.Name.Should().Be("Kept Name");
        session.Profile.Title.Should().Be("Broker");
    }

    // ── State machine transitions ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AdvancesFromScrapeProfileToGenerateSite()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ScrapeProfile;
        var json = ParseJson("""{"name":"Jane Doe"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.CurrentState.Should().Be(OnboardingState.GenerateSite);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotAdvanceWhenInOtherState()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.GenerateSite;
        var json = ParseJson("""{"name":"Jane Doe"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        // GenerateSite is not ScrapeProfile — no transition fires
        session.CurrentState.Should().Be(OnboardingState.GenerateSite);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyParameters_PreservesAllExistingFields()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile
        {
            Name = "Keep",
            Title = "Title",
            Phone = "Phone",
            Email = "Email",
            Brokerage = "Brokerage",
            State = "ST",
            OfficeAddress = "Office",
            Tagline = "Tagline",
        };
        var json = ParseJson("{}");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        session.Profile!.Name.Should().Be("Keep");
        session.Profile.Title.Should().Be("Title");
        session.Profile.Phone.Should().Be("Phone");
        session.Profile.Email.Should().Be("Email");
        session.Profile.Brokerage.Should().Be("Brokerage");
        session.Profile.State.Should().Be("ST");
        session.Profile.OfficeAddress.Should().Be("Office");
        session.Profile.Tagline.Should().Be("Tagline");
    }

    // ── Result message ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ResultIncludesProfileNameAndState()
    {
        var tool = CreateTool();
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ScrapeProfile;
        var json = ParseJson("""{"name":"Bob Builder"}""");

        var result = await tool.ExecuteAsync(json, session, CancellationToken.None);

        result.Should().Contain("Bob Builder");
        result.Should().StartWith("SUCCESS:");
    }
}
