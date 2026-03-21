using RealEstateStar.Domain.Onboarding.Models;
namespace RealEstateStar.Domain.Onboarding.Interfaces;

public interface ISiteDeployService
{
    Task<string> DeployAsync(OnboardingSession session, CancellationToken ct);
}
