using Xunit;
using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Markdown;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Markdown;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Services;
using RealEstateStar.Domain.HomeSearch.Markdown;
using RealEstateStar.Domain.WhatsApp.Models;
using RealEstateStar.Domain.WhatsApp;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.Domain.Onboarding;
namespace RealEstateStar.Domain.Tests.Onboarding;

// TODO: LOW-8 — Add boundary test for exactly 5-minute expiry buffer
public class OAuthCredentialTests
{
    [Fact]
    public void IsExpired_WhenPastExpiry_ReturnsTrue()
    {
        var credential = new OAuthCredential
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            Scopes = ["gmail.send"],
            Email = "test@gmail.com",
            Name = "Test User",
        };

        Assert.True(credential.IsExpired);
    }

    [Fact]
    public void IsExpired_WhenBeforeExpiry_ReturnsFalse()
    {
        var credential = new OAuthCredential
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["gmail.send"],
            Email = "test@gmail.com",
            Name = "Test User",
        };

        Assert.False(credential.IsExpired);
    }

    [Fact]
    public void IsExpired_WhenWithin5MinBuffer_ReturnsTrue()
    {
        var credential = new OAuthCredential
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(3),
            Scopes = ["gmail.send"],
            Email = "test@gmail.com",
            Name = "Test User",
        };

        Assert.True(credential.IsExpired);
    }
}
