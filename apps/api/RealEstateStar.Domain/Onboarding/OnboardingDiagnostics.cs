using System.Diagnostics.Metrics;

namespace RealEstateStar.Domain.Onboarding;

public static class OnboardingDiagnostics
{
    public const string ServiceName = "RealEstateStar.Onboarding";

    private static readonly Meter Meter = new(ServiceName);

    public static readonly Counter<long> StateTransitions = Meter.CreateCounter<long>("onboarding.state_transitions");
}
