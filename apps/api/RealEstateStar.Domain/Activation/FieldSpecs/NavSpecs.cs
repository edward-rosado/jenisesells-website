using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.FieldSpecs;

/// <summary>
/// Field specs for site navigation labels.
/// </summary>
public static class NavSpecs
{
    public static FieldSpec<string> Labels => new(
        Name: "nav.labels",
        PromptTemplate: "Generate navigation labels as a JSON object with keys: home, about, services, contact, cma. Values should be 1-2 word labels in the target locale. Keep them short for mobile nav.",
        MaxOutputTokens: 100,
        Model: "haiku-4-5",
        FallbackValue: """{"home":"Home","about":"About","services":"Services","contact":"Contact","cma":"Free CMA"}""");
}
