using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class CreateStripeSessionTool(Services.StripeService stripeService) : IOnboardingTool
{
    public string Name => "create_stripe_session";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        var intentId = await stripeService.CreateSetupIntentAsync(session, ct);
        return $"Stripe SetupIntent created: {intentId}. The payment card is ready for the agent.";
    }
}
