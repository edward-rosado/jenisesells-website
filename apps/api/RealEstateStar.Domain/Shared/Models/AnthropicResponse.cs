namespace RealEstateStar.Domain.Shared.Models;

public sealed record AnthropicResponse(
    string Content, int InputTokens, int OutputTokens, double DurationMs);
