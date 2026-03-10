namespace RealEstateStar.Api.Features.Onboarding.Services;

public interface IDriveFolderInitializer
{
    Task EnsureFolderStructureAsync(OnboardingSession session, string agentEmail, CancellationToken ct);
}
