using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.WhatsApp;
using RealEstateStar.Api.Features.WhatsApp.Services;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Features.Leads.Services;

/// <summary>
/// Dispatches lead notifications across independent channels (WhatsApp, email).
/// Each channel runs in its own try/catch — one failure never blocks the others.
/// </summary>
public class MultiChannelLeadNotifier(
    IWhatsAppNotifier whatsAppNotifier,
    IEmailNotifier emailNotifier,
    IAgentConfigService configService,
    ILogger<MultiChannelLeadNotifier> logger)
{
    public async Task NotifyAgentAsync(string agentId, LeadNotification lead, CancellationToken ct)
    {
        // Load agent config once — used to determine which channels are active.
        // Config failure is logged but must not prevent email from being sent.
        var agentConfig = await LoadConfigAsync(agentId, ct);

        // ── WhatsApp channel ──────────────────────────────────────────────────
        try
        {
            if (agentConfig?.Integrations?.WhatsApp?.OptedIn == true)
            {
                var templateParams = WhatsAppMappers.ToNewLeadParams(
                    lead.Name, lead.Phone, lead.Email, lead.Interest, lead.Area);

                // WhatsAppMappers returns List<(string type, string value)> but
                // IWhatsAppNotifier expects Dictionary<string, string>.
                // Map by index so param ordering matches the template slots.
                var paramDict = templateParams
                    .Select((p, i) => (key: $"param_{i}", p.value))
                    .ToDictionary(x => x.key, x => x.value);

                await whatsAppNotifier.NotifyAsync(
                    agentId, NotificationType.NewLead, lead.Name, paramDict, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[LEAD-040] WhatsApp notification failed for {AgentId}", agentId);
            // Non-fatal — other channels continue
        }

        // ── Email channel ─────────────────────────────────────────────────────
        try
        {
            await emailNotifier.SendLeadNotificationAsync(agentId, lead, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[LEAD-041] Email notification failed for {AgentId}", agentId);
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<RealEstateStar.Api.Common.AgentConfig?> LoadConfigAsync(
        string agentId, CancellationToken ct)
    {
        try
        {
            return await configService.GetAgentAsync(agentId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[LEAD-039] Failed to load agent config for {AgentId} during lead notification", agentId);
            return null;
        }
    }
}
