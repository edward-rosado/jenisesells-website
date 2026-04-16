using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Activation.Interfaces;

namespace RealEstateStar.Api.Features.Preview.RevokeSession;

/// <summary>
/// DELETE /preview-sessions/{sessionId}
///
/// Revokes a preview session. Idempotent — revoking an already-revoked session
/// returns 200. Returns 404 if the session does not exist.
/// Clears the session cookie.
/// </summary>
public class RevokeSessionEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapDelete("/preview-sessions/{sessionId}", Handle);

    internal static async Task<IResult> Handle(
        string sessionId,
        IPreviewSessionStore sessionStore,
        HttpContext httpContext,
        ILogger<RevokeSessionEndpoint> logger,
        CancellationToken ct)
    {
        try
        {
            await sessionStore.RevokeAsync(sessionId, ct);
        }
        catch (KeyNotFoundException)
        {
            logger.LogWarning("[PREVIEW-021] Revoke requested for unknown sessionId={SessionId}", sessionId);
            return Results.NotFound(new { error = "Session not found." });
        }

        // Clear the cookie on the client side
        httpContext.Response.Cookies.Delete(PreviewSessionValidator.CookieName);

        logger.LogInformation("[PREVIEW-020] Session revoked. sessionId={SessionId}", sessionId);
        return Results.Ok(new { status = "revoked" });
    }
}
