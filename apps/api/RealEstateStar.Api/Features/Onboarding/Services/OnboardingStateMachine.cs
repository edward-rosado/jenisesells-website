namespace RealEstateStar.Api.Features.Onboarding.Services;

public class OnboardingStateMachine
{
    private static readonly Dictionary<OnboardingState, OnboardingState[]> Transitions = new()
    {
        [OnboardingState.ScrapeProfile] = [OnboardingState.ConfirmIdentity],
        [OnboardingState.ConfirmIdentity] = [OnboardingState.CollectBranding],
        [OnboardingState.CollectBranding] = [OnboardingState.GenerateSite],
        [OnboardingState.GenerateSite] = [OnboardingState.PreviewSite],
        [OnboardingState.PreviewSite] = [OnboardingState.DemoCma],
        [OnboardingState.DemoCma] = [OnboardingState.ShowResults],
        [OnboardingState.ShowResults] = [OnboardingState.CollectPayment],
        [OnboardingState.CollectPayment] = [OnboardingState.TrialActivated],
        [OnboardingState.TrialActivated] = [],
    };

    private static readonly Dictionary<OnboardingState, string[]> ToolsByState = new()
    {
        [OnboardingState.ScrapeProfile] = ["scrape_url", "update_profile"],
        [OnboardingState.ConfirmIdentity] = ["update_profile"],
        [OnboardingState.CollectBranding] = ["extract_colors", "set_branding"],
        [OnboardingState.GenerateSite] = ["deploy_site"],
        [OnboardingState.PreviewSite] = ["get_preview_url"],
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

        session.CurrentState = targetState;
        session.UpdatedAt = DateTime.UtcNow;
    }

    public string[] GetAllowedTools(OnboardingState state)
        => ToolsByState.GetValueOrDefault(state, []);
}
