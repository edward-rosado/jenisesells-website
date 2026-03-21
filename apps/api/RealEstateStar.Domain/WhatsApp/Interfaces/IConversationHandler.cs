namespace RealEstateStar.Domain.WhatsApp.Interfaces;

/// <summary>
/// Classifies intent and generates responses for inbound WhatsApp messages.
/// Implementation in Phase 3 (Task 11).
/// </summary>
public interface IConversationHandler
{
    Task<string> HandleMessageAsync(string agentId, string agentFirstName,
        string body, string? leadName, CancellationToken ct);
}
