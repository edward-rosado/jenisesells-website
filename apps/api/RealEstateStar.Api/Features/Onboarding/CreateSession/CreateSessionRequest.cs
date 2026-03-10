namespace RealEstateStar.Api.Features.Onboarding.CreateSession;

public sealed record CreateSessionRequest
{
    public string? ProfileUrl { get; init; }
}
