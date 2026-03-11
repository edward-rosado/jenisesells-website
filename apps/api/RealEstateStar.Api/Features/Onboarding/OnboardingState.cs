using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.Onboarding;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OnboardingState
{
    ScrapeProfile,
    GenerateSite,
    ConnectGoogle,
    DemoCma,
    ShowResults,
    CollectPayment,
    TrialActivated
}
