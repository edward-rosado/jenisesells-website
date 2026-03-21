namespace RealEstateStar.Api.Features.WhatsApp.Services;

/// <summary>
/// Generates a contextual response for in-scope lead questions and action requests.
/// Production implementation calls Claude Sonnet (quality responses).
/// </summary>
public interface IResponseGenerator
{
    Task<string> GenerateAsync(
        string agentFirstName,
        string messageText,
        string? leadName,
        CancellationToken ct);
}
