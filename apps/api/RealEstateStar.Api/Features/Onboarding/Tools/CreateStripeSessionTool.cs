using System.Text.Json;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Services;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class CreateStripeSessionTool(Services.IStripeService stripeService) : IOnboardingTool
{
    public string Name => "create_stripe_session";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        // MED-3: Validate email before passing to Stripe
        var email = session.Profile?.Email;
        if (string.IsNullOrWhiteSpace(email))
            return JsonSerializer.Serialize(new { error = "Agent email is required before creating a payment session. Please complete your profile first." });

        var checkoutUrl = await stripeService.CreateCheckoutSessionAsync(session.Id, email, ct);
        return JsonSerializer.Serialize(new { checkoutUrl });
    }
}
