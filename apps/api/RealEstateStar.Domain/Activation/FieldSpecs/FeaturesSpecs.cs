using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.FieldSpecs;

/// <summary>
/// Field specs for the Services/Features section of the agent site.
/// </summary>
public static class FeaturesSpecs
{
    public static FieldSpec<string> Title => new(
        Name: "features.title",
        PromptTemplate: "Write a section title (3-6 words) for the services/features section of a real estate agent's website.",
        MaxOutputTokens: 20,
        Model: "haiku-4-5",
        FallbackValue: "How I Can Help");

    public static FieldSpec<string> Items => new(
        Name: "features.items",
        PromptTemplate: "Generate 4 service descriptions for {{Facts.Agent.Name}}'s real estate website. Each should be a JSON array of objects with 'title' (3-5 words) and 'description' (1-2 sentences). Base on their specialties: {{Facts.Specialties.Specialties}} and service areas: {{Facts.Location.ServiceAreas}}.",
        MaxOutputTokens: 500,
        Model: "haiku-4-5",
        FallbackValue: "[]");
}
