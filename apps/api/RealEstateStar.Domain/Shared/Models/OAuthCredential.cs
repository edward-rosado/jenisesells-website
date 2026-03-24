namespace RealEstateStar.Domain.Shared.Models;

public sealed record OAuthCredential
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required string[] Scopes { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
    public string? AccountId { get; init; }
    public string? AgentId { get; init; }
    public string? ETag { get; init; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
}
