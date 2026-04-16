using FluentAssertions;
using RealEstateStar.DataServices.Legal;

namespace RealEstateStar.DataServices.Tests.Legal;

public sealed class LegalPageRendererTests : IDisposable
{
    private readonly string _root;

    public LegalPageRendererTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"legal-templates-{Guid.NewGuid():N}");
        BuildTemplateTree(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // ─── Render ───────────────────────────────────────────────────────────────

    [Fact]
    public void Render_SubstitutesAllVariablesInTemplate()
    {
        var path = Path.Combine(_root, "_defaults", "en", "privacy.md");
        var variables = MakeVariables();

        var result = LegalPageRenderer.Render(path, variables);

        result.Should().Contain("Jane Smith");
        result.Should().Contain("Smith Realty");
        result.Should().Contain("NJ");
        result.Should().Contain("jane@smithrealty.com");
        result.Should().Contain("555-123-4567");
        result.Should().Contain("BR-9999");
        result.Should().Contain("2026-01-01T00:00:00Z");
    }

    [Fact]
    public void Render_AppendDisclaimerFooterWhenNotPresent()
    {
        var path = Path.Combine(_root, "_defaults", "en", "terms.md");
        var variables = MakeVariables();

        var result = LegalPageRenderer.Render(path, variables);

        result.Should().Contain("*This page was generated from professional information on file.");
        result.Should().Contain("does not constitute legal advice");
    }

    [Fact]
    public void Render_DoesNotDuplicateDisclaimerFooter()
    {
        var path = Path.Combine(_root, "_defaults", "en", "privacy.md");
        var variables = MakeVariables();

        var result = LegalPageRenderer.Render(path, variables);

        var count = CountOccurrences(result, "*This page was generated from professional information on file.");
        count.Should().Be(1);
    }

    [Fact]
    public void Render_LeavesUnknownVariablesAsPlaceholder()
    {
        var path = Path.Combine(_root, "_defaults", "en", "privacy.md");
        var variables = new Dictionary<string, string>
        {
            ["agent.name"] = "Jane Smith",
            // agent.email intentionally omitted
        };

        var result = LegalPageRenderer.Render(path, variables);

        result.Should().Contain("{{agent.email}}");
    }

    // ─── RenderTemplate (internal) ────────────────────────────────────────────

    [Fact]
    public void RenderTemplate_SubstitutesMultipleVariables()
    {
        const string template = "Hello {{agent.name}} at {{brokerage.name}}.";
        var variables = new Dictionary<string, string>
        {
            ["agent.name"] = "Jane Smith",
            ["brokerage.name"] = "Smith Realty",
        };

        var result = LegalPageRenderer.RenderTemplate(template, variables);

        result.Should().Be("Hello Jane Smith at Smith Realty.\n---\n*This page was generated from professional information on file. It is provided for informational purposes only and does not constitute legal advice. Please review with legal counsel before relying on this content.*");
    }

    [Fact]
    public void RenderTemplate_DoesNotDuplicateFooterWhenAlreadyPresent()
    {
        const string template = "Content here.\n\n---\n*This page was generated from professional information on file. It is provided for informational purposes only and does not constitute legal advice. Please review with legal counsel before relying on this content.*";
        var variables = new Dictionary<string, string>();

        var result = LegalPageRenderer.RenderTemplate(template, variables);

        var count = CountOccurrences(result, "*This page was generated from professional information on file.");
        count.Should().Be(1);
    }

    // ─── ResolvePath ──────────────────────────────────────────────────────────

    [Fact]
    public void ResolvePath_ReturnsStateOverrideWhenExists()
    {
        var path = LegalPageRenderer.ResolvePath("fair-housing", "en", "NJ", _root);

        path.Should().NotBeNull();
        path.Should().Contain(Path.Combine("by-state", "NJ", "en", "fair-housing.md"));
    }

    [Fact]
    public void ResolvePath_ReturnsStateOverrideForCA_Privacy()
    {
        var path = LegalPageRenderer.ResolvePath("privacy", "en", "CA", _root);

        path.Should().NotBeNull();
        path.Should().Contain(Path.Combine("by-state", "CA", "en", "privacy.md"));
    }

    [Fact]
    public void ResolvePath_FallsBackToDefaultLocale_WhenNoStateOverride()
    {
        var path = LegalPageRenderer.ResolvePath("terms", "en", "TX", _root);

        path.Should().NotBeNull();
        path.Should().Contain(Path.Combine("_defaults", "en", "terms.md"));
    }

    [Fact]
    public void ResolvePath_FallsBackToEnglish_WhenLocaleNotFound()
    {
        // Portuguese has no templates in the tree
        var path = LegalPageRenderer.ResolvePath("privacy", "pt", null, _root);

        path.Should().NotBeNull();
        path.Should().Contain(Path.Combine("_defaults", "en", "privacy.md"));
    }

    [Fact]
    public void ResolvePath_ReturnsNull_WhenPageNotFound()
    {
        var path = LegalPageRenderer.ResolvePath("nonexistent-page", "en", null, _root);

        path.Should().BeNull();
    }

    [Fact]
    public void ResolvePath_ReturnsNull_WhenNoStateAndNoDefaultLocaleAndNoEnglishFallback()
    {
        // 'xx' locale doesn't exist and English fallback for 'missing-page' doesn't exist either
        var path = LegalPageRenderer.ResolvePath("missing-page", "xx", null, _root);

        path.Should().BeNull();
    }

    [Fact]
    public void ResolvePath_PrefersStateOverLocaleDefault()
    {
        // NJ has fair-housing — make sure it picks NJ over _defaults
        var njPath = LegalPageRenderer.ResolvePath("fair-housing", "en", "NJ", _root);
        var defaultPath = LegalPageRenderer.ResolvePath("fair-housing", "en", null, _root);

        njPath.Should().NotBe(defaultPath);
        njPath.Should().Contain("by-state");
        defaultPath.Should().Contain("_defaults");
    }

    // ─── All 5 English pages render ───────────────────────────────────────────

    [Theory]
    [InlineData("privacy")]
    [InlineData("terms")]
    [InlineData("accessibility")]
    [InlineData("fair-housing")]
    [InlineData("tcpa")]
    public void ResolvePath_AllEnglishPagesResolve(string page)
    {
        var path = LegalPageRenderer.ResolvePath(page, "en", null, _root);

        path.Should().NotBeNull($"page '{page}' should have an English template");
        File.Exists(path).Should().BeTrue();
    }

    // ─── All 4 Spanish pages render (no TCPA) ─────────────────────────────────

    [Theory]
    [InlineData("privacy")]
    [InlineData("terms")]
    [InlineData("accessibility")]
    [InlineData("fair-housing")]
    public void ResolvePath_AllSpanishPagesResolve(string page)
    {
        var path = LegalPageRenderer.ResolvePath(page, "es", null, _root);

        path.Should().NotBeNull($"page '{page}' should have a Spanish template");
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void ResolvePath_TCPA_HasNoSpanishTemplate_FallsBackToEnglish()
    {
        // TCPA is English-only — es/tcpa.md must not exist; resolver falls back to en
        var esPath = Path.Combine(_root, "_defaults", "es", "tcpa.md");
        File.Exists(esPath).Should().BeFalse("TCPA must remain English-only per legal requirement");

        var resolved = LegalPageRenderer.ResolvePath("tcpa", "es", null, _root);
        resolved.Should().NotBeNull();
        resolved.Should().Contain(Path.Combine("_defaults", "en", "tcpa.md"),
            "Spanish TCPA must fall back to English");
    }

    // ─── Disclaimer footer on every rendered output ───────────────────────────

    [Theory]
    [InlineData("privacy", "en")]
    [InlineData("terms", "en")]
    [InlineData("accessibility", "en")]
    [InlineData("fair-housing", "en")]
    [InlineData("tcpa", "en")]
    [InlineData("privacy", "es")]
    [InlineData("terms", "es")]
    [InlineData("accessibility", "es")]
    [InlineData("fair-housing", "es")]
    public void Render_DisclaimerPresentOnEveryPage(string page, string locale)
    {
        var path = LegalPageRenderer.ResolvePath(page, locale, null, _root);
        path.Should().NotBeNull();

        var result = LegalPageRenderer.Render(path!, MakeVariables());

        result.Should().Contain("*This page was generated from professional information on file.",
            $"{page}/{locale} must contain the disclaimer footer");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static Dictionary<string, string> MakeVariables() => new()
    {
        ["agent.name"] = "Jane Smith",
        ["agent.email"] = "jane@smithrealty.com",
        ["agent.phone"] = "555-123-4567",
        ["agent.state"] = "NJ",
        ["agent.state_full"] = "New Jersey",
        ["brokerage.name"] = "Smith Realty",
        ["brokerage.license_number"] = "BR-9999",
        ["generated_at"] = "2026-01-01T00:00:00Z",
    };

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    // ─── Template tree builder ────────────────────────────────────────────────

    private static void BuildTemplateTree(string root)
    {
        // _defaults/en — all 5 pages
        CreateTemplate(root, "_defaults", "en", "privacy.md",
            "# Privacy\n{{agent.name}} at {{brokerage.name}} in {{agent.state}}. Contact: {{agent.email}}, {{agent.phone}}. License: {{brokerage.license_number}}. Generated: {{generated_at}}.\n\n---\n*This page was generated from professional information on file. It is provided for informational purposes only and does not constitute legal advice. Please review with legal counsel before relying on this content.*");

        CreateTemplate(root, "_defaults", "en", "terms.md",
            "# Terms\nTerms for {{agent.name}} at {{brokerage.name}} in {{agent.state}}.");

        CreateTemplate(root, "_defaults", "en", "accessibility.md",
            "# Accessibility\nAccessibility commitment for {{agent.name}} at {{brokerage.name}} in {{agent.state}}.");

        CreateTemplate(root, "_defaults", "en", "fair-housing.md",
            "# Fair Housing\n{{agent.name}} of {{brokerage.name}} in {{agent.state}} commits to fair housing.");

        CreateTemplate(root, "_defaults", "en", "tcpa.md",
            "# TCPA\nTCPA consent for {{agent.name}} at {{brokerage.name}}. Contact: {{agent.email}}, {{agent.phone}}.");

        // _defaults/es — 4 pages (no tcpa.md)
        CreateTemplate(root, "_defaults", "es", "privacy.md",
            "# Privacidad\n{{agent.name}} en {{brokerage.name}} en {{agent.state}}. Contacto: {{agent.email}}.");

        CreateTemplate(root, "_defaults", "es", "terms.md",
            "# Términos\nTérminos para {{agent.name}} en {{brokerage.name}}.");

        CreateTemplate(root, "_defaults", "es", "accessibility.md",
            "# Accesibilidad\nCompromiso de accesibilidad de {{agent.name}}.");

        CreateTemplate(root, "_defaults", "es", "fair-housing.md",
            "# Vivienda Justa\n{{agent.name}} de {{brokerage.name}} se compromete con la vivienda justa.");

        // by-state/NJ/en
        CreateTemplate(root, "by-state/NJ/en", null, "fair-housing.md",
            "# NJ Fair Housing\n{{agent.name}} of {{brokerage.name}} — NJ-specific including Source of Lawful Income protection.");

        // by-state/CA/en
        CreateTemplate(root, "by-state/CA/en", null, "privacy.md",
            "# CA Privacy\n{{agent.name}} at {{brokerage.name}} — CCPA/CPRA references for California.");

        // by-state/NY/en
        CreateTemplate(root, "by-state/NY/en", null, "accessibility.md",
            "# NY Accessibility\n{{agent.name}} at {{brokerage.name}} — NYC Human Rights Law.");
    }

    private static void CreateTemplate(string root, string folder1, string? folder2, string file, string content)
    {
        var dir = folder2 is null
            ? Path.Combine(root, folder1)
            : Path.Combine(root, folder1, folder2);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, file), content);
    }
}
