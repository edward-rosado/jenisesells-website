using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;

namespace RealEstateStar.Api.Features.Onboarding.ConnectGoogle;

public class StartGoogleOAuthEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapGet("/oauth/google/start", Handle);
    }

    internal static async Task<IResult> Handle(
        string sessionId,
        HttpContext httpContext,
        ISessionDataService sessionStore,
        GoogleOAuthService oAuthService,
        CancellationToken ct)
    {
        var session = await sessionStore.LoadAsync(sessionId, ct);
        if (session is null) return Results.NotFound();

        if (!ValidateBearerToken(httpContext, session))
            return Results.Unauthorized();

        if (session.CurrentState != OnboardingState.ConnectGoogle)
            return Results.BadRequest("Session is not in ConnectGoogle state");

        var (authUrl, nonce) = oAuthService.BuildAuthorizationUrl(sessionId);
        session.OAuthNonce = nonce;
        await sessionStore.SaveAsync(session, ct);

        return Results.Redirect(authUrl);
    }

    internal static bool ValidateBearerToken(HttpContext httpContext, OnboardingSession session)
    {
        var authHeader = httpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        var token = authHeader["Bearer ".Length..];
        return string.Equals(token, session.BearerToken, StringComparison.Ordinal);
    }
}
