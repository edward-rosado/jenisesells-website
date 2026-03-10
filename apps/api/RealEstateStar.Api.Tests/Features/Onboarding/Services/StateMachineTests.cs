using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class StateMachineTests
{
    private readonly OnboardingStateMachine _sm = new();

    [Fact]
    public void CanAdvance_FromScrapeProfile_ToConfirmIdentity()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        Assert.True(_sm.CanAdvance(session, OnboardingState.ConfirmIdentity));
    }

    [Fact]
    public void CannotSkip_FromScrapeProfile_ToGenerateSite()
    {
        var session = OnboardingSession.Create(null);
        Assert.False(_sm.CanAdvance(session, OnboardingState.GenerateSite));
    }

    [Fact]
    public void Advance_UpdatesCurrentState()
    {
        var session = OnboardingSession.Create(null);
        _sm.Advance(session, OnboardingState.ConfirmIdentity);
        Assert.Equal(OnboardingState.ConfirmIdentity, session.CurrentState);
    }

    [Fact]
    public void Advance_ToInvalidState_Throws()
    {
        var session = OnboardingSession.Create(null);
        Assert.Throws<InvalidOperationException>(
            () => _sm.Advance(session, OnboardingState.CollectPayment));
    }

    [Fact]
    public void GetAllowedTools_ScrapeProfile_ReturnsScrapeTools()
    {
        var tools = _sm.GetAllowedTools(OnboardingState.ScrapeProfile);
        Assert.Contains("scrape_url", tools);
        Assert.DoesNotContain("deploy_site", tools);
    }

    [Fact]
    public void GetAllowedTools_CollectPayment_ReturnsStripeTools()
    {
        var tools = _sm.GetAllowedTools(OnboardingState.CollectPayment);
        Assert.Contains("create_stripe_session", tools);
        Assert.DoesNotContain("scrape_url", tools);
    }

    [Theory]
    [InlineData(OnboardingState.ScrapeProfile, OnboardingState.ConfirmIdentity)]
    [InlineData(OnboardingState.ConfirmIdentity, OnboardingState.CollectBranding)]
    [InlineData(OnboardingState.CollectBranding, OnboardingState.GenerateSite)]
    [InlineData(OnboardingState.GenerateSite, OnboardingState.PreviewSite)]
    [InlineData(OnboardingState.PreviewSite, OnboardingState.DemoCma)]
    [InlineData(OnboardingState.DemoCma, OnboardingState.ShowResults)]
    [InlineData(OnboardingState.ShowResults, OnboardingState.CollectPayment)]
    [InlineData(OnboardingState.CollectPayment, OnboardingState.TrialActivated)]
    public void AllTransitions_AreValid(OnboardingState from, OnboardingState to)
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = from;
        Assert.True(_sm.CanAdvance(session, to));
    }
}
