using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.WhatsApp.Services;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class SendWhatsAppWelcomeTool(
    IWhatsAppSender whatsAppClient,
    IAccountConfigService accountConfigService,
    ILogger<SendWhatsAppWelcomeTool> logger) : IOnboardingTool
{
    public string Name => "send_whatsapp_welcome";

    public async Task<string> ExecuteAsync(JsonElement parameters,
        OnboardingSession session, CancellationToken ct)
    {
        var accountConfig = await accountConfigService.GetAccountAsync(session.AgentConfigId!, ct);
        var whatsApp = accountConfig?.Integrations?.WhatsApp;

        if (whatsApp is null || !whatsApp.OptedIn || string.IsNullOrEmpty(whatsApp.PhoneNumber))
            return "WhatsApp not configured — I'll send everything via email instead.";

        var firstName = accountConfig!.Agent?.Name?.Split(' ')[0] ?? "there";

        try
        {
            await whatsAppClient.SendTemplateAsync(
                whatsApp.PhoneNumber,
                "welcome_onboarding",
                [("text", firstName)],
                ct);

            whatsApp.Status = "active";
            whatsApp.WelcomeSent = true;
            await accountConfigService.UpdateAccountAsync(session.AgentConfigId!, accountConfig, ct);

            return $"Welcome message sent to WhatsApp! Check your phone at {whatsApp.PhoneNumber}.";
        }
        catch (WhatsAppNotRegisteredException)
        {
            whatsApp.Status = "not_registered";
            whatsApp.RetryAfter = DateTime.UtcNow.AddHours(4);
            await accountConfigService.UpdateAccountAsync(session.AgentConfigId!, accountConfig, ct);

            logger.LogInformation("[WA-014] Agent {AgentId} not on WhatsApp, retry scheduled",
                session.AgentConfigId);
            return "It looks like you haven't set up WhatsApp yet. No worries — I'll try again in a few hours. In the meantime, all notifications will go to your email.";
        }
        catch (Exception ex)
        {
            whatsApp.Status = "error";
            await accountConfigService.UpdateAccountAsync(session.AgentConfigId!, accountConfig, ct);
            logger.LogError(ex, "[WA-014] WhatsApp welcome failed for {AgentId}", session.AgentConfigId);
            return "WhatsApp setup hit a snag. I'll send everything via email for now.";
        }
    }
}
