using RealEstateStar.Api.Features.Onboarding.CreateSession;
using RealEstateStar.Api.Features.Onboarding.GetSession;
using RealEstateStar.Domain.Onboarding.Models;

namespace RealEstateStar.Api.Features.Onboarding;

public static class OnboardingMappers
{
    public static OnboardingSession ToSession(this CreateSessionRequest request)
        => OnboardingSession.Create(request.ProfileUrl);

    public static CreateSessionResponse ToCreateResponse(this OnboardingSession session)
        => new(session.Id, session.BearerToken);

    public static GetSessionResponse ToGetResponse(this OnboardingSession session)
        => new()
        {
            SessionId = session.Id,
            CurrentState = session.CurrentState,
            SiteUrl = session.SiteUrl,
            CreatedAt = session.CreatedAt,
        };
}
