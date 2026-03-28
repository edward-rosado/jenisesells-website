using Stripe;
using Stripe.Checkout;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class StripeService : IStripeService
{
    private readonly SessionService _sessionService;
    private readonly string _priceId;
    private readonly string _platformUrl;
    private readonly string _webhookSecret;
    private readonly ILogger<StripeService> _logger;

    public StripeService(
        IConfiguration configuration,
        ILogger<StripeService> logger)
        : this(
            configuration,
            logger,
            CreateSessionService(configuration))
    {
    }

    internal StripeService(
        IConfiguration configuration,
        ILogger<StripeService> logger,
        SessionService sessionService)
    {
        _logger = logger;
        _sessionService = sessionService;

        // LOW-3: Stripe key validation also happens in Program.cs startup — this is intentional defense-in-depth
        var secretKey = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("Stripe:SecretKey configuration is required");

        _priceId = configuration["Stripe:PriceId"]
            ?? throw new InvalidOperationException("Stripe:PriceId configuration is required");
        if (string.IsNullOrEmpty(_priceId))
            throw new InvalidOperationException("Stripe:PriceId configuration is required");

        _webhookSecret = configuration["Stripe:WebhookSecret"]
            ?? throw new InvalidOperationException("Stripe:WebhookSecret configuration is required");
        if (string.IsNullOrEmpty(_webhookSecret))
            throw new InvalidOperationException("Stripe:WebhookSecret configuration is required");

        var platformUrl = configuration["Platform:BaseUrl"];
        if (string.IsNullOrWhiteSpace(platformUrl))
            throw new InvalidOperationException("Platform:BaseUrl configuration is required");
        _platformUrl = platformUrl;
    }

    public Event ConstructWebhookEvent(string payload, string signatureHeader)
    {
        return EventUtility.ConstructEvent(payload, signatureHeader, _webhookSecret);
    }

    public async Task<string> CreateCheckoutSessionAsync(
        string sessionId,
        string agentEmail,
        CancellationToken ct)
    {
        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            CustomerEmail = agentEmail,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = _priceId,
                    Quantity = 1,
                }
            ],
            SuccessUrl = $"{_platformUrl}/onboard?payment=success&session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{_platformUrl}/onboard?payment=cancelled",
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                TrialPeriodDays = 7,
            },
            Metadata = new Dictionary<string, string>
            {
                ["onboarding_session_id"] = sessionId,
            },
        };

        _logger.LogInformation(
            "Creating Stripe Checkout session for onboarding {SessionId}",
            sessionId);

        // LOW-2: Idempotency key prevents duplicate Stripe sessions on retries
        var requestOptions = new RequestOptions { IdempotencyKey = sessionId };
        var checkoutSession = await _sessionService.CreateAsync(options, requestOptions, ct);

        return checkoutSession.Url
            ?? throw new InvalidOperationException("Stripe returned a session without a checkout URL");
    }

    private static SessionService CreateSessionService(IConfiguration configuration)
    {
        var secretKey = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("Stripe:SecretKey configuration is required");

        var client = new StripeClient(secretKey);
        return new SessionService(client);
    }
}
