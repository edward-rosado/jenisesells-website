using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.Onboarding;

[JsonConverter(typeof(JsonStringEnumConverter<ChatRole>))]
public enum ChatRole
{
    User,
    Assistant,
    System,
    Tool,
}

public sealed record ChatMessage
{
    public required ChatRole Role { get; init; }
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
