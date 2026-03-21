using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.WhatsApp;
using RealEstateStar.Api.Features.WhatsApp.Services;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Api.Features.Leads.Services;

/// <summary>
/// Dispatches lead notifications via preferred channel with cascading fallback.
/// Priority: WhatsApp → Email → File Storage (Drive).
/// WhatsApp is preferred — if it succeeds, email is skipped.
/// If both WhatsApp and email fail, the notification is persisted to file storage
/// so the agent can still see it in their Drive folder.
/// </summary>
public class CascadingAgentNotifier(
    IWhatsAppNotifier whatsAppNotifier,
    IEmailNotifier emailNotifier,
    IConversationLogger conversationLogger,
    IAccountConfigService configService,
    ILogger<CascadingAgentNotifier> logger)
{
    public async Task NotifyAgentAsync(string agentId, LeadNotification lead, CancellationToken ct)
    {
        var agentConfig = await LoadConfigAsync(agentId, ct);

        var whatsAppSent = false;
        var emailSent = false;

        // ── WhatsApp channel (preferred) ──────────────────────────────────────
        try
        {
            if (agentConfig?.Integrations?.WhatsApp?.OptedIn == true)
            {
                var templateParams = WhatsAppMappers.ToNewLeadParams(
                    lead.Name, lead.Phone, lead.Email, lead.Interest, lead.Area);

                var paramDict = templateParams
                    .Select((p, i) => (key: $"param_{i}", p.value))
                    .ToDictionary(x => x.key, x => x.value);

                await whatsAppNotifier.NotifyAsync(
                    agentId, NotificationType.NewLead, lead.Name, paramDict, ct);
                whatsAppSent = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[LEAD-040] WhatsApp notification failed for {AgentId}, falling back to email", agentId);
        }

        // ── Email channel (fallback — only when WhatsApp didn't send) ─────────
        if (!whatsAppSent)
        {
            try
            {
                await emailNotifier.SendLeadNotificationAsync(agentId, lead, ct);
                emailSent = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[LEAD-041] Email fallback also failed for {AgentId}", agentId);
            }
        }

        // ── File storage (last resort — only when both channels failed) ───────
        if (!whatsAppSent && !emailSent)
        {
            try
            {
                var body = $"New lead: {lead.Name} | {lead.Phone} | {lead.Email} | Interest: {lead.Interest} | Area: {lead.Area}";
                await conversationLogger.LogMessagesAsync(agentId, lead.Name,
                    [(DateTime.UtcNow, "Real Estate Star", body, null)], ct);
                logger.LogWarning("[LEAD-042] Both WhatsApp and email failed for {AgentId}, notification persisted to file storage", agentId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[LEAD-043] All notification channels failed for {AgentId} — lead {LeadName} may not have been notified",
                    agentId, lead.Name);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<AccountConfig?> LoadConfigAsync(
        string agentId, CancellationToken ct)
    {
        try
        {
            return await configService.GetAccountAsync(agentId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[LEAD-039] Failed to load agent config for {AgentId} during lead notification", agentId);
            return null;
        }
    }
}
