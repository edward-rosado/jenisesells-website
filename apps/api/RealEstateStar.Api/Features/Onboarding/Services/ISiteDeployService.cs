namespace RealEstateStar.Api.Features.Onboarding.Services;

public interface ISiteDeployService
{
    Task<string> DeployAsync(OnboardingSession session, CancellationToken ct);
}
