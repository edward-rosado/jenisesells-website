using RealEstateStar.Domain.Shared.Models;
using Xunit;

namespace RealEstateStar.Domain.Tests.Onboarding;

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

    [Fact]
    public void GoogleTokens_DefaultsToNull()
    {
        var session = OnboardingSession.Create(null);
        Assert.Null(session.GoogleTokens);
    }

    [Fact]
    public void GoogleTokens_CanBeSet()
    {
        var session = OnboardingSession.Create(null);
        session.GoogleTokens = new OAuthCredential
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["gmail.send"],
            Email = "test@gmail.com",
            Name = "Test User",
        };

        Assert.NotNull(session.GoogleTokens);
        Assert.Equal("test@gmail.com", session.GoogleTokens.Email);
    }
}
