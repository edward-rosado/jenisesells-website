namespace RealEstateStar.Api.Features.Onboarding.Services;

public interface IDriveFolderInitializer
{
    Task EnsureFolderStructureAsync(string agentEmail, CancellationToken ct);
}
