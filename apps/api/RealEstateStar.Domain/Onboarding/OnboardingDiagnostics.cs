using System.Diagnostics.Metrics;

namespace RealEstateStar.Domain.Onboarding;

public static class OnboardingDiagnostics
{
    public const string ServiceName = "RealEstateStar.Onboarding";

    private static readonly Meter Meter = new(ServiceName);

    public static readonly Counter<long> StateTransitions = Meter.CreateCounter<long>("onboarding.state_transitions");

    public static readonly Counter<long> LlmTokensInput = Meter.CreateCounter<long>(
        "onboarding.llm_tokens.input", description: "LLM input tokens consumed for onboarding");

    public static readonly Counter<long> LlmTokensOutput = Meter.CreateCounter<long>(
        "onboarding.llm_tokens.output", description: "LLM output tokens consumed for onboarding");
}
