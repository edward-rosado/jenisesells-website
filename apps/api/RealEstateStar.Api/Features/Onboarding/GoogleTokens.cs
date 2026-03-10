namespace RealEstateStar.Api.Features.Onboarding;

public sealed record GoogleTokens
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAt { get; set; }
    public required string[] Scopes { get; init; }
    public required string GoogleEmail { get; init; }
    public required string GoogleName { get; init; }

    /// <summary>
    /// Returns true if the access token is expired or will expire within 5 minutes.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
}
