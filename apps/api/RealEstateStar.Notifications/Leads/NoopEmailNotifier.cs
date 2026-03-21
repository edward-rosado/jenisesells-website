using Microsoft.Extensions.Logging;

namespace RealEstateStar.Notifications.Leads;

/// <summary>
/// Noop implementation of IEmailNotifier used until the email channel is built.
/// Logs the intent and returns without sending anything.
/// </summary>
public class NoopEmailNotifier(ILogger<NoopEmailNotifier> logger) : IEmailNotifier
{
    public Task SendLeadNotificationAsync(string agentId, LeadNotification lead, CancellationToken ct)
    {
        logger.LogDebug("[EMAIL-001] Noop: SendLeadNotification for {AgentId} lead {LeadName}",
            agentId, lead.Name);
        return Task.CompletedTask;
    }
}
