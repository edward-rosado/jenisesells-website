using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Webhooks;
using Stripe;
using Stripe.Checkout;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Webhooks;

public class StripeWebhookEndpointTests
{
    private readonly Mock<ISessionStore> _sessionStore = new();
    private readonly Mock<OnboardingStateMachine> _stateMachine = new();
    private const string WebhookSecret = "whsec_test_secret";

    private static IConfiguration BuildConfig(string webhookSecret = WebhookSecret) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:WebhookSecret"] = webhookSecret,
            })
            .Build();

    [Fact]
    public async Task Handle_CheckoutSessionCompleted_AdvancesToTrialActivated()
    {
        var onboardingSession = OnboardingSession.Create(null);
        onboardingSession.CurrentState = OnboardingState.CollectPayment;
        var sessionId = onboardingSession.Id;

        _sessionStore
            .Setup(s => s.LoadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(onboardingSession);
        _sessionStore
            .Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var stripeSession = new Session
        {
            Metadata = new Dictionary<string, string>
            {
                ["onboarding_session_id"] = sessionId
            }
        };

        var stripeEvent = new Event
        {
            Type = EventTypes.CheckoutSessionCompleted,
            Data = new EventData { Object = stripeSession }
        };

        var result = await StripeWebhookEndpoint.HandleWebhookEvent(
            stripeEvent,
            _sessionStore.Object,
            new OnboardingStateMachine(),
            NullLogger<StripeWebhookEndpoint>.Instance,
            CancellationToken.None);

        Assert.Equal(OnboardingState.TrialActivated, onboardingSession.CurrentState);
        _sessionStore.Verify(
            s => s.SaveAsync(onboardingSession, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CheckoutSessionCompleted_Returns200()
    {
        var onboardingSession = OnboardingSession.Create(null);
        onboardingSession.CurrentState = OnboardingState.CollectPayment;
        var sessionId = onboardingSession.Id;

        _sessionStore
            .Setup(s => s.LoadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(onboardingSession);
        _sessionStore
            .Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var stripeSession = new Session
        {
            Metadata = new Dictionary<string, string>
            {
                ["onboarding_session_id"] = sessionId
            }
        };

        var stripeEvent = new Event
        {
            Type = EventTypes.CheckoutSessionCompleted,
            Data = new EventData { Object = stripeSession }
        };

        var result = await StripeWebhookEndpoint.HandleWebhookEvent(
            stripeEvent,
            _sessionStore.Object,
            new OnboardingStateMachine(),
            NullLogger<StripeWebhookEndpoint>.Instance,
            CancellationToken.None);

        var okResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok>(result);
    }

    [Fact]
    public async Task Handle_UnhandledEventType_Returns200WithoutProcessing()
    {
        var stripeEvent = new Event
        {
            Type = "customer.subscription.updated",
            Data = new EventData()
        };

        var result = await StripeWebhookEndpoint.HandleWebhookEvent(
            stripeEvent,
            _sessionStore.Object,
            new OnboardingStateMachine(),
            NullLogger<StripeWebhookEndpoint>.Instance,
            CancellationToken.None);

        var okResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok>(result);
        _sessionStore.Verify(
            s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SessionNotFound_Returns200WithoutCrash()
    {
        _sessionStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var stripeSession = new Session
        {
            Metadata = new Dictionary<string, string>
            {
                ["onboarding_session_id"] = "nonexistent"
            }
        };

        var stripeEvent = new Event
        {
            Type = EventTypes.CheckoutSessionCompleted,
            Data = new EventData { Object = stripeSession }
        };

        var result = await StripeWebhookEndpoint.HandleWebhookEvent(
            stripeEvent,
            _sessionStore.Object,
            new OnboardingStateMachine(),
            NullLogger<StripeWebhookEndpoint>.Instance,
            CancellationToken.None);

        // Webhook should return 200 even if session not found (avoid Stripe retries)
        var okResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok>(result);
    }

    [Fact]
    public async Task Handle_MissingSessionIdInMetadata_Returns200()
    {
        var stripeSession = new Session
        {
            Metadata = new Dictionary<string, string>()
        };

        var stripeEvent = new Event
        {
            Type = EventTypes.CheckoutSessionCompleted,
            Data = new EventData { Object = stripeSession }
        };

        var result = await StripeWebhookEndpoint.HandleWebhookEvent(
            stripeEvent,
            _sessionStore.Object,
            new OnboardingStateMachine(),
            NullLogger<StripeWebhookEndpoint>.Instance,
            CancellationToken.None);

        var okResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok>(result);
        _sessionStore.Verify(
            s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
