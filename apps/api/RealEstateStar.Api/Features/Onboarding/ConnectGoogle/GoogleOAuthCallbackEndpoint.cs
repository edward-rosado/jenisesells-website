using System.Security.Cryptography;
using System.Web;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Onboarding.ConnectGoogle;

public class GoogleOAuthCallbackEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapGet("/oauth/google/callback", Handle);
    }

    internal static async Task<IResult> Handle(
        string? code,
        string state,
        string? error,
        ISessionStore sessionStore,
        GoogleOAuthService oAuthService,
        OnboardingStateMachine stateMachine,
        IConfiguration configuration,
        ILogger<GoogleOAuthCallbackEndpoint> logger,
        CancellationToken ct)
    {
        var platformOrigin = configuration["Platform:BaseUrl"] ?? "http://localhost:3000";

        if (error is not null)
            return Results.Content(BuildCallbackHtml(false, "Google authorization denied", platformOrigin), "text/html");

        if (code is null)
            return Results.Content(BuildCallbackHtml(false, "No authorization code received", platformOrigin), "text/html");

        // Parse state as sessionId:nonce
        var parts = state.Split(':', 2);
        if (parts.Length != 2)
            return Results.Content(BuildCallbackHtml(false, "Invalid OAuth state", platformOrigin), "text/html");

        var sessionId = parts[0];
        var nonce = parts[1];

        var session = await sessionStore.LoadAsync(sessionId, ct);
        if (session is null)
            return Results.Content(BuildCallbackHtml(false, "Session not found", platformOrigin), "text/html");

        // SEC-4: Verify CSRF nonce
        if (session.OAuthNonce is null || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(nonce),
                System.Text.Encoding.UTF8.GetBytes(session.OAuthNonce)))
        {
            return Results.Content(BuildCallbackHtml(false, "Invalid OAuth state", platformOrigin), "text/html");
        }

        // Clear nonce after use (single-use)
        session.OAuthNonce = null;

        try
        {
            var tokens = await oAuthService.ExchangeCodeAsync(code, ct);
            session.GoogleTokens = tokens;
            // TODO: MED-2 — Consider cross-validating Google email matches session profile email
            stateMachine.Advance(session, OnboardingState.DemoCma);
            await sessionStore.SaveAsync(session, ct);

            return Results.Content(
                BuildCallbackHtml(true, $"Connected as {tokens.GoogleName} ({tokens.GoogleEmail})", platformOrigin),
                "text/html");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "[OAUTH-010] Google token exchange failed for session {SessionId}. ExType={ExType}",
                sessionId, ex.GetType().Name);
            await sessionStore.SaveAsync(session, ct);
            return Results.Content(BuildCallbackHtml(false, "Failed to connect Google account", platformOrigin), "text/html");
        }
    }

    private static string BuildCallbackHtml(bool success, string message, string platformOrigin)
    {
        var status = success ? "Connected!" : "Error";
        var successJs = success ? "true" : "false";
        // SEC-5: HTML-encode message for safe rendering in HTML body
        var htmlMessage = HttpUtility.HtmlEncode(message);
        // SEC-5: JS-encode message for safe embedding in JS string literal
        var jsMessage = message
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("<", "\\x3c")
            .Replace(">", "\\x3e");
        // SEC-6: JS-encode the origin for postMessage target
        var jsOrigin = platformOrigin
            .Replace("\\", "\\\\")
            .Replace("'", "\\'");

        return $$"""
            <!DOCTYPE html>
            <html>
            <head><title>Google OAuth</title></head>
            <body>
                <p>{{HttpUtility.HtmlEncode(status)}}: {{htmlMessage}}</p>
                <script>
                    window.opener?.postMessage({
                        type: 'google_oauth_callback',
                        success: {{successJs}},
                        message: '{{jsMessage}}'
                    }, '{{jsOrigin}}');
                    window.close();
                </script>
            </body>
            </html>
            """;
    }
}
