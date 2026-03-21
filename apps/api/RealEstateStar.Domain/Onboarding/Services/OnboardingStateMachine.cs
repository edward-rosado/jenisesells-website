using RealEstateStar.Domain.Onboarding.Models;
namespace RealEstateStar.Domain.Onboarding.Services;

public class OnboardingStateMachine
{
    private static readonly Dictionary<OnboardingState, OnboardingState[]> Transitions = new()
    {
        [OnboardingState.ScrapeProfile] = [OnboardingState.GenerateSite],
        [OnboardingState.GenerateSite] = [OnboardingState.ConnectGoogle],
        [OnboardingState.ConnectGoogle] = [OnboardingState.DemoCma],
        [OnboardingState.DemoCma] = [OnboardingState.ShowResults],
        [OnboardingState.ShowResults] = [OnboardingState.CollectPayment],
        [OnboardingState.CollectPayment] = [OnboardingState.TrialActivated],
        [OnboardingState.TrialActivated] = [],
    };

    private static readonly Dictionary<OnboardingState, string[]> ToolsByState = new()
    {
        [OnboardingState.ScrapeProfile] = ["scrape_url", "update_profile"],
        [OnboardingState.GenerateSite] = ["deploy_site"],
        [OnboardingState.ConnectGoogle] = ["google_auth_card"],
        [OnboardingState.DemoCma] = ["submit_cma_form"],
        [OnboardingState.ShowResults] = [],
        [OnboardingState.CollectPayment] = ["create_stripe_session"],
        [OnboardingState.TrialActivated] = [],
    };

    public bool CanAdvance(OnboardingSession session, OnboardingState targetState)
        => Transitions.TryGetValue(session.CurrentState, out var allowed)
           && allowed.Contains(targetState);

    public void Advance(OnboardingSession session, OnboardingState targetState)
    {
        if (!CanAdvance(session, targetState))
            throw new InvalidOperationException(
                $"Cannot transition from {session.CurrentState} to {targetState}");

        OnboardingDiagnostics.StateTransitions.Add(1,
            new KeyValuePair<string, object?>("from_state", session.CurrentState.ToString()),
            new KeyValuePair<string, object?>("to_state", targetState.ToString()));
        session.CurrentState = targetState;
        session.UpdatedAt = DateTime.UtcNow;
    }

    public string[] GetAllowedTools(OnboardingState state)
        => ToolsByState.GetValueOrDefault(state, []);
}
