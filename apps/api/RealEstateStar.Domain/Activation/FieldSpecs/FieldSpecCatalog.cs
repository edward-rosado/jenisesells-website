using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.FieldSpecs;

/// <summary>
/// Central registry of all FieldSpecs used to generate voiced content for agent sites.
/// Pure data — enumerate once, pass to VoicedContentGenerator.
/// </summary>
public static class FieldSpecCatalog
{
    public static IReadOnlyList<FieldSpec<string>> All =>
    [
        HeroSpecs.Headline, HeroSpecs.Tagline, HeroSpecs.CtaText,
        AboutSpecs.Bio, AboutSpecs.Subtitle,
        FeaturesSpecs.Title, FeaturesSpecs.Items,
        StepsSpecs.Title, StepsSpecs.Items,
        TestimonialsSpecs.Title,
        ContactSpecs.Title, ContactSpecs.Subtitle,
        ThankYouSpecs.Headline, ThankYouSpecs.Message,
        NavSpecs.Labels,
    ];
}
