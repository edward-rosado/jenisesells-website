using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.Onboarding;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OnboardingState
{
    ScrapeProfile,
    ConfirmIdentity,
    CollectBranding,
    ConnectGoogle,
    GenerateSite,
    PreviewSite,
    DemoCma,
    ShowResults,
    CollectPayment,
    TrialActivated
}
