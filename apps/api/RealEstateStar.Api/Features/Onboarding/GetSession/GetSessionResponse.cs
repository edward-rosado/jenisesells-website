namespace RealEstateStar.Api.Features.Onboarding.GetSession;

public sealed record GetSessionResponse
{
    public required string SessionId { get; init; }
    public required OnboardingState CurrentState { get; init; }
    public string? ProfileUrl { get; init; }
    public ScrapedProfile? Profile { get; init; }
    public required List<ChatMessage> Messages { get; init; }
    public string? SiteUrl { get; init; }
    public required DateTime CreatedAt { get; init; }
}
