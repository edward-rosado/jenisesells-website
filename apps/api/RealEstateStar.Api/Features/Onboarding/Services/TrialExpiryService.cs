namespace RealEstateStar.Api.Features.Onboarding.Services;

// TODO: MED-5 — Implement actual trial expiry logic (check CreatedAt + 7 days, notify agent, deactivate site)
// TODO: MED-12 — Use IServiceScopeFactory for future scoped dependency injection
public class TrialExpiryService(
    ISessionStore sessionStore,
    IStripeService stripeService,
    ILogger<TrialExpiryService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan TrialDuration = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Trial expiry service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
                // TODO: Enumerate sessions, find expired trials, charge via Stripe.
                // This is a stub — real implementation reads all sessions from the store,
                // checks CreatedAt + TrialDuration, and calls stripeService.ChargeAsync.
                logger.LogDebug("Trial expiry check completed");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
