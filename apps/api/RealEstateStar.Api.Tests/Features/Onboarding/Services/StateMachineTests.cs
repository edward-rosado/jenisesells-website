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

    [Fact]
    public void CanAdvance_FromCollectBranding_ToConnectGoogle()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;
        Assert.True(_sm.CanAdvance(session, OnboardingState.ConnectGoogle));
    }

    [Fact]
    public void CanAdvance_FromConnectGoogle_ToGenerateSite()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ConnectGoogle;
        Assert.True(_sm.CanAdvance(session, OnboardingState.GenerateSite));
    }

    [Fact]
    public void CannotSkip_FromCollectBranding_ToGenerateSite()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;
        Assert.False(_sm.CanAdvance(session, OnboardingState.GenerateSite));
    }

    [Fact]
    public void GetAllowedTools_ConnectGoogle_ReturnsGoogleAuthTool()
    {
        var tools = _sm.GetAllowedTools(OnboardingState.ConnectGoogle);
        Assert.Contains("google_auth_card", tools);
        Assert.DoesNotContain("deploy_site", tools);
    }

    [Theory]
    [InlineData(OnboardingState.ScrapeProfile, OnboardingState.ConfirmIdentity)]
    [InlineData(OnboardingState.ConfirmIdentity, OnboardingState.CollectBranding)]
    [InlineData(OnboardingState.CollectBranding, OnboardingState.ConnectGoogle)]
    [InlineData(OnboardingState.ConnectGoogle, OnboardingState.GenerateSite)]
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

    // --- Missing branch coverage ---

    [Fact]
    public void CanAdvance_WithUnknownState_ReturnsFalse()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = (OnboardingState)999;
        Assert.False(_sm.CanAdvance(session, OnboardingState.ConfirmIdentity));
    }

    [Fact]
    public void CanAdvance_FromTrialActivated_ReturnsFalseForAllTargets()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.TrialActivated;
        // TrialActivated has an empty allowed array — TryGetValue succeeds but Contains returns false
        Assert.False(_sm.CanAdvance(session, OnboardingState.ScrapeProfile));
        Assert.False(_sm.CanAdvance(session, OnboardingState.ConfirmIdentity));
        Assert.False(_sm.CanAdvance(session, OnboardingState.CollectPayment));
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
        _sm.Advance(session, OnboardingState.ConfirmIdentity);
        Assert.True(session.UpdatedAt >= before);
    }

    [Theory]
    [InlineData(OnboardingState.ConfirmIdentity, "update_profile")]
    [InlineData(OnboardingState.CollectBranding, "set_branding")]
    [InlineData(OnboardingState.GenerateSite, "deploy_site")]
    [InlineData(OnboardingState.PreviewSite, "get_preview_url")]
    [InlineData(OnboardingState.DemoCma, "submit_cma_form")]
    public void GetAllowedTools_ReturnsExpectedToolForState(OnboardingState state, string expectedTool)
    {
        var tools = _sm.GetAllowedTools(state);
        Assert.Contains(expectedTool, tools);
    }

    [Theory]
    [InlineData(OnboardingState.ShowResults)]
    [InlineData(OnboardingState.TrialActivated)]
    public void GetAllowedTools_EmptyStates_ReturnsEmpty(OnboardingState state)
    {
        var tools = _sm.GetAllowedTools(state);
        Assert.Empty(tools);
    }
}
