using RealEstateStar.Domain.WhatsApp.Interfaces;

namespace RealEstateStar.Workers.WhatsApp;

/// <summary>
/// Noop implementation of IResponseGenerator used until the Claude Sonnet generator
/// is wired up. Returns a static placeholder so the pipeline can be exercised end-to-end.
/// </summary>
public class NoopResponseGenerator : IResponseGenerator
{
    public Task<string> GenerateAsync(
        string agentFirstName,
        string messageText,
        string? leadName,
        CancellationToken ct) =>
        Task.FromResult("I've received your message and will respond shortly.");
}
