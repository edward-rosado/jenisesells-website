using RealEstateStar.Api.Features.WhatsApp;

namespace RealEstateStar.Api.Features.WhatsApp.Services;

public interface IWhatsAppNotifier
{
    /// <summary>
    /// Sends an outbound notification to the agent's WhatsApp number.
    /// Checks opt-in status and notification preferences before sending.
    /// DataDeletion bypasses preferences and is always sent.
    /// Logs [WA-014] on WhatsAppNotRegisteredException and [WA-015] on other errors — never throws.
    /// </summary>
    Task NotifyAsync(string agentId, NotificationType type,
        string? leadName, Dictionary<string, string> templateParams,
        CancellationToken ct);

    /// <summary>
    /// Marks the 24-hour conversation window as open for the given agent phone number.
    /// Called by WebhookProcessorWorker whenever the agent sends a message.
    /// </summary>
    void RecordAgentMessage(string agentPhone);

    /// <summary>
    /// Returns true if the 24-hour conversation window is currently open for the given phone.
    /// </summary>
    bool IsWindowOpen(string agentPhone);
}
