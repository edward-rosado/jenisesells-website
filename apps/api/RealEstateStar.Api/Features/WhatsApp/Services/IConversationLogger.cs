namespace RealEstateStar.Api.Features.WhatsApp.Services;

public interface IConversationLogger
{
    /// <summary>
    /// Appends a batch of WhatsApp messages as markdown to the appropriate Drive folder.
    /// If leadName is provided, messages are written to the per-lead folder;
    /// otherwise they go to the general WhatsApp conversation log.
    /// Drive write failures are logged as [WA-011] warnings and do NOT propagate.
    /// </summary>
    Task LogMessagesAsync(string agentId, string? leadName,
        List<(DateTime timestamp, string sender, string body, string? templateName)> messages,
        CancellationToken ct);
}
