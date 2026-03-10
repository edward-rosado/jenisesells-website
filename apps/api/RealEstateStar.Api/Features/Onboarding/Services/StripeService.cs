using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class StripeService(ILogger<StripeService> logger)
{
    public Task<string> CreateSetupIntentAsync(OnboardingSession session, CancellationToken ct)
    {
        // TODO: Wire Stripe.net SDK to create a real SetupIntent.
        var stubIntentId = $"seti_stub_{session.Id}";
        session.StripeSetupIntentId = stubIntentId;

        logger.LogInformation("Created Stripe SetupIntent {IntentId} for session {SessionId}",
            stubIntentId, session.Id);

        return Task.FromResult(stubIntentId);
    }

    public Task<bool> ChargeAsync(string setupIntentId, decimal amount, CancellationToken ct)
    {
        // TODO: Wire Stripe.net SDK to create a PaymentIntent from the SetupIntent.
        logger.LogInformation("Charging {Amount} via SetupIntent {IntentId}", amount, setupIntentId);
        return Task.FromResult(true);
    }
}
