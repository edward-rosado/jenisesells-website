namespace RealEstateStar.Api.Features.Onboarding.PostChat;

public sealed record PostChatRequest
{
    public required string Message { get; init; }
}
