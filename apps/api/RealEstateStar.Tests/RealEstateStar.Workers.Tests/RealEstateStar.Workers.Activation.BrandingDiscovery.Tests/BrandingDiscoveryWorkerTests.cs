using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Workers.Activation.BrandingDiscovery.Tests;

public class BrandingDiscoveryWorkerTests
{
    private readonly BrandingDiscoveryWorker _sut;

    public BrandingDiscoveryWorkerTests()
    {
        _sut = new BrandingDiscoveryWorker(
            NullLogger<BrandingDiscoveryWorker>.Instance);
    }

    // ── Test data ─────────────────────────────────────────────────────────────

    private static AgentDiscovery EmptyDiscovery() =>
        new(null, null, null, [], [], [], null, false);

    private static EmailCorpus EmptyCorpus() =>
        new([], [], null);

    // ── Color extraction ──────────────────────────────────────────────────────

    [Fact]
    public void ExtractColors_FromThemeColorMeta_ExtractsHex()
    {
        var websites = new List<DiscoveredWebsite>
        {
            new("https://example.com", "own-site",
                "<meta name=\"theme-color\" content=\"#2E4A8B\"><body>test</body>")
        };

        var colors = _sut.ExtractColors(websites);

        colors.Should().ContainSingle(c => c.Hex == "#2E4A8B");
    }

    [Fact]
    public void ExtractColors_FromCssColorDeclaration_ExtractsHex()
    {
        var websites = new List<DiscoveredWebsite>
        {
            new("https://example.com", "own-site",
                "<style>.header { color: #3A5A8F; background-color: #F0F4F8; }</style>")
        };

        var colors = _sut.ExtractColors(websites);

        colors.Select(c => c.Hex).Should().Contain("#3A5A8F");
    }

    [Fact]
    public void ExtractColors_IgnoresCommonSystemColors()
    {
        var websites = new List<DiscoveredWebsite>
        {
            new("https://example.com", "own-site",
                "<style>body { color: #000000; background: #FFFFFF; } .brand { color: #D4A843; }</style>")
        };

        var colors = _sut.ExtractColors(websites);

        colors.Should().NotContain(c => c.Hex == "#000000");
        colors.Should().NotContain(c => c.Hex == "#FFFFFF");
        colors.Select(c => c.Hex).Should().Contain("#D4A843");
    }

    [Fact]
    public void ExtractColors_EmptyWebsites_ReturnsEmpty()
    {
        var colors = _sut.ExtractColors([]);
        colors.Should().BeEmpty();
    }

    [Fact]
    public void ExtractColors_WebsiteWithNullHtml_ReturnsEmpty()
    {
        var websites = new List<DiscoveredWebsite>
        {
            new("https://example.com", "own-site", null)
        };

        var colors = _sut.ExtractColors(websites);
        colors.Should().BeEmpty();
    }

    // ── Font extraction ───────────────────────────────────────────────────────

    [Fact]
    public void ExtractFonts_FromGoogleFontsLink_ExtractsFamilyName()
    {
        var websites = new List<DiscoveredWebsite>
        {
            new("https://example.com", "own-site",
                "<link href=\"https://fonts.googleapis.com/css?family=Playfair+Display\" rel=\"stylesheet\">")
        };

        var fonts = _sut.ExtractFonts(websites);

        fonts.Should().ContainSingle(f => f.Family == "Playfair Display");
    }

    [Fact]
    public void ExtractFonts_FromFontFaceDeclaration_ExtractsFamilyName()
    {
        var websites = new List<DiscoveredWebsite>
        {
            new("https://example.com", "own-site",
                "@font-face { font-family: 'BrandFont'; src: url('/fonts/brand.woff2'); }")
        };

        var fonts = _sut.ExtractFonts(websites);

        fonts.Should().ContainSingle(f => f.Family == "BrandFont");
    }

    [Fact]
    public void ExtractFonts_IgnoresSystemFonts()
    {
        var websites = new List<DiscoveredWebsite>
        {
            new("https://example.com", "own-site",
                "<style>body { font-family: Arial, sans-serif; } .brand { font-family: Lato, sans-serif; }</style>")
        };

        var fonts = _sut.ExtractFonts(websites);

        fonts.Should().NotContain(f => f.Family == "Arial");
        fonts.Should().NotContain(f => f.Family == "sans-serif");
    }

    [Fact]
    public void ExtractFonts_EmptyWebsites_ReturnsEmpty()
    {
        var fonts = _sut.ExtractFonts([]);
        fonts.Should().BeEmpty();
    }

