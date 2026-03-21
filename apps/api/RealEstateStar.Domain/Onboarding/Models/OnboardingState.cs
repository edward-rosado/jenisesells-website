using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Onboarding.Models;

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
