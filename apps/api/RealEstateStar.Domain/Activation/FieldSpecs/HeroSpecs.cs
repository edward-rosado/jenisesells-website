using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.FieldSpecs;

/// <summary>
/// Field specs for the Hero section of the agent site.
/// </summary>
public static class HeroSpecs
{
    public static FieldSpec<string> Headline => new(
        Name: "hero.headline",
        PromptTemplate: "Write a short, compelling hero headline (max 10 words) for {{Facts.Agent.Name}}, a {{Facts.Agent.Title}} specializing in {{Facts.Specialties.Specialties[0]}} in {{Facts.Location.ServiceAreas[0]}}. Use the agent's voice and personality. Do NOT include the agent's name in the headline.",
        MaxOutputTokens: 50,
        Model: "haiku-4-5",
        FallbackValue: "Your Dream Home Awaits");

    public static FieldSpec<string> Tagline => new(
        Name: "hero.tagline",
        PromptTemplate: "Write a 1-2 sentence tagline for {{Facts.Agent.Name}}'s real estate website. Highlight their {{Facts.Trust.TransactionCount}} transactions and expertise in {{Facts.Location.State}}. Match their voice personality.",
        MaxOutputTokens: 100,
        Model: "haiku-4-5",
        FallbackValue: "Dedicated to finding the perfect property for you.");

    public static FieldSpec<string> CtaText => new(
        Name: "hero.cta_text",
        PromptTemplate: "Write a short call-to-action button text (2-5 words) for a real estate agent's website hero section. Should invite visitors to get started or make contact.",
        MaxOutputTokens: 20,
        Model: "haiku-4-5",
        FallbackValue: "Get Started Today");
}
