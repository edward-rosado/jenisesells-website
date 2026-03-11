using System.Text.Json;
using RealEstateStar.Api.Features.Onboarding.Services;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class UpdateProfileTool(OnboardingStateMachine stateMachine) : IOnboardingTool
{
    public string Name => "update_profile";

    public Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        var current = session.Profile ?? new ScrapedProfile();

        session.Profile = current with
        {
            Name = parameters.TryGetProperty("name", out var n) ? n.GetString() : current.Name,
            Title = parameters.TryGetProperty("title", out var t) ? t.GetString() : current.Title,
            Phone = parameters.TryGetProperty("phone", out var p) ? p.GetString() : current.Phone,
            Email = parameters.TryGetProperty("email", out var e) ? e.GetString() : current.Email,
            Brokerage = parameters.TryGetProperty("brokerage", out var b) ? b.GetString() : current.Brokerage,
            State = parameters.TryGetProperty("state", out var s) ? s.GetString() : current.State,
            OfficeAddress = parameters.TryGetProperty("officeAddress", out var oa) ? oa.GetString() : current.OfficeAddress,
            Tagline = parameters.TryGetProperty("tagline", out var tg) ? tg.GetString() : current.Tagline,
        };

        // Auto-advance: ScrapeProfile → ConfirmIdentity, or ConfirmIdentity → CollectBranding
        if (session.CurrentState == OnboardingState.ScrapeProfile
            && stateMachine.CanAdvance(session, OnboardingState.ConfirmIdentity))
        {
            stateMachine.Advance(session, OnboardingState.ConfirmIdentity);
        }
        else if (session.CurrentState == OnboardingState.ConfirmIdentity
            && stateMachine.CanAdvance(session, OnboardingState.CollectBranding))
        {
            stateMachine.Advance(session, OnboardingState.CollectBranding);
        }

        return Task.FromResult($"SUCCESS: Profile saved for {session.Profile.Name}.");
    }
}
