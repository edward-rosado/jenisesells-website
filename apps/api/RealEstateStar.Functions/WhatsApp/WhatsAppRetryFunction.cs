using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Workers.WhatsApp;

namespace RealEstateStar.Functions.WhatsApp;

/// <summary>
/// Azure Functions timer-triggered replacement for WhatsAppRetryJob (BackgroundService).
/// Runs every 30 minutes to retry sending welcome messages to agents who opted in
/// but did not receive their welcome message (e.g. due to a transient WhatsApp API error).
///
/// TODO [Phase 4]: Remove WhatsAppRetryJob (BackgroundService) from Workers.WhatsApp
/// once feature flag Features:WhatsApp:UseBackgroundService is fully disabled in all environments.
/// </summary>
public class WhatsAppRetryFunction(
    WhatsAppRetryJob retryJob,
    ILogger<WhatsAppRetryFunction> logger)
{
    [Function("WhatsAppRetry")]
    public async Task RunAsync(
        [TimerTrigger("0 */30 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("[WA-FN-010] WhatsAppRetry timer fired. IsPastDue={IsPastDue}",
            timer.IsPastDue);

        try
        {
            await retryJob.ProcessRetriesAsync(cancellationToken);
            logger.LogInformation("[WA-FN-011] WhatsAppRetry completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WA-FN-012] WhatsAppRetry failed");
            throw;
        }
    }
}
