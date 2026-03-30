using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.Interfaces;

public interface IWelcomeNotificationService
{
    /// <summary>
    /// Sends a personalized welcome message to the agent via WhatsApp or email fallback.
    /// Idempotent — does not re-send if already sent (tracked via WelcomeSent flag).
    /// </summary>
    Task SendAsync(
        string accountId,
        string agentId,
        string handle,
        ActivationOutputs outputs,
        CancellationToken ct);
}
