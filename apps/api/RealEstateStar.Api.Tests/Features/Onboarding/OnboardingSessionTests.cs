using RealEstateStar.Api.Features.Onboarding;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding;

public class OnboardingSessionTests
{
    [Fact]
    public void NewSession_StartsInScrapeProfileState()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test-agent");
        Assert.Equal(OnboardingState.ScrapeProfile, session.CurrentState);
    }

    [Fact]
    public void NewSession_HasUniqueId()
    {
        var s1 = OnboardingSession.Create("https://zillow.com/profile/a");
        var s2 = OnboardingSession.Create("https://zillow.com/profile/b");
        Assert.NotEqual(s1.Id, s2.Id);
    }

    [Fact]
    public void NewSession_StoresProfileUrl()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        Assert.Equal("https://zillow.com/profile/test", session.ProfileUrl);
    }

    [Fact]
    public void NewSession_WithoutUrl_StartsInScrapeProfileState()
    {
        var session = OnboardingSession.Create(null);
        Assert.Equal(OnboardingState.ScrapeProfile, session.CurrentState);
        Assert.Null(session.ProfileUrl);
    }

    [Fact]
    public void NewSession_HasEmptyMessageHistory()
    {
        var session = OnboardingSession.Create(null);
        Assert.Empty(session.Messages);
    }
}
