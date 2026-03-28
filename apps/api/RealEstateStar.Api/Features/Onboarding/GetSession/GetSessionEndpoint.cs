using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;

namespace RealEstateStar.Api.Features.Onboarding.GetSession;

public class GetSessionEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapGet("/onboard/{sessionId}", Handle);
    }

    internal static async Task<IResult> Handle(
        string sessionId,
        HttpContext httpContext,
        ISessionStore sessionStore,
        CancellationToken ct)
    {
        var session = await sessionStore.LoadAsync(sessionId, ct);
        if (session is null) return Results.NotFound();

        if (!ValidateBearerToken(httpContext, session))
            return Results.Unauthorized();

        return Results.Ok(session.ToGetResponse());
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
