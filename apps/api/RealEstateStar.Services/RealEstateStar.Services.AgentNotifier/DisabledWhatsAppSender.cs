using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Senders;

namespace RealEstateStar.Services.AgentNotifier;

/// <summary>
/// Null-object implementation registered when WhatsApp is not configured.
/// Ensures the DI container resolves IWhatsAppSender without throwing,
/// while making it clear that no messages will be sent.
/// </summary>
public class DisabledWhatsAppSender(ILogger<DisabledWhatsAppSender> logger) : IWhatsAppSender
{
    public Task<string> SendTemplateAsync(string toPhoneNumber, string templateName,
        List<(string type, string value)> parameters, CancellationToken ct)
    {
        logger.LogDebug("[WA-SENDER-000] WhatsApp disabled — skipping template {Template} to {Phone}",
            templateName, toPhoneNumber);
        return Task.FromResult(string.Empty);
    }

    public Task<string> SendFreeformAsync(string toPhoneNumber, string text, CancellationToken ct)
    {
        logger.LogDebug("[WA-SENDER-001] WhatsApp disabled — skipping freeform to {Phone}", toPhoneNumber);
        return Task.FromResult(string.Empty);
    }

    public Task MarkReadAsync(string messageId, CancellationToken ct)
    {
        logger.LogDebug("[WA-SENDER-002] WhatsApp disabled — skipping mark-read for {MessageId}", messageId);
        return Task.CompletedTask;
    }
}
