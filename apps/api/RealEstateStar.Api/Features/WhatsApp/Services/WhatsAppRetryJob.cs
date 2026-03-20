using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Features.WhatsApp.Services;

public class WhatsAppRetryJob(
    IAgentConfigService agentConfigService,
    IWhatsAppClient whatsAppClient,
    ILogger<WhatsAppRetryJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRetries(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[WA-023] Retry job error");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    internal async Task ProcessRetries(CancellationToken ct)
    {
        var agentIds = await agentConfigService.GetAllAgentIdsAsync(ct);

        foreach (var agentId in agentIds)
        {
            var config = await agentConfigService.GetAgentAsync(agentId, ct);
            var wa = config?.Integrations?.WhatsApp;

            if (wa is null || !wa.OptedIn || wa.WelcomeSent)
                continue;

            if (wa.RetryAfter is null || wa.RetryAfter > DateTime.UtcNow)
                continue;

            try
            {
                var firstName = config!.Identity?.Name.Split(' ').FirstOrDefault() ?? config.Identity?.Name ?? agentId;

                await whatsAppClient.SendTemplateAsync(
                    wa.PhoneNumber,
                    "welcome_onboarding",
                    [("text", firstName)],
                    ct);

                wa.Status = "active";
                wa.WelcomeSent = true;
                wa.RetryAfter = null;

                await agentConfigService.UpdateAgentAsync(agentId, config!, ct);
                logger.LogInformation("[WA-013] Welcome retry succeeded for {AgentId}", agentId);
            }
            catch (Exception)
            {
                // Single retry only — clear retry_after to prevent loops
                wa.RetryAfter = null;
                await agentConfigService.UpdateAgentAsync(agentId, config!, ct);
                logger.LogWarning("[WA-014] Welcome retry failed for {AgentId}, no further retries", agentId);
            }
        }
    }
}
