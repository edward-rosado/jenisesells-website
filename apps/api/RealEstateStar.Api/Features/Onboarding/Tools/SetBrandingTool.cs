using System.Text.Json;
using RealEstateStar.Api.Features.Onboarding.Services;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class SetBrandingTool(OnboardingStateMachine stateMachine) : IOnboardingTool
{
    public string Name => "set_branding";

    public Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        var current = session.Profile ?? new ScrapedProfile();

        session.Profile = current with
        {
            PrimaryColor = parameters.TryGetProperty("primaryColor", out var pc) ? pc.GetString() : current.PrimaryColor,
            AccentColor = parameters.TryGetProperty("accentColor", out var ac) ? ac.GetString() : current.AccentColor,
            LogoUrl = parameters.TryGetProperty("logoUrl", out var lu) ? lu.GetString() : current.LogoUrl,
        };

        // Auto-advance to ConnectGoogle
        if (stateMachine.CanAdvance(session, OnboardingState.ConnectGoogle))
            stateMachine.Advance(session, OnboardingState.ConnectGoogle);

        return Task.FromResult($"SUCCESS: Branding saved — primary={session.Profile.PrimaryColor}, accent={session.Profile.AccentColor}.");
    }
}
