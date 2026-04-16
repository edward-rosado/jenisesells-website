using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.FieldSpecs;

/// <summary>
/// Field specs for the Testimonials section of the agent site.
/// </summary>
public static class TestimonialsSpecs
{
    public static FieldSpec<string> Title => new(
        Name: "testimonials.title",
        PromptTemplate: "Write a section title (3-6 words) for the testimonials/reviews section. Agent has {{Facts.Trust.ReviewCount}} reviews with {{Facts.Trust.AverageRating}} average rating.",
        MaxOutputTokens: 20,
        Model: "haiku-4-5",
        FallbackValue: "What Clients Say");
}
