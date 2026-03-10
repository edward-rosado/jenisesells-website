using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class UpdateProfileTool : IOnboardingTool
{
    public string Name => "update_profile";

    public Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        var current = session.Profile ?? new ScrapedProfile();

        session.Profile = current with
        {
            Name = parameters.TryGetProperty("name", out var n) ? n.GetString() : current.Name,
            Phone = parameters.TryGetProperty("phone", out var p) ? p.GetString() : current.Phone,
            Email = parameters.TryGetProperty("email", out var e) ? e.GetString() : current.Email,
            Brokerage = parameters.TryGetProperty("brokerage", out var b) ? b.GetString() : current.Brokerage,
            State = parameters.TryGetProperty("state", out var s) ? s.GetString() : current.State,
        };

        return Task.FromResult($"Profile updated: {session.Profile.Name}");
    }
}
