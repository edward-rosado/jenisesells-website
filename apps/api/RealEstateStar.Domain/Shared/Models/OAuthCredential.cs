namespace RealEstateStar.Domain.Shared.Models;

public sealed record OAuthCredential
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required string[] Scopes { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
    /// <summary>Account partition ID. Null during in-session onboarding; populated when stored durably.</summary>
    public string? AccountId { get; init; }
    /// <summary>Agent identifier. Null during in-session onboarding; populated when stored durably.</summary>
    public string? AgentId { get; init; }
    /// <summary>Azure Table ETag for optimistic concurrency. Populated by ITokenStore on read.</summary>
    public string? ETag { get; init; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
}
