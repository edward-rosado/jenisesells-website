using RealEstateStar.Domain.Onboarding.Models;
using System.Text.Json;

namespace RealEstateStar.Domain.Onboarding.Interfaces;

public interface IOnboardingTool
{
    string Name { get; }
    Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct);
}
