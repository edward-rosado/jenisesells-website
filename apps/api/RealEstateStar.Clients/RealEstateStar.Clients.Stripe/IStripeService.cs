using Stripe;

namespace RealEstateStar.Clients.Stripe;

public interface IStripeService
{
    Task<string> CreateCheckoutSessionAsync(string sessionId, string agentEmail, CancellationToken ct);
    Event ConstructWebhookEvent(string payload, string signatureHeader);
}
