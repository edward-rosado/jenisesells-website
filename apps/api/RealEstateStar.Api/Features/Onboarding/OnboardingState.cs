using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.Onboarding;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OnboardingState
{
    ScrapeProfile,
    ConfirmIdentity,
    CollectBranding,
    GenerateSite,
    PreviewSite,
    DemoCma,
    ShowResults,
    CollectPayment,
    TrialActivated
}
