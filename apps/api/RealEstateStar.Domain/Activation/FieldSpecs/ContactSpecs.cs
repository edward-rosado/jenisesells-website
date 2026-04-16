using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.FieldSpecs;

/// <summary>
/// Field specs for the Contact/CTA section of the agent site.
/// </summary>
public static class ContactSpecs
{
    public static FieldSpec<string> Title => new(
        Name: "contact.title",
        PromptTemplate: "Write a section title (3-6 words) for the contact/CTA section of a real estate agent's website.",
        MaxOutputTokens: 20,
        Model: "haiku-4-5",
        FallbackValue: "Let's Connect");

    public static FieldSpec<string> Subtitle => new(
        Name: "contact.subtitle",
        PromptTemplate: "Write a short subtitle (10-20 words) for the contact section. Encourage visitors to reach out to {{Facts.Agent.Name}} about buying or selling in {{Facts.Location.ServiceAreas[0]}}.",
        MaxOutputTokens: 50,
        Model: "haiku-4-5",
        FallbackValue: "Ready to start your real estate journey? Get in touch today.");
}
