using RealEstateStar.Api.Features.Onboarding.Services;
using System.Text.Json;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class GoogleAuthCardTool(GoogleOAuthService oAuthService, ISessionDataService sessionStore) : IOnboardingTool
{
    public string Name => "google_auth_card";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        var (authUrl, nonce) = oAuthService.BuildAuthorizationUrl(session.Id);
        session.OAuthNonce = nonce;
        await sessionStore.SaveAsync(session, ct);

        var json = JsonSerializer.Serialize(new { oauthUrl = authUrl });
        return $"[CARD:google_auth]{json}";
    }
}
