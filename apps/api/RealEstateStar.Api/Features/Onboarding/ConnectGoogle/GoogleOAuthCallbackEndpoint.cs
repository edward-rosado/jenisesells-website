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
        CancellationToken ct)
    {
        // The callback renders HTML that uses postMessage to notify the parent window
        if (error is not null)
            return Results.Content(BuildCallbackHtml(false, $"Google authorization denied: {error}"), "text/html");

        if (code is null)
            return Results.Content(BuildCallbackHtml(false, "No authorization code received"), "text/html");

        var session = await sessionStore.LoadAsync(state, ct);
        if (session is null)
            return Results.Content(BuildCallbackHtml(false, "Session not found"), "text/html");

        try
        {
            var tokens = await oAuthService.ExchangeCodeAsync(code, ct);
            session.GoogleTokens = tokens;
            stateMachine.Advance(session, OnboardingState.GenerateSite);
            await sessionStore.SaveAsync(session, ct);

            return Results.Content(
                BuildCallbackHtml(true, $"Connected as {tokens.GoogleName} ({tokens.GoogleEmail})"),
                "text/html");
        }
        catch (InvalidOperationException)
        {
            return Results.Content(BuildCallbackHtml(false, "Failed to connect Google account"), "text/html");
        }
    }

    private static string BuildCallbackHtml(bool success, string message)
    {
        var status = success ? "Connected!" : "Error";
        var successJs = success.ToString().ToLowerInvariant();
        var escapedMessage = message.Replace("'", "\\'").Replace("\"", "\\\"");
        return $$"""
            <!DOCTYPE html>
            <html>
            <head><title>Google OAuth</title></head>
            <body>
                <p>{{status}}: {{message}}</p>
                <script>
                    window.opener?.postMessage({
                        type: 'google_oauth_callback',
                        success: {{successJs}},
                        message: '{{escapedMessage}}'
                    }, '*');
                    window.close();
                </script>
            </body>
            </html>
            """;
    }
}
