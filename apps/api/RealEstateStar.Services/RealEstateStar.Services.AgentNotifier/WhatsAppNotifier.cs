using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.WhatsApp.Models;

namespace RealEstateStar.Services.AgentNotifier;

public class WhatsAppNotifier(
    IWhatsAppSender client,
    IConversationLogger conversationLogger,
    IConfigDataService configService,
    IMemoryCache cache,
    ILogger<WhatsAppNotifier> logger) : IWhatsAppNotifier
{
    private static readonly TimeSpan WindowDuration = TimeSpan.FromHours(24);

    private static readonly Dictionary<NotificationType, string> TemplateNames = new()
    {
        [NotificationType.NewLead] = "new_lead_notification",
        [NotificationType.CmaReady] = "cma_ready",
        [NotificationType.FollowUpReminder] = "follow_up_reminder",
        [NotificationType.DataDeletion] = "data_deletion_notice",
        [NotificationType.ListingAlert] = "listing_alert",
        [NotificationType.Welcome] = "welcome"
    };

    // Preference key names match the JSON config values (snake_case)
    private static readonly Dictionary<NotificationType, string> PreferenceKeys = new()
    {
        [NotificationType.NewLead] = "new_lead",
        [NotificationType.CmaReady] = "cma_ready",
        [NotificationType.FollowUpReminder] = "follow_up_reminder",
        [NotificationType.DataDeletion] = "data_deletion",
        [NotificationType.ListingAlert] = "listing_alert"
    };

    public async Task NotifyAsync(string agentId, NotificationType type,
        string? leadName, Dictionary<string, string> templateParams,
        CancellationToken ct)
    {
        AccountConfig? config;
        try
        {
            config = await configService.GetAccountAsync(agentId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[WA-015] Failed to load agent config for {AgentId}", agentId);
            return;
        }

        var whatsApp = config?.Integrations?.WhatsApp;
        if (whatsApp is null || !whatsApp.OptedIn)
            return;

        // DataDeletion bypasses preference check — always send
        if (type != NotificationType.DataDeletion)
        {
            if (PreferenceKeys.TryGetValue(type, out var key) &&
                !whatsApp.NotificationPreferences.Contains(key))
                return;
        }

        var agentPhone = whatsApp.PhoneNumber;

        // Welcome flow — send welcome template first if not yet sent
        if (!whatsApp.WelcomeSent)
        {
            try
            {
                var firstName = config!.Agent?.Name.Split(' ').FirstOrDefault() ?? "there";
                var welcomeParams = WhatsAppMappers.ToWelcomeParams(firstName);
                await client.SendTemplateAsync(agentPhone, "welcome", welcomeParams, ct);

                var updatedWhatsApp = new AccountWhatsApp
                {
                    PhoneNumber = whatsApp.PhoneNumber,
                    OptedIn = whatsApp.OptedIn,
                    WelcomeSent = true,
                    NotificationPreferences = whatsApp.NotificationPreferences,
                    Status = whatsApp.Status,
                    RetryAfter = whatsApp.RetryAfter
                };
                var updatedConfig = new AccountConfig
                {
                    Handle = config!.Handle,
                    Agent = config.Agent,
                    Location = config.Location,
                    Branding = config.Branding,
                    Compliance = config.Compliance,
                    Integrations = new AccountIntegrations
                    {
                        EmailProvider = config.Integrations!.EmailProvider,
                        Hosting = config.Integrations.Hosting,
                        FormHandler = config.Integrations.FormHandler,
                        FormHandlerId = config.Integrations.FormHandlerId,
                        WhatsApp = updatedWhatsApp
                    }
                };

                await configService.UpdateAccountAsync(agentId, updatedConfig, ct);

                await conversationLogger.LogMessagesAsync(agentId, leadName,
                    [(DateTime.UtcNow, "system", "welcome", "welcome")], ct);
            }
            catch (WhatsAppNotRegisteredException ex)
            {
                logger.LogWarning(ex, "[WA-040] Agent {AgentId} phone {Phone} not registered on WhatsApp",
                    agentId, agentPhone);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[WA-015] Failed to send welcome to agent {AgentId}", agentId);
                return;
            }
        }

        // Determine send strategy based on 24hr window
        string messageId;
        string body;
        string? templateName = null;

        try
        {
            if (IsWindowOpen(agentPhone))
            {
                body = BuildFreeformBody(type, leadName, templateParams);
                messageId = await client.SendFreeformAsync(agentPhone, body, ct);
            }
            else
            {
                templateName = TemplateNames.GetValueOrDefault(type, type.ToString().ToLowerInvariant());
                var @params = BuildTemplateParams(type, leadName, templateParams);
                messageId = await client.SendTemplateAsync(agentPhone, templateName, @params, ct);
                body = $"[template:{templateName}]";
            }
        }
        catch (WhatsAppNotRegisteredException ex)
        {
            logger.LogWarning(ex, "[WA-041] Agent {AgentId} phone {Phone} not registered on WhatsApp",
                agentId, agentPhone);
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WA-015] Failed to send {Type} notification to agent {AgentId}",
                type, agentId);
            return;
        }

        // Log after successful send
        await conversationLogger.LogMessagesAsync(agentId, leadName,
            [(DateTime.UtcNow, "system", body, templateName)], ct);
    }

    public void RecordAgentMessage(string agentPhone)
    {
        var cacheKey = $"wa:window:{agentPhone}";
        _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
        {
            SlidingExpiration = WindowDuration
        });
    }

    public bool IsWindowOpen(string agentPhone)
    {
        var cacheKey = $"wa:window:{agentPhone}";
        return _cache.TryGetValue(cacheKey, out _);
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private static List<(string type, string value)> BuildTemplateParams(
        NotificationType type, string? leadName, Dictionary<string, string> @params)
    {
        return type switch
        {
            NotificationType.NewLead => WhatsAppMappers.ToNewLeadParams(
                leadName ?? @params.GetValueOrDefault("lead_name", ""),
                @params.GetValueOrDefault("phone", ""),
                @params.GetValueOrDefault("email", ""),
                @params.GetValueOrDefault("interest", ""),
                @params.GetValueOrDefault("area", "")),

            NotificationType.CmaReady => WhatsAppMappers.ToCmaReadyParams(
                leadName ?? @params.GetValueOrDefault("lead_name", ""),
                @params.GetValueOrDefault("address", ""),
                @params.GetValueOrDefault("estimated_value", "")),

            NotificationType.FollowUpReminder => WhatsAppMappers.ToFollowUpParams(
                leadName ?? @params.GetValueOrDefault("lead_name", ""),
                int.TryParse(@params.GetValueOrDefault("days", "0"), out var d) ? d : 0),

            NotificationType.DataDeletion => WhatsAppMappers.ToDataDeletionParams(
                leadName ?? @params.GetValueOrDefault("lead_name", ""),
                DateTime.TryParse(@params.GetValueOrDefault("deletion_deadline", ""), out var dt)
                    ? dt : DateTime.UtcNow.AddDays(30)),

            _ => []
        };
    }

    private static string BuildFreeformBody(
        NotificationType type, string? leadName, Dictionary<string, string> @params)
    {
        var name = leadName ?? @params.GetValueOrDefault("lead_name", "someone");
        return type switch
        {
            NotificationType.NewLead =>
                $"New lead: {name} | {Param("phone")} | {Param("email")} | {Param("interest")} | {Param("area")}",
            NotificationType.CmaReady =>
                $"CMA ready for {name} — {Param("address")} | Est. value: {Param("estimated_value")}",
            NotificationType.FollowUpReminder =>
                $"Follow-up reminder: {name} — {Param("days")} days since submission",
            NotificationType.DataDeletion =>
                $"Data deletion notice for {name}. Deadline: {Param("deletion_deadline")}",
            _ => $"{type} notification for {name}"
        };

        string Param(string key) => @params.GetValueOrDefault(key, "");
    }

    // Satisfy the field reference from constructor parameter — store the cache
    private readonly IMemoryCache _cache = cache;
}
