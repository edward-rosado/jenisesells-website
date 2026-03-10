namespace RealEstateStar.Api.Features.Onboarding;

public static class OnboardingHelpers
{
    public static string GenerateSlug(string? name)
    {
        var slug = (name ?? "agent").ToLowerInvariant().Replace(" ", "-");
        slug = string.Concat(slug.Where(c => char.IsLetterOrDigit(c) || c == '-'));
        return string.IsNullOrEmpty(slug) ? "agent" : slug;
    }
}
