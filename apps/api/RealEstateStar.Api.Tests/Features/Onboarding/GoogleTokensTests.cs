using RealEstateStar.Api.Features.Onboarding;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding;

public class GoogleTokensTests
{
    [Fact]
    public void IsExpired_WhenPastExpiry_ReturnsTrue()
    {
        var tokens = new GoogleTokens
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            Scopes = ["gmail.send"],
            GoogleEmail = "test@gmail.com",
            GoogleName = "Test User",
        };

        Assert.True(tokens.IsExpired);
    }

    [Fact]
    public void IsExpired_WhenBeforeExpiry_ReturnsFalse()
    {
        var tokens = new GoogleTokens
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["gmail.send"],
            GoogleEmail = "test@gmail.com",
            GoogleName = "Test User",
        };

        Assert.False(tokens.IsExpired);
    }

    [Fact]
    public void IsExpired_WhenWithin5MinBuffer_ReturnsTrue()
    {
        var tokens = new GoogleTokens
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(3),
            Scopes = ["gmail.send"],
            GoogleEmail = "test@gmail.com",
            GoogleName = "Test User",
        };

        Assert.True(tokens.IsExpired);
    }
}
