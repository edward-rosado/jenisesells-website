using RealEstateStar.Api.Features.Onboarding.Services;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.Extensions.Logging;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

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
        ITokenStore tokenStore,
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
                Encoding.UTF8.GetBytes(nonce),
                Encoding.UTF8.GetBytes(session.OAuthNonce)))
        {
            return Results.Content(BuildCallbackHtml(false, "Invalid OAuth state", platformOrigin), "text/html");
        }

        // Clear nonce after use (single-use) and persist immediately so a replay attempt
        // on a second concurrent request will see null and be rejected, even if the code
        // exchange below is still in flight.
        session.OAuthNonce = null;
        await sessionStore.SaveAsync(session, ct);

        try
        {
            var tokens = await oAuthService.ExchangeCodeAsync(code, ct);

            // SEC-6: Cross-validate Google email against scraped profile email
            if (!IsEmailMatch(session.Profile?.Email, tokens.Email))
            {
                logger.LogWarning("[OAUTH-011] Google email mismatch for session {SessionId}. " +
                    "ProfileEmailHash={ProfileHash}, GoogleEmailHash={GoogleHash}",
                    sessionId, HashEmail(session.Profile?.Email), HashEmail(tokens.Email));
                return Results.Content(
                    BuildCallbackHtml(false,
                        "Google account email does not match your profile email. Please sign in with the correct Google account.",
                        platformOrigin),
                    "text/html");
            }

            session.GoogleTokens = tokens;
            stateMachine.Advance(session, OnboardingState.DemoCma);

            // Persist to durable token store for future API calls (GDrive, Gmail, etc.)
            // AgentConfigId is the agent identifier; single-agent brokerages use agentId as accountId
            var agentId = session.AgentConfigId;
            if (agentId is not null)
            {
                var accountId = agentId; // single-agent: accountId == agentId
                await tokenStore.SaveAsync(
                    tokens with { AccountId = accountId, AgentId = agentId },
                    OAuthProviders.Google,
                    ct);
                logger.LogInformation("[OAUTH-013] Persisted OAuth tokens to token store for agent {AgentId}", agentId);
            }
            else
            {
                logger.LogWarning("[OAUTH-014] AgentConfigId not set on session {SessionId} — skipping token store persist", sessionId);
            }

            await sessionStore.SaveAsync(session, ct);

            return Results.Content(
                BuildCallbackHtml(true, $"Connected as {tokens.Name} ({tokens.Email})", platformOrigin),
                "text/html");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "[OAUTH-010] Google token exchange failed for session {SessionId}. ExType={ExType}",
                sessionId, ex.GetType().Name);
            return Results.Content(BuildCallbackHtml(false, "Failed to connect Google account", platformOrigin), "text/html");
        }
    }

    /// <summary>
    /// Returns true if the Google email matches the scraped profile email (case-insensitive),
    /// or if the scraped profile has no email (allow — agent may not have public email).
    /// Returns false only when both emails are present and they don't match.
    /// </summary>
    internal static bool IsEmailMatch(string? profileEmail, string? googleEmail)
    {
        // No scraped profile email — allow any Google account
        if (string.IsNullOrWhiteSpace(profileEmail))
            return true;

        // Google returned no email — should not happen with email scope, but guard
        if (string.IsNullOrWhiteSpace(googleEmail))
            return false;

        return string.Equals(profileEmail.Trim(), googleEmail.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Hashes an email for safe logging (no PII in logs).
    /// Returns "null" for null/empty input.
    /// </summary>
    internal static string HashEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "null";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
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
