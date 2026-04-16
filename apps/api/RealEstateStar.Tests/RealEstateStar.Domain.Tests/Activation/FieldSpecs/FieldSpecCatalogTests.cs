using FluentAssertions;
using RealEstateStar.Domain.Activation.FieldSpecs;

namespace RealEstateStar.Domain.Tests.Activation.FieldSpecs;

public class FieldSpecCatalogTests
{
    private static readonly IReadOnlyList<string> ValidModels = ["haiku-4-5", "sonnet-4-6"];

    [Fact]
    public void All_contains_exactly_15_entries()
    {
        FieldSpecCatalog.All.Should().HaveCount(15);
    }

    [Fact]
    public void All_entries_have_unique_Names()
    {
        var names = FieldSpecCatalog.All.Select(s => s.Name).ToList();
        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_entries_have_non_empty_PromptTemplate()
    {
        foreach (var spec in FieldSpecCatalog.All)
        {
            spec.PromptTemplate.Should().NotBeNullOrWhiteSpace(
                because: $"spec '{spec.Name}' must have a non-empty PromptTemplate");
        }
    }

    [Fact]
    public void All_entries_have_positive_MaxOutputTokens()
    {
        foreach (var spec in FieldSpecCatalog.All)
        {
            spec.MaxOutputTokens.Should().BePositive(
                because: $"spec '{spec.Name}' must have positive MaxOutputTokens");
        }
    }

    [Fact]
    public void All_entries_have_valid_Model()
    {
        foreach (var spec in FieldSpecCatalog.All)
        {
            spec.Model.Should().BeOneOf(ValidModels,
                because: $"spec '{spec.Name}' must use a recognized model");
        }
    }

    [Fact]
    public void All_entries_have_non_null_FallbackValue()
    {
        foreach (var spec in FieldSpecCatalog.All)
        {
            spec.FallbackValue.Should().NotBeNull(
                because: $"spec '{spec.Name}' must have a non-null FallbackValue");
        }
    }

    [Fact]
    public void Only_AboutBio_uses_sonnet()
    {
        var sonnetSpecs = FieldSpecCatalog.All
            .Where(s => s.Model == "sonnet-4-6")
            .ToList();

        sonnetSpecs.Should().ContainSingle(
            because: "only AboutSpecs.Bio should use sonnet-4-6 for cost control");
        sonnetSpecs[0].Name.Should().Be("about.bio");
    }

    [Fact]
    public void All_remaining_entries_use_haiku()
    {
        var nonBioSpecs = FieldSpecCatalog.All
            .Where(s => s.Name != "about.bio")
            .ToList();

        nonBioSpecs.Should().AllSatisfy(s =>
            s.Model.Should().Be("haiku-4-5",
                because: $"spec '{s.Name}' should use haiku-4-5 for cost efficiency"));
    }

    [Fact]
    public void Hero_specs_are_present_with_correct_names()
    {
        var names = FieldSpecCatalog.All.Select(s => s.Name).ToHashSet();
        names.Should().Contain("hero.headline");
        names.Should().Contain("hero.tagline");
        names.Should().Contain("hero.cta_text");
    }

    [Fact]
    public void About_specs_are_present_with_correct_names()
    {
        var names = FieldSpecCatalog.All.Select(s => s.Name).ToHashSet();
        names.Should().Contain("about.bio");
        names.Should().Contain("about.subtitle");
    }

    [Fact]
    public void Features_specs_are_present_with_correct_names()
    {
        var names = FieldSpecCatalog.All.Select(s => s.Name).ToHashSet();
        names.Should().Contain("features.title");
        names.Should().Contain("features.items");
    }

    [Fact]
    public void Steps_specs_are_present_with_correct_names()
    {
        var names = FieldSpecCatalog.All.Select(s => s.Name).ToHashSet();
        names.Should().Contain("steps.title");
        names.Should().Contain("steps.items");
    }

    [Fact]
    public void Testimonials_specs_are_present_with_correct_names()
    {
        var names = FieldSpecCatalog.All.Select(s => s.Name).ToHashSet();
        names.Should().Contain("testimonials.title");
    }

    [Fact]
    public void Contact_specs_are_present_with_correct_names()
    {
        var names = FieldSpecCatalog.All.Select(s => s.Name).ToHashSet();
        names.Should().Contain("contact.title");
        names.Should().Contain("contact.subtitle");
    }

    [Fact]
    public void ThankYou_specs_are_present_with_correct_names()
    {
        var names = FieldSpecCatalog.All.Select(s => s.Name).ToHashSet();
        names.Should().Contain("thankyou.headline");
        names.Should().Contain("thankyou.message");
    }

    [Fact]
    public void Nav_specs_are_present_with_correct_names()
    {
        var names = FieldSpecCatalog.All.Select(s => s.Name).ToHashSet();
        names.Should().Contain("nav.labels");
    }

    [Fact]
    public void All_entries_have_non_empty_Name()
    {
        foreach (var spec in FieldSpecCatalog.All)
        {
            spec.Name.Should().NotBeNullOrWhiteSpace(
                because: "all specs must have a non-empty Name for lookup");
        }
    }
}
