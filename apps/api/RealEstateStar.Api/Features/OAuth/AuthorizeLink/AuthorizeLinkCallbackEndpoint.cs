using RealEstateStar.Api.Features.OAuth.Services;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using System.Web;

namespace RealEstateStar.Api.Features.OAuth.AuthorizeLink;

/// <summary>
/// GET /oauth/google/authorize/callback — exchanges code, validates nonce, stores tokens, enqueues activation.
/// </summary>
public class AuthorizeLinkCallbackEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapGet("/oauth/google/authorize/callback", Handle)
            .RequireRateLimiting("oauth-link-authorize");

    internal static async Task<IResult> Handle(
        string? code,
        string state,
        string? error,
        AuthorizationLinkService authorizationLinkService,
        GoogleOAuthService googleOAuthService,
        ITokenStore tokenStore,
        ChannelWriter<ActivationRequest> activationChannel,
        ILogger<AuthorizeLinkCallbackEndpoint> logger,
        CancellationToken ct)
    {
        if (error is not null)
        {
            logger.LogWarning("[OAUTH-LINK-400] Google returned error: {Error}", error);
            return Results.Content(BuildErrorHtml("Authorization Denied", "Something Went Wrong",
                "Google authorization was denied. Please try again."), "text/html");
        }

        // Parse state to extract nonce: accountId:agentId:nonce (used only for logging until nonce is validated)
        var parts = state.Split(':', 3);
        if (parts.Length != 3)
        {
            logger.LogWarning("[OAUTH-LINK-401] Invalid state format. State={State}", state);
            return Results.Content(BuildErrorHtml("Invalid Request", "Something Went Wrong",
                "The authorization request was invalid. Please try again."), "text/html");
        }

        var nonce = parts[2];

        // Validate and consume nonce (single-use, constant-time).
        // HIGH-2: Use the nonce-bound accountId/agentId — not the raw state values.
        // This prevents an attacker from substituting a valid nonce with forged accountId/agentId parts.
        var linkState = authorizationLinkService.ValidateAndConsumeNonce(nonce);
        if (linkState is null)
        {
            logger.LogWarning("[OAUTH-LINK-402] Invalid or expired nonce.");
            return Results.Content(BuildErrorHtml("Link Expired", "This Link Has Expired",
                "This link has expired. Please request a new one."), "text/html");
        }

        // Use nonce-bound values — not the potentially-forged values from the raw state string
        var accountId = linkState.AccountId;
        var agentId = linkState.AgentId;

        if (code is null)
        {
            logger.LogWarning("[OAUTH-LINK-403] No code in callback. AccountId={AccountId}, AgentId={AgentId}",
                accountId, agentId);
            return Results.Content(BuildErrorHtml("No Code", "Something Went Wrong",
                "No authorization code was received. Please try again."), "text/html");
        }

        try
        {
            var tokens = await googleOAuthService.ExchangeCodeAsync(code, ct);

            // SEC: Verify Google email matches the expected email from the signed link
            if (!IsEmailMatch(linkState.Email, tokens.Email))
            {
                logger.LogWarning("[OAUTH-LINK-404] Google email mismatch. AccountId={AccountId}, AgentId={AgentId}, " +
                    "ExpectedEmailHash={ExpectedHash}, GoogleEmailHash={GoogleHash}",
                    accountId, agentId, HashEmail(linkState.Email), HashEmail(tokens.Email));
                return Results.Content(BuildErrorHtml("Email Mismatch", "Google Account Mismatch",
                    "The Google account you signed in with does not match the email address on your authorization link. " +
                    "Please sign out of Google and sign in with the correct business account."), "text/html");
            }

            // Persist tokens to durable store
            await tokenStore.SaveAsync(
                tokens with { AccountId = accountId, AgentId = agentId },
                OAuthProviders.Google,
                ct);

            logger.LogInformation("[OAUTH-LINK-405] Tokens persisted. AccountId={AccountId}, AgentId={AgentId}", accountId, agentId);

            // Enqueue activation request
            await activationChannel.WriteAsync(
                new ActivationRequest(accountId, agentId, linkState.Email, DateTime.UtcNow),
                ct);

            logger.LogInformation("[OAUTH-LINK-406] Activation enqueued. AccountId={AccountId}, AgentId={AgentId}", accountId, agentId);

            return Results.Content(BuildSuccessHtml(tokens.Name, tokens.Email), "text/html");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "[OAUTH-LINK-407] Token exchange failed. AccountId={AccountId}, AgentId={AgentId}",
                accountId, agentId);
            return Results.Content(BuildErrorHtml("Exchange Failed", "Something Went Wrong",
                "Failed to connect your Google account. Please try again."), "text/html");
        }
    }

    internal static bool IsEmailMatch(string? expectedEmail, string? googleEmail)
    {
        if (string.IsNullOrWhiteSpace(expectedEmail)) return true;
        if (string.IsNullOrWhiteSpace(googleEmail)) return false;
        return string.Equals(expectedEmail.Trim(), googleEmail.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    internal static string HashEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "null";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    private static string BuildSuccessHtml(string name, string email)
    {
        var safeName = HttpUtility.HtmlEncode(name);
        var safeEmail = HttpUtility.HtmlEncode(email);
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Google account connected — Real Estate Star</title>
                <style>
                    body { font-family: system-ui, sans-serif; max-width: 480px; margin: 80px auto; padding: 24px; color: #1a1a2e; }
                    h1 { color: #28a745; }
                    .email { color: #555; font-style: italic; }
                </style>
            </head>
            <body>
                <h1>Google account connected!</h1>
                <p>Welcome, <strong>{{safeName}}</strong>.</p>
                <p class="email">Connected as {{safeEmail}}</p>
                <p>Your Real Estate Star automation is being activated. You'll receive a confirmation email shortly.</p>
            </body>
            </html>
            """;
    }

    private static string BuildErrorHtml(string title, string heading, string message)
    {
        var safeTitle = HttpUtility.HtmlEncode(title);
        var safeHeading = HttpUtility.HtmlEncode(heading);
        var safeMessage = HttpUtility.HtmlEncode(message);
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>{{safeTitle}} — Real Estate Star</title>
                <style>
                    body { font-family: system-ui, sans-serif; max-width: 480px; margin: 80px auto; padding: 24px; color: #1a1a2e; }
                    h1 { color: #dc3545; }
                    .message { color: #555; }
                </style>
            </head>
            <body>
                <h1>{{safeHeading}}</h1>
                <p class="message">{{safeMessage}}</p>
            </body>
            </html>
            """;
    }
}
