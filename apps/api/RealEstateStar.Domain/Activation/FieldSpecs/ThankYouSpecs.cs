using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.FieldSpecs;

/// <summary>
/// Field specs for the Thank You page of the agent site.
/// </summary>
public static class ThankYouSpecs
{
    public static FieldSpec<string> Headline => new(
        Name: "thankyou.headline",
        PromptTemplate: "Write a thank-you page headline (3-8 words) for after a visitor submits a contact form on a real estate agent's website.",
        MaxOutputTokens: 30,
        Model: "haiku-4-5",
        FallbackValue: "Thank You!");

    public static FieldSpec<string> Message => new(
        Name: "thankyou.message",
        PromptTemplate: "Write a brief thank-you message (2-3 sentences) for {{Facts.Agent.Name}}'s website. Mention typical response time. Use their voice.",
        MaxOutputTokens: 100,
        Model: "haiku-4-5",
        FallbackValue: "We've received your message and will get back to you shortly.");
}
