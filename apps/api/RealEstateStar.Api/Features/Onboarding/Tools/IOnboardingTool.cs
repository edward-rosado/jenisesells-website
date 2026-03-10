using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public interface IOnboardingTool
{
    string Name { get; }
    Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct);
}
