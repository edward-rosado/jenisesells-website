using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.Interfaces;

/// <summary>
/// CRUD store for preview sessions persisted in Azure Table Storage.
/// Sessions are created via one-time exchange token and validated via HttpOnly cookie.
/// </summary>
public interface IPreviewSessionStore
{
    /// <summary>
    /// Creates a new preview session. Throws <see cref="InvalidOperationException"/> if a session
    /// with the same sessionId already exists (idempotency guard — exchange tokens are single-use).
    /// </summary>
    Task CreateAsync(PreviewSession session, CancellationToken ct);

    /// <summary>Returns the session or <c>null</c> if not found.</summary>
    Task<PreviewSession?> GetAsync(string sessionId, CancellationToken ct);

    /// <summary>
    /// Marks the session as revoked. Idempotent — calling on an already-revoked session succeeds.
    /// Throws if the session does not exist.
    /// </summary>
    Task RevokeAsync(string sessionId, CancellationToken ct);

    /// <summary>
    /// Slides the session expiry forward by 24 hours, subject to a hard cap of 30 days from
    /// the original <see cref="PreviewSession.ExpiresAt"/> stored at creation time.
    /// Returns the updated session.
    /// </summary>
    Task<PreviewSession> RefreshExpiryAsync(string sessionId, DateTime createdAt, CancellationToken ct);
}
