using Stripe;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public interface IStripeService
{
    Task<string> CreateCheckoutSessionAsync(string sessionId, string agentEmail, CancellationToken ct);
    Event ConstructWebhookEvent(string payload, string signatureHeader);
}
