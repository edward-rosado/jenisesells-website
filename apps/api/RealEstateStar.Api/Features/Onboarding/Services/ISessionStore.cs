namespace RealEstateStar.Api.Features.Onboarding.Services;

public interface ISessionStore
{
    Task SaveAsync(OnboardingSession session, CancellationToken ct);
    Task<OnboardingSession?> LoadAsync(string sessionId, CancellationToken ct);
}
