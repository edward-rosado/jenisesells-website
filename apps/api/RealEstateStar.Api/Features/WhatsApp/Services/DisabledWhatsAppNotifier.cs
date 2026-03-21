using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.WhatsApp;

namespace RealEstateStar.Api.Features.WhatsApp.Services;

/// <summary>
/// Null-object implementation registered when WhatsApp is not configured.
/// Ensures the DI container resolves IWhatsAppNotifier without throwing,
/// while making it clear that no messages will be sent.
/// </summary>
public class DisabledWhatsAppNotifier(ILogger<DisabledWhatsAppNotifier> logger) : IWhatsAppNotifier
{
    public Task NotifyAsync(string agentId, NotificationType type,
        string? leadName, Dictionary<string, string> templateParams, CancellationToken ct)
    {
        logger.LogDebug("[WA-000] WhatsApp disabled — skipping {Type} notification for {AgentId}", type, agentId);
        return Task.CompletedTask;
    }

    public void RecordAgentMessage(string agentPhone) { }

    public bool IsWindowOpen(string agentPhone) => false;
}
