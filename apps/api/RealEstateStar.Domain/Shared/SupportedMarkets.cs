namespace RealEstateStar.Domain.Shared;

/// <summary>
/// Go-to-market states where Real Estate Star is licensed to operate.
/// Used for: timezone resolution, compliance forms, state-specific contracts,
/// and validation of new agent onboarding.
/// </summary>
public static class SupportedMarkets
{
    public static readonly IReadOnlySet<string> States = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "NY", "NJ", "PA", "CA", "FL", "SC", "MA", "CT", "PR"
    };

    public static bool IsSupported(string? state) =>
        state is not null && States.Contains(state);
}
