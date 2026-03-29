using RealEstateStar.Api.Features.OAuth.Services;
using RealEstateStar.Api.Infrastructure;
using System.Web;

namespace RealEstateStar.Api.Features.OAuth.AuthorizeLink;

/// <summary>
/// GET /oauth/google/authorize — validates HMAC-signed link and renders branded landing page.
/// </summary>
public class AuthorizeLinkEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapGet("/oauth/google/authorize", Handle)
            .RequireRateLimiting("oauth-link-authorize");

    internal static Task<IResult> Handle(
        string accountId,
        string agentId,
        string email,
        long exp,
        string sig,
        AuthorizationLinkService authorizationLinkService,
        ILogger<AuthorizeLinkEndpoint> logger,
        CancellationToken ct)
    {
        // Check expiry first for a better UX message
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
        {
            logger.LogDebug("[OAUTH-LINK-200] Expired link accessed. AccountId={AccountId}, AgentId={AgentId}", accountId, agentId);
            return Task.FromResult(Results.StatusCode(410));
        }

        if (!authorizationLinkService.ValidateSignature(accountId, agentId, email, exp, sig))
        {
            logger.LogWarning("[OAUTH-LINK-201] Invalid signature. AccountId={AccountId}, AgentId={AgentId}", accountId, agentId);
            return Task.FromResult(Results.Unauthorized());
        }

        logger.LogInformation("[OAUTH-LINK-202] Valid authorization link accessed. AccountId={AccountId}, AgentId={AgentId}",
            accountId, agentId);

        var html = BuildLandingPageHtml(accountId, agentId, email, exp, sig);
        return Task.FromResult(Results.Content(html, "text/html"));
    }

    private static string BuildLandingPageHtml(
        string accountId, string agentId, string email, long exp, string sig)
    {
        var safeAccountId = HttpUtility.HtmlEncode(accountId);
        var safeAgentId = HttpUtility.HtmlEncode(agentId);
        var safeEmail = HttpUtility.HtmlEncode(email);
        var safeExp = HttpUtility.HtmlEncode(exp.ToString());
        var safeSig = HttpUtility.HtmlEncode(sig);

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Connect Google Account — Real Estate Star</title>
                <style>
                    body { font-family: system-ui, sans-serif; max-width: 480px; margin: 80px auto; padding: 24px; color: #1a1a2e; }
                    h1 { font-size: 1.5rem; margin-bottom: 8px; }
                    .subtitle { color: #555; margin-bottom: 24px; }
                    .warning { background: #fff3cd; border: 1px solid #ffc107; border-radius: 8px; padding: 12px 16px; margin-bottom: 24px; font-size: 0.9rem; }
                    .btn { display: inline-block; background: #4285f4; color: #fff; text-decoration: none; padding: 12px 28px; border-radius: 6px; font-size: 1rem; font-weight: 600; cursor: pointer; border: none; }
                    .btn:hover { background: #3367d6; }
                    form { display: inline; }
                </style>
            </head>
            <body>
                <h1>Connect Your Business Google Account</h1>
                <p class="subtitle">Hello, {{safeAgentId}} — link your Google Workspace account to activate Real Estate Star automation.</p>
                <div class="warning">
                    <strong>Important:</strong> Please sign in with your <em>business</em> Google account, not a personal one.
                    This account will be used to send emails and manage your Google Drive files on your behalf.
                </div>
                <form method="POST" action="/oauth/google/authorize/connect">
                    <input type="hidden" name="accountId" value="{{safeAccountId}}">
                    <input type="hidden" name="agentId" value="{{safeAgentId}}">
                    <input type="hidden" name="email" value="{{safeEmail}}">
                    <input type="hidden" name="exp" value="{{safeExp}}">
                    <input type="hidden" name="sig" value="{{safeSig}}">
                    <button type="submit" class="btn">Connect with Google</button>
                </form>
            </body>
            </html>
            """;
    }
}