    // ── Logo extraction ───────────────────────────────────────────────────────

    [Fact]
    public void ExtractLogos_WithLogoBytes_ReturnsFullLogo()
    {
        var logoBytes = new byte[] { 1, 2, 3 };
        var discovery = new AgentDiscovery(null, logoBytes, null, [], [], [], null, false);

        var logos = _sut.ExtractLogos(discovery, EmptyCorpus());

        logos.Should().ContainSingle(l => l.Variant == "full");
    }

    [Fact]
    public void ExtractLogos_WithHeadshotBytes_ReturnsHeadshot()
    {
        var headshotBytes = new byte[] { 4, 5, 6 };
        var discovery = new AgentDiscovery(headshotBytes, null, null, [], [], [], null, false);

        var logos = _sut.ExtractLogos(discovery, EmptyCorpus());

        logos.Should().ContainSingle(l => l.Variant == "headshot");
    }

    [Fact]
    public void ExtractLogos_WithEmailSigLogoUrl_ReturnsEmailSigVariant()
    {
        var sig = new EmailSignature("Jenise", "Agent", "555", null, null, [], null, null, "https://example.com/logo.png");
        var corpus = new EmailCorpus([], [], sig);

        var logos = _sut.ExtractLogos(EmptyDiscovery(), corpus);

        logos.Should().ContainSingle(l => l.Variant == "email-sig");
    }

    [Fact]
    public void ExtractLogos_NoBranding_ReturnsEmpty()
    {
        var logos = _sut.ExtractLogos(EmptyDiscovery(), EmptyCorpus());
        logos.Should().BeEmpty();
    }

    // ── ScoreTemplate ────────────────────────────────────────────────────────

    [Fact]
    public void ScoreTemplate_DarkColors_RecommmendsLuxury()
    {
        var colors = new List<ColorEntry> { new("Brand", "#1A1A2E", "own-site", "css") };
        var fonts = new List<FontEntry>();
        var profiles = new List<ThirdPartyProfile>();

        var (template, _) = BrandingDiscoveryWorker.ScoreTemplate(colors, fonts, profiles, null);

        template.Should().Be("luxury");
    }

    [Fact]
    public void ScoreTemplate_LuxurySpecialty_RecommendsLuxury()
    {
        var colors = new List<ColorEntry>();
        var fonts = new List<FontEntry>();
        var profiles = new List<ThirdPartyProfile>();

        var (template, _) = BrandingDiscoveryWorker.ScoreTemplate(colors, fonts, profiles, "luxury homes, estate properties");

        template.Should().Be("luxury");
    }

    [Fact]
    public void ScoreTemplate_WarmTones_RecommendsWarm()
    {
        // High red, medium green, low blue → warm
        var colors = new List<ColorEntry> { new("Brand", "#C8843A", "own-site", "css") };
        var fonts = new List<FontEntry>();
        var profiles = new List<ThirdPartyProfile>();

        var (template, _) = BrandingDiscoveryWorker.ScoreTemplate(colors, fonts, profiles, "family, community");

        template.Should().Be("warm");
    }

    [Fact]
    public void ScoreTemplate_NeutralColors_RecommendsProfessional()
    {
        // Muted/neutral colors where r ≈ g ≈ b, not too dark, not too bright
        var colors = new List<ColorEntry>
        {
            new("Brand", "#808080", "own-site", "css"),
            new("Brand", "#909090", "own-site", "css"),
            new("Brand", "#A0A0A0", "own-site", "css")
        };
        var fonts = new List<FontEntry>();
        var profiles = new List<ThirdPartyProfile>();

        var (template, _) = BrandingDiscoveryWorker.ScoreTemplate(colors, fonts, profiles, "commercial, corporate");

        template.Should().Be("professional");
    }

    [Fact]
    public void ScoreTemplate_ModernFonts_RecommendsModern()
    {
        var colors = new List<ColorEntry>();
        var fonts = new List<FontEntry>
        {
            new("Body", "Inter", "400", "google-fonts"),
            new("Body", "Roboto", "400", "google-fonts")
        };
        var profiles = new List<ThirdPartyProfile>();

        var (template, _) = BrandingDiscoveryWorker.ScoreTemplate(colors, fonts, profiles, "new construction, development");

        template.Should().Be("modern");
    }

