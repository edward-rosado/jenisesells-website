using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Onboarding.ConnectGoogle;

public class StartGoogleOAuthEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapGet("/oauth/google/start", Handle);
    }

    internal static async Task<IResult> Handle(
        string sessionId,
        ISessionStore sessionStore,
        GoogleOAuthService oAuthService,
        CancellationToken ct)
    {
        var session = await sessionStore.LoadAsync(sessionId, ct);
        if (session is null) return Results.NotFound();

        if (session.CurrentState != OnboardingState.ConnectGoogle)
            return Results.BadRequest("Session is not in ConnectGoogle state");

        var authUrl = oAuthService.BuildAuthorizationUrl(sessionId);
        return Results.Redirect(authUrl);
    }
}
