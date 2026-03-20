namespace RealEstateStar.Api.Features.Onboarding.Tools;

public interface ISiteDeployService
{
    Task<string> DeployAsync(OnboardingSession session, CancellationToken ct);
}
