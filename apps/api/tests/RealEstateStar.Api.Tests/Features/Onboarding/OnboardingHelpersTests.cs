using FluentAssertions;
using RealEstateStar.Api.Features.Onboarding;

namespace RealEstateStar.Api.Tests.Features.Onboarding;

public class OnboardingHelpersTests
{
    // ── Normal input ───────────────────────────────────────────────────────────

    [Fact]
    public void GenerateSlug_NormalName_ReturnsLowercaseHyphenated()
    {
        var slug = OnboardingHelpers.GenerateSlug("Jane Doe");

        slug.Should().Be("jane-doe");
    }

    [Theory]
    [InlineData("Alice Smith", "alice-smith")]
    [InlineData("BOB BUILDER", "bob-builder")]
    [InlineData("Carlos De La Vega", "carlos-de-la-vega")]
    public void GenerateSlug_WithSpaces_ReplacesSpacesWithHyphens(string name, string expected)
    {
        var slug = OnboardingHelpers.GenerateSlug(name);

        slug.Should().Be(expected);
    }

    // ── Null input ─────────────────────────────────────────────────────────────

    [Fact]
    public void GenerateSlug_NullName_ReturnsAgent()
    {
        var slug = OnboardingHelpers.GenerateSlug(null);

        slug.Should().Be("agent");
    }

    // ── Special characters ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateSlug_SpecialCharacters_AreStripped()
    {
        // Spaces become hyphens first; then non-alphanumeric non-hyphen chars (apostrophe, &, period, comma)
        // are stripped. Adjacent hyphens may remain when a stripped char was surrounded by hyphens.
        // "O'Brien & Co., LLC" → "o'brien & co., llc" → "o'brien-&-co.,-llc" → "obrien--co-llc"
        var slug = OnboardingHelpers.GenerateSlug("O'Brien & Co., LLC");

        slug.Should().Be("obrien--co-llc");
    }

    [Fact]
    public void GenerateSlug_HyphensInInput_ArePreserved()
    {
        var slug = OnboardingHelpers.GenerateSlug("Mary-Jane Watson");

        slug.Should().Be("mary-jane-watson");
    }

    // ── Empty string after stripping ───────────────────────────────────────────

    [Fact]
    public void GenerateSlug_OnlySpecialChars_ReturnsAgent()
    {
        // After stripping, the slug becomes empty — fallback to "agent"
        var slug = OnboardingHelpers.GenerateSlug("!!!@@@###");

        slug.Should().Be("agent");
    }

    [Fact]
    public void GenerateSlug_EmptyString_ReturnsAgent()
    {
        // Empty string lowercased + filtered = empty → fallback
        var slug = OnboardingHelpers.GenerateSlug("");

        slug.Should().Be("agent");
    }
}
