using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Workers.Onboarding;
using RealEstateStar.Domain.Onboarding.Interfaces;

namespace RealEstateStar.Api.Features.Onboarding.CreateSession;

public class CreateSessionEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapPost("/onboard", Handle)
            .RequireRateLimiting("session-create");
    }

    internal static async Task<IResult> Handle(
        CreateSessionRequest request,
        ISessionDataService sessionStore,
        ILogger<CreateSessionEndpoint> logger,
        CancellationToken ct)
    {
        logger.LogInformation("[SESSION-CREATE-010] Creating session, profileUrl={HasUrl}",
            request.ProfileUrl is not null);

        var session = request.ToSession();
        await sessionStore.SaveAsync(session, ct);

        OnboardingChatService.SessionsCreated.Add(1);
        logger.LogInformation("[SESSION-CREATE-011] Session created: {SessionId}", session.Id);
        return Results.Ok(session.ToCreateResponse());
    }
}
