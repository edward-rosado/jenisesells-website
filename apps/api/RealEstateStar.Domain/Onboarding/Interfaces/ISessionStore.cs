using RealEstateStar.Domain.Onboarding.Models;
namespace RealEstateStar.Domain.Onboarding.Interfaces;

public interface ISessionStore
{
    Task SaveAsync(OnboardingSession session, CancellationToken ct);
    Task<OnboardingSession?> LoadAsync(string sessionId, CancellationToken ct);
}
