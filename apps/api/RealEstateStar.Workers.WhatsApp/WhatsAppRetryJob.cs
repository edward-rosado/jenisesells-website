using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.WhatsApp;

public class WhatsAppRetryJob(
    IAccountConfigService accountConfigService,
    IWhatsAppSender whatsAppClient,
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
        var accounts = await accountConfigService.ListAllAsync(ct);

        foreach (var config in accounts)
        {
            var wa = config.Integrations?.WhatsApp;

            if (wa is null || !wa.OptedIn || wa.WelcomeSent)
                continue;

            if (wa.RetryAfter is null || wa.RetryAfter > DateTime.UtcNow)
                continue;

            try
            {
                var firstName = config.Agent?.Name.Split(' ').FirstOrDefault() ?? config.Agent?.Name ?? config.Handle;

                await whatsAppClient.SendTemplateAsync(
                    wa.PhoneNumber,
                    "welcome_onboarding",
                    [("text", firstName)],
                    ct);

                wa.Status = "active";
                wa.WelcomeSent = true;
                wa.RetryAfter = null;

                await accountConfigService.UpdateAccountAsync(config.Handle, config, ct);
                logger.LogInformation("[WA-013] Welcome retry succeeded for {Handle}", config.Handle);
            }
            catch (Exception)
            {
                // Single retry only — clear retry_after to prevent loops
                wa.RetryAfter = null;
                await accountConfigService.UpdateAccountAsync(config.Handle, config, ct);
                logger.LogWarning("[WA-014] Welcome retry failed for {Handle}, no further retries", config.Handle);
            }
        }
    }
}
