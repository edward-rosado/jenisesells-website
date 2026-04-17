namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Opaque preview session stored in Azure Table. 5 fields per R6-12.
/// Created via one-time exchange token, validated via HttpOnly cookie.
/// </summary>
public sealed record PreviewSession(
    string SessionId,
    string AccountId,
    DateTime ExpiresAt,
    bool Revoked,
    DateTime? RevokedAt);
