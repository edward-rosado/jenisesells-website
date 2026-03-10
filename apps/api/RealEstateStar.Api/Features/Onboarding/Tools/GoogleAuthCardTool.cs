using System.Text.Json;
using RealEstateStar.Api.Features.Onboarding.Services;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class GoogleAuthCardTool(GoogleOAuthService oAuthService, ISessionStore sessionStore) : IOnboardingTool
{
    public string Name => "google_auth_card";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        var (authUrl, nonce) = oAuthService.BuildAuthorizationUrl(session.Id);
        session.OAuthNonce = nonce;
        await sessionStore.SaveAsync(session, ct);

        return $"Google OAuth URL: {authUrl} — " +
            "Render a google_auth card with a 'Connect Google Account' button that opens this URL in a popup window.";
    }
}
