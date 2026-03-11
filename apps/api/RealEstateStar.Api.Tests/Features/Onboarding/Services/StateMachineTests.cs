using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class StateMachineTests
{
    private readonly OnboardingStateMachine _sm = new();

    // --- Valid transitions ---

    [Theory]
    [InlineData(OnboardingState.ScrapeProfile, OnboardingState.GenerateSite)]
    [InlineData(OnboardingState.GenerateSite, OnboardingState.ConnectGoogle)]
    [InlineData(OnboardingState.ConnectGoogle, OnboardingState.DemoCma)]
    [InlineData(OnboardingState.DemoCma, OnboardingState.ShowResults)]
    [InlineData(OnboardingState.ShowResults, OnboardingState.CollectPayment)]
    [InlineData(OnboardingState.CollectPayment, OnboardingState.TrialActivated)]
    public void AllTransitions_AreValid(OnboardingState from, OnboardingState to)
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = from;
        Assert.True(_sm.CanAdvance(session, to));
    }

    [Theory]
    [InlineData(OnboardingState.ScrapeProfile, OnboardingState.GenerateSite)]
    [InlineData(OnboardingState.GenerateSite, OnboardingState.ConnectGoogle)]
    [InlineData(OnboardingState.ConnectGoogle, OnboardingState.DemoCma)]
    [InlineData(OnboardingState.DemoCma, OnboardingState.ShowResults)]
    [InlineData(OnboardingState.ShowResults, OnboardingState.CollectPayment)]
    [InlineData(OnboardingState.CollectPayment, OnboardingState.TrialActivated)]
    public void Advance_UpdatesCurrentState(OnboardingState from, OnboardingState to)
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = from;
        _sm.Advance(session, to);
        Assert.Equal(to, session.CurrentState);
    }

    // --- Invalid transitions ---

    [Fact]
    public void CannotSkip_FromScrapeProfile_ToDemoCma()
    {
        var session = OnboardingSession.Create(null);
        Assert.False(_sm.CanAdvance(session, OnboardingState.DemoCma));
    }

    [Fact]
    public void CannotSkip_FromGenerateSite_ToDemoCma()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.GenerateSite;
        Assert.False(_sm.CanAdvance(session, OnboardingState.DemoCma));
    }

    [Fact]
    public void Advance_ToInvalidState_Throws()
    {
        var session = OnboardingSession.Create(null);
        Assert.Throws<InvalidOperationException>(
            () => _sm.Advance(session, OnboardingState.CollectPayment));
    }

    // --- Terminal state ---

    [Fact]
    public void CanAdvance_FromTrialActivated_ReturnsFalseForAllTargets()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.TrialActivated;
        Assert.False(_sm.CanAdvance(session, OnboardingState.ScrapeProfile));
        Assert.False(_sm.CanAdvance(session, OnboardingState.GenerateSite));
        Assert.False(_sm.CanAdvance(session, OnboardingState.CollectPayment));
    }

    // --- Tools by state ---

    [Fact]
    public void GetAllowedTools_ScrapeProfile_ReturnsScrapeTools()
    {
        var tools = _sm.GetAllowedTools(OnboardingState.ScrapeProfile);
        Assert.Contains("scrape_url", tools);
        Assert.Contains("update_profile", tools);
        Assert.DoesNotContain("deploy_site", tools);
    }

    [Fact]
    public void GetAllowedTools_GenerateSite_ReturnsDeployTool()
    {
        var tools = _sm.GetAllowedTools(OnboardingState.GenerateSite);
        Assert.Contains("deploy_site", tools);
    }

    [Fact]
    public void GetAllowedTools_ConnectGoogle_ReturnsGoogleAuthTool()
    {
        var tools = _sm.GetAllowedTools(OnboardingState.ConnectGoogle);
        Assert.Contains("google_auth_card", tools);
        Assert.DoesNotContain("deploy_site", tools);
    }

    [Fact]
    public void GetAllowedTools_DemoCma_ReturnsCmaTool()
    {
        var tools = _sm.GetAllowedTools(OnboardingState.DemoCma);
        Assert.Contains("submit_cma_form", tools);
    }

    [Fact]
    public void GetAllowedTools_CollectPayment_ReturnsStripeTools()
    {
        var tools = _sm.GetAllowedTools(OnboardingState.CollectPayment);
        Assert.Contains("create_stripe_session", tools);
        Assert.DoesNotContain("scrape_url", tools);
    }

    [Theory]
    [InlineData(OnboardingState.ShowResults)]
    [InlineData(OnboardingState.TrialActivated)]
    public void GetAllowedTools_EmptyStates_ReturnsEmpty(OnboardingState state)
    {
        var tools = _sm.GetAllowedTools(state);
        Assert.Empty(tools);
    }

    // --- Edge cases ---

    [Fact]
    public void CanAdvance_WithUnknownState_ReturnsFalse()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = (OnboardingState)999;
        Assert.False(_sm.CanAdvance(session, OnboardingState.GenerateSite));
    }

    [Fact]
    public void GetAllowedTools_WithUnknownState_ReturnsEmpty()
    {
        var tools = _sm.GetAllowedTools((OnboardingState)999);
        Assert.Empty(tools);
    }

    [Fact]
    public void Advance_UpdatesUpdatedAtTimestamp()
    {
        var session = OnboardingSession.Create(null);
        var before = DateTime.UtcNow;
        _sm.Advance(session, OnboardingState.GenerateSite);
        Assert.True(session.UpdatedAt >= before);
    }
}
