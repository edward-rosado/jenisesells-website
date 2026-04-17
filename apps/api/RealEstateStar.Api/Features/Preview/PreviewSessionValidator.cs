using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Api.Features.Preview;

/// <summary>
/// Shared helper for validating preview sessions from cookie or Authorization header.
/// Used by Sites endpoints (Approve, GetState) to authenticate preview access.
/// </summary>
internal static class PreviewSessionValidator
{
    /// <summary>The cookie name shared with ExchangeTokenEndpoint.</summary>
    internal const string CookieName = "preview_session";
    /// <summary>
    /// Resolves the session ID from the request (cookie takes precedence, then header).
    /// Loads the session and validates: exists, not revoked, not expired, accountId matches.
    /// Returns (session, null) on success or (null, errorResult) on failure.
    /// </summary>
    internal static async Task<(PreviewSession? Session, IResult? Error)> ValidateAsync(
        string accountId,
        HttpContext httpContext,
        IPreviewSessionStore sessionStore,
        ILogger logger,
        CancellationToken ct)
    {
        var sessionId = ResolveSessionId(httpContext);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return (null, Results.Unauthorized());
        }

        PreviewSession? session;
        try
        {
            session = await sessionStore.GetAsync(sessionId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PREVIEW-080] Error loading session sessionId={SessionId}", sessionId);
            return (null, Results.Problem("Session lookup failed.", statusCode: 500));
        }

        if (session is null)
        {
            return (null, Results.Unauthorized());
        }

        if (session.Revoked)
        {
            return (null, Results.Unauthorized());
        }

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            return (null, Results.Unauthorized());
        }

        if (!string.Equals(session.AccountId, accountId, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[PREVIEW-081] Session accountId mismatch. sessionAccountId={SessionAccountId} requestAccountId={RequestAccountId}",
                session.AccountId, accountId);
            return (null, Results.Forbid());
        }

        return (session, null);
    }

    /// <summary>
    /// Reads the session ID from the "preview_session" cookie or the
    /// "X-Preview-Session" header (fallback for non-browser clients).
    /// </summary>
    private static string? ResolveSessionId(HttpContext httpContext)
    {
        if (httpContext.Request.Cookies.TryGetValue(CookieName, out var cookie)
            && !string.IsNullOrWhiteSpace(cookie))
            return cookie;

        if (httpContext.Request.Headers.TryGetValue("X-Preview-Session", out var header)
            && !string.IsNullOrWhiteSpace(header))
            return header.ToString();

        return null;
    }
}
