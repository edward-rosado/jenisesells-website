using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Activation.BrandingDiscovery.Tests;

public class BrandingDiscoveryWorkerTests
{
    private readonly Mock<IAnthropicClient> _anthropic = new(MockBehavior.Strict);
    private readonly Mock<IContentSanitizer> _sanitizer = new(MockBehavior.Strict);
    private readonly BrandingDiscoveryWorker _sut;

    public BrandingDiscoveryWorkerTests()
    {
        _sanitizer.Setup(s => s.Sanitize(It.IsAny<string>()))
            .Returns<string>(s => s);
        _sut = new BrandingDiscoveryWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<BrandingDiscoveryWorker>.Instance);
    }

    // ── Test data ─────────────────────────────────────────────────────────────

    private static AnthropicResponse MakeTemplateResponse(string template = "warm", string reason = "Approachable") =>
        new($"Template: {template}\nReason: {reason}", 50, 30, 400.0);

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

    // ── Template parsing ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("Template: luxury\nReason: High-end market", "luxury", "High-end market")]
    [InlineData("Template: modern\nReason: Clean minimal design", "modern", "Clean minimal design")]
    [InlineData("Template: warm\nReason: Community focused", "warm", "Community focused")]
    [InlineData("Template: professional\nReason: Traditional approach", "professional", "Traditional approach")]
    public void ParseTemplateResponse_ValidTemplate_ParsesCorrectly(
        string response, string expectedTemplate, string expectedReason)
    {
        var (template, reason) = BrandingDiscoveryWorker.ParseTemplateResponse(response);
        template.Should().Be(expectedTemplate);
        reason.Should().Be(expectedReason);
    }

    [Fact]
    public void ParseTemplateResponse_InvalidTemplate_DefaultsToModern()
    {
        var (template, _) = BrandingDiscoveryWorker.ParseTemplateResponse("Template: unknown\nReason: test");
        template.Should().Be("modern");
    }

    [Fact]
    public void ParseTemplateResponse_EmptyResponse_DefaultsToModern()
    {
        var (template, _) = BrandingDiscoveryWorker.ParseTemplateResponse(string.Empty);
        template.Should().Be("modern");
    }

    // ── Full DiscoverAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_ReturnsBrandingKit_WithExtractedData()
    {
        var html = "<style>.brand { color: #4A90E2; } .bg { background-color: #F5F5F5; }</style>" +
                   "<link href=\"https://fonts.googleapis.com/css?family=Open+Sans\" rel=\"stylesheet\">";
        var websites = new List<DiscoveredWebsite> { new("https://example.com", "own-site", html) };
        var discovery = new AgentDiscovery(null, null, null, websites, [], [], null, false);

        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTemplateResponse("modern", "Clean design"));

        var result = await _sut.DiscoverAsync("Jenise", discovery, EmptyCorpus(), new DriveIndex("fid", [], new Dictionary<string, string>(), []), default);

        result.Kit.Should().NotBeNull();
        result.BrandingKitMarkdown.Should().Contain("# Branding Kit: Jenise");
        result.BrandingKitMarkdown.Should().Contain("modern");
    }

    [Fact]
    public async Task DiscoverAsync_ClaudeFailure_FallsBackToModernTemplate()
    {
        var discovery = EmptyDiscovery();
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Claude unavailable"));

        var result = await _sut.DiscoverAsync("Jenise", discovery, EmptyCorpus(), new DriveIndex("fid", [], new Dictionary<string, string>(), []), default);

        result.Kit.RecommendedTemplate.Should().Be("modern");
    }

    [Fact]
    public async Task DiscoverAsync_SanitizesProfileSummaryBeforeClaude()
    {
        var discovery = EmptyDiscovery();
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTemplateResponse());

        await _sut.DiscoverAsync("Jenise", discovery, EmptyCorpus(), new DriveIndex("fid", [], new Dictionary<string, string>(), []), default);

        _sanitizer.Verify(s => s.Sanitize(It.IsAny<string>()), Times.AtLeastOnce);
    }
}
