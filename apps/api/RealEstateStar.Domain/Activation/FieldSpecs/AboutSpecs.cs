using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.FieldSpecs;

/// <summary>
/// Field specs for the About section of the agent site.
/// </summary>
public static class AboutSpecs
{
    public static FieldSpec<string> Bio => new(
        Name: "about.bio",
        PromptTemplate: "Write a professional bio (150-250 words) for {{Facts.Agent.Name}}, a {{Facts.Agent.Title}} with {{Facts.Agent.YearsExperience}} years of experience at {{Facts.Brokerage.Name}}. They serve {{Facts.Location.ServiceAreas}} in {{Facts.Location.State}}. Specialties: {{Facts.Specialties.Specialties}}. Use their authentic voice. Include their credentials: {{Facts.Credentials}}.",
        MaxOutputTokens: 400,
        Model: "sonnet-4-6",
        FallbackValue: "A dedicated real estate professional committed to exceptional service.");

    public static FieldSpec<string> Subtitle => new(
        Name: "about.subtitle",
        PromptTemplate: "Write a short subtitle (5-10 words) for the About section of {{Facts.Agent.Name}}'s website. Should convey expertise and approachability.",
        MaxOutputTokens: 30,
        Model: "haiku-4-5",
        FallbackValue: "Get to Know Your Agent");
}
