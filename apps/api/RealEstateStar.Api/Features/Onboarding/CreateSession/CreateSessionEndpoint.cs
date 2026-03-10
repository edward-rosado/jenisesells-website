using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Onboarding.CreateSession;

public class CreateSessionEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapPost("/onboard", Handle);
    }

    internal static async Task<IResult> Handle(
        CreateSessionRequest request,
        ISessionStore sessionStore,
        CancellationToken ct)
    {
        var session = request.ToSession();
        await sessionStore.SaveAsync(session, ct);
        return Results.Ok(session.ToCreateResponse());
    }
}
