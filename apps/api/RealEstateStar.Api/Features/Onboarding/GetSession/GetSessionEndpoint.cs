using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Onboarding.GetSession;

public class GetSessionEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapGet("/onboard/{sessionId}", Handle);
    }

    internal static async Task<IResult> Handle(
        string sessionId,
        ISessionStore sessionStore,
        CancellationToken ct)
    {
        var session = await sessionStore.LoadAsync(sessionId, ct);
        return session is null
            ? Results.NotFound()
            : Results.Ok(session.ToGetResponse());
    }
}
