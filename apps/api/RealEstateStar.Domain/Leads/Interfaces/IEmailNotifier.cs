using RealEstateStar.Domain.Leads.Models;
namespace RealEstateStar.Domain.Leads.Interfaces;

/// <summary>
/// Sends email notifications to agents about new leads.
/// Stub — full implementation will be added with the email channel plan.
/// </summary>
public interface IEmailNotifier
{
    /// <summary>
    /// Sends a new-lead notification email to the agent identified by <paramref name="agentId"/>.
    /// Implementations must never throw — failures should be logged internally.
    /// </summary>
    Task SendLeadNotificationAsync(string agentId, LeadNotification lead, CancellationToken ct);
}
