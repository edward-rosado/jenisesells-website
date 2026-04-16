using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Clients.Anthropic;
using RealEstateStar.Domain.Activation.FieldSpecs;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Functions.Activation.Activities;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Functions.Activation.Helpers;

namespace RealEstateStar.Functions.Tests.Activation;

/// <summary>
/// Tests for <see cref="BuildLocalizedSiteContentFunction"/> and <see cref="TemplateSelector"/>.
///
/// VoicedContentGenerator is sealed (not mockable via Moq).
/// Tests construct a real VoicedContentGenerator with:
///   - Mocked IAnthropicClient → returns controlled strings
///   - Mocked IDistributedContentCache → always returns cache miss (null) for Gets
/// This tests the full delegation path without actual Claude API calls.
/// </summary>
public sealed class BuildLocalizedSiteContentFunctionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a VoicedContentGenerator with a claude mock that returns <paramref name="responseContent"/>
    /// for every call, and a cache that always misses.
    /// </summary>
    private static (BuildLocalizedSiteContentFunction Fn, Mock<IAnthropicClient> ClaudeMock)
        BuildFn(string responseContent = "Generated content")
    {
        var claudeMock = new Mock<IAnthropicClient>(MockBehavior.Loose);
        claudeMock
            .Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse(responseContent, 100, 50, 200.0));

        var cacheMock = new Mock<IDistributedContentCache>(MockBehavior.Loose);
        cacheMock
            .Setup(c => c.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null); // always miss

        var generator = new VoicedContentGenerator(
            claudeMock.Object,
            cacheMock.Object,
            NullLogger<VoicedContentGenerator>.Instance);

        var fn = new BuildLocalizedSiteContentFunction(
            generator,
            NullLogger<BuildLocalizedSiteContentFunction>.Instance);

        return (fn, claudeMock);
    }

    /// <summary>Builds a VoicedContentGenerator whose Claude client always throws.</summary>
    private static BuildLocalizedSiteContentFunction BuildFnWithThrowingClaude()
    {
        var claudeMock = new Mock<IAnthropicClient>(MockBehavior.Loose);
        claudeMock
            .Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Claude API unreachable"));

        var cacheMock = new Mock<IDistributedContentCache>(MockBehavior.Loose);
        cacheMock
            .Setup(c => c.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var generator = new VoicedContentGenerator(
            claudeMock.Object,
            cacheMock.Object,
            NullLogger<VoicedContentGenerator>.Instance);

        return new BuildLocalizedSiteContentFunction(
            generator,
            NullLogger<BuildLocalizedSiteContentFunction>.Instance);
    }

    private static SiteFacts BuildFacts(string[]? vibeHints = null, string[]? specialties = null) =>
        new(
            Agent: new AgentIdentity(
                Name: "Jane Smith",
                LegalName: "Jane Smith",
                Title: "REALTOR",
                Email: "jane@example.com",
                Phone: "555-1234",
                LicenseNumber: "NJ-123456",
                YearsExperience: 10,
                Languages: ["en"]),
            Brokerage: new BrokerageIdentity(
                Name: "Smith Realty",
                LegalName: null, LicenseNumber: null, OfficeAddress: null,
                OfficePhone: null, DomainHint: null),
            Location: new LocationFacts(
                State: "NJ",
                ServiceAreas: ["Hoboken", "Jersey City"],
                ListingFrequencyByCity: new Dictionary<string, int> { ["Hoboken"] = 15 }),
            Specialties: new SpecialtiesFacts(
                Specialties: specialties ?? ["Residential", "First-Time Buyers"],
                VibeHints: vibeHints ?? ["friendly", "local"],
                EvidenceCount: new Dictionary<string, int>()),
            Trust: new TrustSignals(
                ReviewCount: 42, AverageRating: 4.9m,
                TransactionCount: 85, AverageResponseTime: TimeSpan.FromHours(2),
                AverageSalePrice: 450_000m),
            RecentSales: [],
            Testimonials: [],
            Credentials: [],
            Stages: new PipelineStages(
                StageNames: ["Inquiry", "Showing", "Offer", "Close"],
                LeadCountByStage: new Dictionary<string, int>()),
            VoicesByLocale: new Dictionary<string, LocaleVoice>
            {
                ["en"] = new LocaleVoice("en", "# Voice\nWarm and professional.", "# Personality\nFriendly.", "hash-en")
            });

    private static BuildSiteContentInput BuildInput(
        SiteFacts? facts = null,
        string[]? locales = null,
        string template = "emerald-classic") =>
        new()
        {
            AccountId = "acc1",
            AgentId = "agent1",
            CorrelationId = "corr-123",
            Facts = facts ?? BuildFacts(),
            SupportedLocales = locales ?? ["en"],
            TemplateName = template,
        };

    // ── Full-success tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AllFieldsGenerated_ReturnsFull()
    {
        var (fn, _) = BuildFn("Hello, world!");
        var input = BuildInput();

        var result = await fn.RunAsync(input, Ct);

        result.ResultType.Should().Be(BuildResultType.Full);
        result.FallbackReason.Should().BeNull();
        result.ContentByLocale.Should().ContainKey("en");
    }

    [Fact]
    public async Task RunAsync_ReturnsNestedSectionStructure()
    {
        var (fn, _) = BuildFn("Section content here");
        var input = BuildInput();

        var result = await fn.RunAsync(input, Ct);

        result.ResultType.Should().Be(BuildResultType.Full);
        var enContent = result.ContentByLocale["en"];
        enContent.Should().BeOfType<Dictionary<string, object>>();

        var sections = (Dictionary<string, object>)enContent;
        // FieldSpecCatalog has hero, about, features, steps, testimonials, contact, thankyou, nav
        sections.Should().ContainKey("hero");
        sections.Should().ContainKey("about");
    }

    [Fact]
    public async Task RunAsync_HeroSectionContainsExpectedFields()
    {
        var (fn, _) = BuildFn("My hero content");
        var input = BuildInput();

        var result = await fn.RunAsync(input, Ct);

        var enContent = (Dictionary<string, object>)result.ContentByLocale["en"];
        var hero = (Dictionary<string, string>)enContent["hero"];
        hero.Should().ContainKey("headline");
        hero.Should().ContainKey("tagline");
        hero.Should().ContainKey("cta_text");
    }

    [Fact]
    public async Task RunAsync_AllFieldsInCatalogPresent()
    {
        var (fn, _) = BuildFn("content");
        var input = BuildInput();

        var result = await fn.RunAsync(input, Ct);

        var enContent = (Dictionary<string, object>)result.ContentByLocale["en"];
        var allFields = enContent.Values
            .OfType<Dictionary<string, string>>()
            .SelectMany(d => d.Keys)
            .ToHashSet();

        // All 15 FieldSpec names should map to sections in the output
        var expectedSections = FieldSpecCatalog.All
            .Select(s => s.Name.Split('.')[0])
            .Distinct()
            .ToList();

        foreach (var section in expectedSections)
        {
            enContent.Should().ContainKey(section,
                $"section '{section}' from FieldSpecCatalog should appear in output");
        }
    }

    // ── Partial-fallback tests (some fields fallback, locale still counts as success) ──

    [Fact]
    public async Task RunAsync_SomeFieldsFallback_LocaleStillCountsAsSuccess()
    {
        // Claude returns content for first call, throws for all subsequent calls
        var claudeMock = new Mock<IAnthropicClient>(MockBehavior.Loose);
        var callCount = 0;
        claudeMock
            .Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount <= 3)
                    return new AnthropicResponse("content", 100, 50, 100);
                throw new HttpRequestException("timeout");
            });

        var cacheMock = new Mock<IDistributedContentCache>(MockBehavior.Loose);
        cacheMock
            .Setup(c => c.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var generator = new VoicedContentGenerator(
            claudeMock.Object, cacheMock.Object, NullLogger<VoicedContentGenerator>.Instance);
        var fn = new BuildLocalizedSiteContentFunction(
            generator, NullLogger<BuildLocalizedSiteContentFunction>.Instance);

        var result = await fn.RunAsync(BuildInput(), Ct);

        // At least some fields succeeded — result should be Full, not Fallback
        result.ResultType.Should().Be(BuildResultType.Full);
        result.ContentByLocale.Should().ContainKey("en");
    }

    [Fact]
    public async Task RunAsync_FallbackFieldsGetFallbackValues_NotEmpty()
    {
        // All Claude calls throw → every field uses its FallbackValue
        var fn = BuildFnWithThrowingClaude();

        var result = await fn.RunAsync(BuildInput(), Ct);

        // All fields fell back → result should be Fallback
        result.ResultType.Should().Be(BuildResultType.Fallback);
    }

    // ── All-fallback tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AllFieldsFallback_ReturnsFallbackResult()
    {
        var fn = BuildFnWithThrowingClaude();

        var result = await fn.RunAsync(BuildInput(), Ct);

        result.ResultType.Should().Be(BuildResultType.Fallback);
        result.FallbackReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunAsync_NullFacts_ReturnsFallback()
    {
        var (fn, _) = BuildFn();
        var input = new BuildSiteContentInput
        {
            AccountId = "acc1",
            AgentId = "agent1",
            CorrelationId = "corr-1",
            Facts = null!,
            SupportedLocales = ["en"],
            TemplateName = "emerald-classic"
        };

        var result = await fn.RunAsync(input, Ct);

        result.ResultType.Should().Be(BuildResultType.Fallback);
        result.FallbackReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunAsync_EmptyLocales_ReturnsFallback()
    {
        var (fn, _) = BuildFn();
        var input = BuildInput(locales: []);

        var result = await fn.RunAsync(input, Ct);

        result.ResultType.Should().Be(BuildResultType.Fallback);
        result.FallbackReason.Should().NotBeNullOrEmpty();
    }

    // ── Multi-locale tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_MultiLocale_EnAndEs_BothGenerated()
    {
        // Facts has both en + es voices
        var facts = new SiteFacts(
            Agent: new AgentIdentity("Maria Lopez", "Maria Lopez", "REALTOR",
                "maria@example.com", "555-9999", "NJ-789", 8, ["en", "es"]),
            Brokerage: new BrokerageIdentity("Lopez Realty", null, null, null, null, null),
            Location: new LocationFacts("NJ", ["Newark", "Clifton"], new Dictionary<string, int>()),
            Specialties: new SpecialtiesFacts(
                ["Residential"], ["warm", "community"], new Dictionary<string, int>()),
            Trust: new TrustSignals(30, 4.8m, 60, TimeSpan.FromHours(1), 380_000m),
            RecentSales: [],
            Testimonials: [],
            Credentials: [],
            Stages: new PipelineStages(["Inquiry", "Close"], new Dictionary<string, int>()),
            VoicesByLocale: new Dictionary<string, LocaleVoice>
            {
                ["en"] = new LocaleVoice("en", "# Voice EN\nProfessional.", "# Personality EN\nFriendly.", "hash-en"),
                ["es"] = new LocaleVoice("es", "# Voice ES\nProfesional.", "# Personality ES\nAmigable.", "hash-es")
            });

        var (fn, _) = BuildFn("localized content");
        var input = BuildInput(facts: facts, locales: ["en", "es"]);

        var result = await fn.RunAsync(input, Ct);

        result.ResultType.Should().Be(BuildResultType.Full);
        result.ContentByLocale.Should().ContainKey("en");
        result.ContentByLocale.Should().ContainKey("es");
    }

    [Fact]
    public async Task RunAsync_MultiLocale_MissingEsVoice_FallsBackToEnVoice()
    {
        // Facts only has "en" voice — "es" locale should still generate using en voice as fallback
        var (fn, _) = BuildFn("fallback voice content");
        var input = BuildInput(locales: ["en", "es"]); // facts only has "en" voice

        var result = await fn.RunAsync(input, Ct);

        // Both locales should still succeed (en voice used as fallback for es)
        result.ResultType.Should().Be(BuildResultType.Full);
        result.ContentByLocale.Should().ContainKey("en");
        result.ContentByLocale.Should().ContainKey("es");
    }

    [Fact]
    public async Task RunAsync_MultiLocale_OnlyEnSucceeds_ReturnsFull()
    {
        // Build a generator where es-locale calls all fail
        var claudeMock = new Mock<IAnthropicClient>(MockBehavior.Loose);
        var callCount = 0;
        var totalSpecs = FieldSpecCatalog.All.Count;

        claudeMock
            .Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First batch (en locale) succeeds, second batch (es locale) throws
                return callCount <= totalSpecs
                    ? new AnthropicResponse("en content", 100, 50, 100)
                    : throw new HttpRequestException("es locale failure");
            });

        var cacheMock = new Mock<IDistributedContentCache>(MockBehavior.Loose);
        cacheMock
            .Setup(c => c.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var generator = new VoicedContentGenerator(
            claudeMock.Object, cacheMock.Object, NullLogger<VoicedContentGenerator>.Instance);
        var fn = new BuildLocalizedSiteContentFunction(
            generator, NullLogger<BuildLocalizedSiteContentFunction>.Instance);

        var result = await fn.RunAsync(BuildInput(locales: ["en", "es"]), Ct);

        // en succeeded — overall result is Full even if es fully failed
        result.ResultType.Should().Be(BuildResultType.Full);
        result.ContentByLocale.Should().ContainKey("en");
    }

    // ── BuildSectionContent unit tests ────────────────────────────────────────────

    [Fact]
    public void BuildSectionContent_GroupsFlatKeysIntoSections()
    {
        var flat = new Dictionary<string, string>
        {
            ["hero.headline"] = "Great Headline",
            ["hero.tagline"] = "A tagline here",
            ["about.bio"] = "My bio text",
            ["contact.title"] = "Reach Out"
        };

        var sections = BuildLocalizedSiteContentFunction.BuildSectionContent(flat);

        sections.Should().ContainKey("hero");
        sections.Should().ContainKey("about");
        sections.Should().ContainKey("contact");

        var hero = (Dictionary<string, string>)sections["hero"];
        hero["headline"].Should().Be("Great Headline");
        hero["tagline"].Should().Be("A tagline here");

        var about = (Dictionary<string, string>)sections["about"];
        about["bio"].Should().Be("My bio text");
    }

    [Fact]
    public void BuildSectionContent_BareKeyGoesToMetaBucket()
    {
        var flat = new Dictionary<string, string>
        {
            ["orphanKey"] = "value",
            ["hero.headline"] = "Title"
        };

        var sections = BuildLocalizedSiteContentFunction.BuildSectionContent(flat);

        sections.Should().ContainKey("meta");
        var meta = (Dictionary<string, string>)sections["meta"];
        meta["orphanKey"].Should().Be("value");
    }

    [Fact]
    public void BuildSectionContent_EmptyInput_ReturnsEmptyDictionary()
    {
        var sections = BuildLocalizedSiteContentFunction.BuildSectionContent([]);

        sections.Should().BeEmpty();
    }

    // ── TemplateSelector tests ────────────────────────────────────────────────────

    private static SiteFacts MakeFacts(string[] vibeHints, string[] specialties,
        int yearsExperience = 10) =>
        new(
            Agent: new AgentIdentity("Test Agent", "Test Agent", "REALTOR",
                "test@example.com", "555-0000", null, yearsExperience, ["en"]),
            Brokerage: new BrokerageIdentity("Test Brokerage", null, null, null, null, null),
            Location: new LocationFacts("NJ", ["Area"], new Dictionary<string, int>()),
            Specialties: new SpecialtiesFacts(specialties, vibeHints, new Dictionary<string, int>()),
            Trust: new TrustSignals(0, 0m, 0, TimeSpan.Zero, 0m),
            RecentSales: [], Testimonials: [], Credentials: [],
            Stages: new PipelineStages([], new Dictionary<string, int>()),
            VoicesByLocale: new Dictionary<string, LocaleVoice>());

    [Fact]
    public void SelectTemplate_LuxuryVibeHint_ReturnsLuxuryEstate_WhenExperienced()
    {
        var facts = MakeFacts(["luxury", "premium"], ["Residential"], yearsExperience: 8);
        TemplateSelector.SelectTemplate(facts).Should().Be("luxury-estate");
    }

    [Fact]
    public void SelectTemplate_LuxuryVibeHint_ReturnsLightLuxury_WhenNewer()
    {
        var facts = MakeFacts(["luxury"], ["Residential"], yearsExperience: 3);
        TemplateSelector.SelectTemplate(facts).Should().Be("light-luxury");
    }

    [Fact]
    public void SelectTemplate_CommercialSpecialty_ReturnsCommercial()
    {
        var facts = MakeFacts([], ["Commercial", "Office"]);
        TemplateSelector.SelectTemplate(facts).Should().Be("commercial");
    }

    [Fact]
    public void SelectTemplate_WarmCommunityVibe_ReturnsWarmCommunity()
    {
        var facts = MakeFacts(["community", "family"], ["Residential"]);
        TemplateSelector.SelectTemplate(facts).Should().Be("warm-community");
    }

    [Fact]
    public void SelectTemplate_DefaultAgent_ReturnsEmeraldClassic()
    {
        var facts = MakeFacts([], ["Residential"]);
        TemplateSelector.SelectTemplate(facts).Should().Be("emerald-classic");
    }

    [Fact]
    public void SelectTemplate_CommercialBeatsLuxury_WhenBoth()
    {
        // Commercial specialty takes priority over luxury vibe
        var facts = MakeFacts(["luxury"], ["Commercial"]);
        TemplateSelector.SelectTemplate(facts).Should().Be("commercial");
    }

    [Fact]
    public void SelectTemplate_NullFacts_Throws()
    {
        var act = () => TemplateSelector.SelectTemplate(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectTemplate_PartialLuxuryKeywordMatch_ReturnsLuxury()
    {
        // "high-end luxury homes" contains "luxury"
        var facts = MakeFacts(["high-end luxury homes"], [], yearsExperience: 6);
        TemplateSelector.SelectTemplate(facts).Should().Be("luxury-estate");
    }

    [Fact]
    public void SelectTemplate_InvestmentSpecialty_ReturnsCommercial()
    {
        var facts = MakeFacts([], ["investment properties"]);
        TemplateSelector.SelectTemplate(facts).Should().Be("commercial");
    }
}
