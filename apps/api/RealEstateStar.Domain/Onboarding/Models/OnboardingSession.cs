using System.Security.Cryptography;

namespace RealEstateStar.Domain.Onboarding.Models;

public sealed class OnboardingSession
{
    public required string Id { get; init; }
    public required string BearerToken { get; init; }
    public OnboardingState CurrentState { get; set; } = OnboardingState.ScrapeProfile;
    public string? ProfileUrl { get; init; }
    public ScrapedProfile? Profile { get; set; }
    public GoogleTokens? GoogleTokens { get; set; }
    public List<ChatMessage> Messages { get; init; } = [];
    public string? AgentConfigId { get; set; }
    public string? StripeSetupIntentId { get; set; }
    public string? SiteUrl { get; set; }
    public string? CustomDomain { get; set; }
    public string? OAuthNonce { get; set; }
    public string? LastStripeEventId { get; set; }
    public bool DriveFolderInitialized { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // LOW-1: 48-bit session ID is acceptable with bearer token auth (SEC-1). Consider upgrading to 128-bit if auth is ever removed.
    public static OnboardingSession Create(string? profileUrl) => new()
    {
        Id = Guid.NewGuid().ToString("N")[..12],
        BearerToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
        ProfileUrl = profileUrl
    };
}