    [Fact]
    public void ScoreTemplate_SerifDisplayFont_BoostsLuxury()
    {
        var colors = new List<ColorEntry>();
        var fonts = new List<FontEntry>
        {
            new("Display", "Playfair Display", "400", "google-fonts")
        };
        var profiles = new List<ThirdPartyProfile>();

        var (template, _) = BrandingDiscoveryWorker.ScoreTemplate(colors, fonts, profiles, "luxury");

        template.Should().Be("luxury");
    }

    [Fact]
    public void ScoreTemplate_ProfileWithLuxuryBio_BoostsLuxury()
    {
        var colors = new List<ColorEntry>();
        var fonts = new List<FontEntry>();
        var profiles = new List<ThirdPartyProfile>
        {
            new("zillow", "Specializing in luxury million-dollar homes", [], null, null, null, [], [], [], [])
        };

        var (template, _) = BrandingDiscoveryWorker.ScoreTemplate(colors, fonts, profiles, null);

        template.Should().Be("luxury");
    }

    [Fact]
    public void ScoreTemplate_ProfileWithCommunityBio_BoostsWarm()
    {
        var colors = new List<ColorEntry>();
        var fonts = new List<FontEntry>();
        var profiles = new List<ThirdPartyProfile>
        {
            new("realtor", "Helping families find their dream neighborhood in the community", [], null, null, null, [], [], [], [])
        };

        var (template, _) = BrandingDiscoveryWorker.ScoreTemplate(colors, fonts, profiles, null);

        template.Should().Be("warm");
    }

    [Fact]
    public void ScoreTemplate_NoSignals_DefaultsToModern()
    {
        var (template, reason) = BrandingDiscoveryWorker.ScoreTemplate([], [], [], null);

        template.Should().Be("modern");
        reason.Should().Contain("insufficient brand signals");
    }

    [Fact]
    public void ScoreTemplate_GoldColor_BoostsLuxury()
    {
        // Gold: r > 180, g > 150, b < 80
        var colors = new List<ColorEntry> { new("Brand", "#D4A843", "own-site", "css") };
        var fonts = new List<FontEntry>();
        var profiles = new List<ThirdPartyProfile>();

        var (template, _) = BrandingDiscoveryWorker.ScoreTemplate(colors, fonts, profiles, null);

        template.Should().Be("luxury");
    }

    [Fact]
    public void ScoreTemplate_NavyColor_BoostsLuxury()
    {
        // Navy: r < 50, g < 50, b > 100
        var colors = new List<ColorEntry> { new("Brand", "#1A1A8B", "own-site", "css") };
        var fonts = new List<FontEntry>();
        var profiles = new List<ThirdPartyProfile>();

        var (template, _) = BrandingDiscoveryWorker.ScoreTemplate(colors, fonts, profiles, null);

        template.Should().Be("luxury");
    }

    [Fact]
    public void ScoreTemplate_ShortHex_SkipsColor()
    {
        // Short hex (3 chars) should be skipped
        var colors = new List<ColorEntry> { new("Brand", "#FFF", "own-site", "css") };
        var fonts = new List<FontEntry>();
        var profiles = new List<ThirdPartyProfile>();

        var (template, _) = BrandingDiscoveryWorker.ScoreTemplate(colors, fonts, profiles, null);

        template.Should().Be("modern"); // default — no signal from short hex
    }

    // ── Full DiscoverAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_ReturnsBrandingKit_WithExtractedData()
    {
        var html = "<style>.brand { color: #4A90E2; } .bg { background-color: #F5F5F5; }</style>" +
                   "<link href=\"https://fonts.googleapis.com/css?family=Open+Sans\" rel=\"stylesheet\">";
        var websites = new List<DiscoveredWebsite> { new("https://example.com", "own-site", html) };
        var discovery = new AgentDiscovery(null, null, null, websites, [], [], null, false);

        var result = await _sut.DiscoverAsync("Jenise", discovery, EmptyCorpus(), new DriveIndex("fid", [], new Dictionary<string, string>(), [], []), default);

        result.Kit.Should().NotBeNull();
        result.BrandingKitMarkdown.Should().Contain("# Branding Kit: Jenise");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyDiscovery_DefaultsToModernTemplate()
    {
        var discovery = EmptyDiscovery();

        var result = await _sut.DiscoverAsync("Jenise", discovery, EmptyCorpus(), new DriveIndex("fid", [], new Dictionary<string, string>(), [], []), default);

        result.Kit.RecommendedTemplate.Should().Be("modern");
    }
}
