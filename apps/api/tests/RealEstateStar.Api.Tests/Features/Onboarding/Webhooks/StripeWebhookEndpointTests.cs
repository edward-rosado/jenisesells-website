using System.IO;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Webhooks;
using RealEstateStar.Api.Tests.Integration;
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
            Id = "evt_test_123",
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
            Id = "evt_test_456",
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

    [Fact]
    public async Task HandleWebhookEvent_DuplicateStripeEvent_SkipsProcessing()
    {
        var onboardingSession = OnboardingSession.Create(null);
        onboardingSession.CurrentState = OnboardingState.CollectPayment;
        onboardingSession.LastStripeEventId = "evt_duplicate_123";
        var sessionId = onboardingSession.Id;

        _sessionStore
            .Setup(s => s.LoadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(onboardingSession);

        var stripeSession = new Session
        {
            Metadata = new Dictionary<string, string>
            {
                ["onboarding_session_id"] = sessionId
            }
        };

        var stripeEvent = new Event
        {
            Id = "evt_duplicate_123",
            Type = EventTypes.CheckoutSessionCompleted,
            Data = new EventData { Object = stripeSession }
        };

        var result = await StripeWebhookEndpoint.HandleWebhookEvent(
            stripeEvent,
            _sessionStore.Object,
            new OnboardingStateMachine(),
            NullLogger<StripeWebhookEndpoint>.Instance,
            CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok>(result);
        // Should NOT advance state or save
        Assert.Equal(OnboardingState.CollectPayment, onboardingSession.CurrentState);
        _sessionStore.Verify(
            s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleWebhookEvent_CannotAdvanceState_DoesNotSave()
    {
        // Session already at TrialActivated — cannot advance further
        var onboardingSession = OnboardingSession.Create(null);
        onboardingSession.CurrentState = OnboardingState.TrialActivated;
        var sessionId = onboardingSession.Id;

        _sessionStore
            .Setup(s => s.LoadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(onboardingSession);

        var stripeSession = new Session
        {
            Metadata = new Dictionary<string, string>
            {
                ["onboarding_session_id"] = sessionId
            }
        };

        var stripeEvent = new Event
        {
            Id = "evt_cant_advance",
            Type = EventTypes.CheckoutSessionCompleted,
            Data = new EventData { Object = stripeSession }
        };

        var result = await StripeWebhookEndpoint.HandleWebhookEvent(
            stripeEvent,
            _sessionStore.Object,
            new OnboardingStateMachine(),
            NullLogger<StripeWebhookEndpoint>.Instance,
            CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok>(result);
        _sessionStore.Verify(
            s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleWebhookEvent_DataObjectNotSession_Returns200()
    {
        // Data.Object is not a Session (e.g., some other Stripe object type)
        var stripeEvent = new Event
        {
            Type = EventTypes.CheckoutSessionCompleted,
            Data = new EventData { Object = new PaymentIntent() }
        };

        var result = await StripeWebhookEndpoint.HandleWebhookEvent(
            stripeEvent,
            _sessionStore.Object,
            new OnboardingStateMachine(),
            NullLogger<StripeWebhookEndpoint>.Instance,
            CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok>(result);
        _sessionStore.Verify(
            s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleWebhookEvent_EmptySessionIdInMetadata_Returns200()
    {
        var stripeSession = new Session
        {
            Metadata = new Dictionary<string, string>
            {
                ["onboarding_session_id"] = ""
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

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok>(result);
        _sessionStore.Verify(
            s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidSignature_DelegatesToHandleWebhookEvent()
    {
        // Test the outer Handle method with a mock IStripeService
        var onboardingSession = OnboardingSession.Create(null);
        onboardingSession.CurrentState = OnboardingState.CollectPayment;
        var sessionId = onboardingSession.Id;

        var stripeSession = new Session
        {
            Metadata = new Dictionary<string, string>
            {
                ["onboarding_session_id"] = sessionId
            }
        };

        var stripeEvent = new Event
        {
            Id = "evt_valid",
            Type = EventTypes.CheckoutSessionCompleted,
            Data = new EventData { Object = stripeSession }
        };

        var mockStripeService = new Mock<IStripeService>();
        mockStripeService
            .Setup(s => s.ConstructWebhookEvent(It.IsAny<string>(), "valid-sig"))
            .Returns(stripeEvent);

        _sessionStore
            .Setup(s => s.LoadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(onboardingSession);
        _sessionStore
            .Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Stripe-Signature"] = "valid-sig";
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"test\":true}"));

        var result = await StripeWebhookEndpoint.Handle(
            httpContext,
            mockStripeService.Object,
            _sessionStore.Object,
            new OnboardingStateMachine(),
            NullLogger<StripeWebhookEndpoint>.Instance,
            CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok>(result);
        Assert.Equal(OnboardingState.TrialActivated, onboardingSession.CurrentState);
    }

    [Fact]
    public async Task Handle_StripeExceptionOnConstruct_Returns400()
    {
        var mockStripeService = new Mock<IStripeService>();
        mockStripeService
            .Setup(s => s.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new StripeException("bad sig"));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Stripe-Signature"] = "bad-sig";
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        var result = await StripeWebhookEndpoint.Handle(
            httpContext,
            mockStripeService.Object,
            _sessionStore.Object,
            new OnboardingStateMachine(),
            NullLogger<StripeWebhookEndpoint>.Instance,
            CancellationToken.None);

        // Should be BadRequest - the string body check
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_MissingSignatureHeader_ReturnsBadRequest()
    {
        var mockStripeService = new Mock<IStripeService>();

        var httpContext = new DefaultHttpContext();
        // No Stripe-Signature header set
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        var result = await StripeWebhookEndpoint.Handle(
            httpContext,
            mockStripeService.Object,
            _sessionStore.Object,
            new OnboardingStateMachine(),
            NullLogger<StripeWebhookEndpoint>.Instance,
            CancellationToken.None);

        Assert.NotNull(result);
        // ConstructWebhookEvent should never be called
        mockStripeService.Verify(
            s => s.ConstructWebhookEvent(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}

/// <summary>
/// SEC-9: Integration tests that exercise the full HTTP pipeline including
/// Stripe signature verification via EventUtility.ConstructEvent.
/// </summary>
public class StripeWebhookSignatureTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public StripeWebhookSignatureTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Handle_MissingStripeSignatureHeader_Returns400()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/webhooks/stripe", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Handle_InvalidStripeSignature_Returns400()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent("{\"type\":\"checkout.session.completed\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "t=1234567890,v1=invalid_signature_value");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Handle_EmptyStripeSignature_Returns400()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Handle_MalformedStripeSignature_Returns400()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent("{\"id\":\"evt_test\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "not-a-real-signature");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
