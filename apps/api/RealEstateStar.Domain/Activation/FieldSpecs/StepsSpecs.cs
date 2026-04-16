using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.FieldSpecs;

/// <summary>
/// Field specs for the Process/Steps section of the agent site.
/// </summary>
public static class StepsSpecs
{
    public static FieldSpec<string> Title => new(
        Name: "steps.title",
        PromptTemplate: "Write a section title (3-6 words) for the 'how it works' or process steps section of a real estate agent's website.",
        MaxOutputTokens: 20,
        Model: "haiku-4-5",
        FallbackValue: "Your Journey Home");

    public static FieldSpec<string> Items => new(
        Name: "steps.items",
        PromptTemplate: "Generate 4-5 process steps for working with {{Facts.Agent.Name}} as a JSON array of objects with 'title' and 'description'. Steps should reflect their actual process from {{Facts.Stages.StageNames}}.",
        MaxOutputTokens: 500,
        Model: "haiku-4-5",
        FallbackValue: "[]");
}
