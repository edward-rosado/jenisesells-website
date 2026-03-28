using System.Text.Json;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Services;

namespace RealEstateStar.Workers.Onboarding.Tools;

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

        // Auto-advance: ScrapeProfile → GenerateSite (manual profile entry skips scrape)
        if (session.CurrentState == OnboardingState.ScrapeProfile
            && stateMachine.CanAdvance(session, OnboardingState.GenerateSite))
        {
            stateMachine.Advance(session, OnboardingState.GenerateSite);
        }

        return Task.FromResult($"SUCCESS: Profile saved for {session.Profile.Name}.");
    }
}
