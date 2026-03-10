using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class CreateStripeSessionTool(Services.IStripeService stripeService) : IOnboardingTool
{
    public string Name => "create_stripe_session";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        var email = session.Profile?.Email ?? "";
        var checkoutUrl = await stripeService.CreateCheckoutSessionAsync(session.Id, email, ct);
        return JsonSerializer.Serialize(new { checkoutUrl });
    }
}
