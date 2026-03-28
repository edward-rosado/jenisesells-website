using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using Stripe.Checkout;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class StripeServiceTests
{
    private const string TestPriceId = "price_test_123";
    private const string TestPlatformUrl = "https://app.example.com";

    private static IConfiguration BuildConfig(
        string? secretKey = "sk_test_fake",
        string? priceId = TestPriceId,
        string? webhookSecret = "whsec_test",
        string? platformUrl = TestPlatformUrl)
    {
        var pairs = new Dictionary<string, string?>();
        if (secretKey is not null) pairs["Stripe:SecretKey"] = secretKey;
        if (priceId is not null) pairs["Stripe:PriceId"] = priceId;
        if (webhookSecret is not null) pairs["Stripe:WebhookSecret"] = webhookSecret;
        if (platformUrl is not null) pairs["Platform:BaseUrl"] = platformUrl;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(pairs)
            .Build();
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_CallsStripeWithCorrectOptions()
    {
        var mockSessionService = new Mock<SessionService>();
        var expectedUrl = "https://checkout.stripe.com/c/pay_abc123";

        mockSessionService
            .Setup(s => s.CreateAsync(
                It.IsAny<SessionCreateOptions>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Session { Url = expectedUrl });

        var config = BuildConfig();
        var service = new StripeService(
            config,
            NullLogger<StripeService>.Instance,
            mockSessionService.Object);

        var session = OnboardingSession.Create(null);
        var result = await service.CreateCheckoutSessionAsync(
            session.Id, "agent@example.com", CancellationToken.None);

        Assert.Equal(expectedUrl, result);

        mockSessionService.Verify(s => s.CreateAsync(
            It.Is<SessionCreateOptions>(opts =>
                opts.Mode == "subscription" &&
                opts.CustomerEmail == "agent@example.com" &&
                opts.LineItems.Count == 1 &&
                opts.LineItems[0].Price == TestPriceId &&
                opts.LineItems[0].Quantity == 1 &&
                opts.SuccessUrl!.Contains("payment=success") &&
                opts.CancelUrl!.Contains("payment=cancelled") &&
                opts.PaymentIntentData == null &&
                opts.SubscriptionData!.TrialPeriodDays == 7 &&
                opts.Metadata["onboarding_session_id"] == session.Id),
            It.IsAny<RequestOptions>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_SetsCorrectSuccessUrl()
    {
        var mockSessionService = new Mock<SessionService>();
        SessionCreateOptions? capturedOptions = null;

        mockSessionService
            .Setup(s => s.CreateAsync(
                It.IsAny<SessionCreateOptions>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<SessionCreateOptions, RequestOptions?, CancellationToken>(
                (opts, _, _) => capturedOptions = opts)
            .ReturnsAsync(new Session { Url = "https://checkout.stripe.com/test" });

        var config = BuildConfig();
        var service = new StripeService(
            config,
            NullLogger<StripeService>.Instance,
            mockSessionService.Object);

        var session = OnboardingSession.Create(null);
        await service.CreateCheckoutSessionAsync(
            session.Id, "test@test.com", CancellationToken.None);

        Assert.NotNull(capturedOptions);
        Assert.Equal(
            $"{TestPlatformUrl}/onboard?payment=success&session_id={{CHECKOUT_SESSION_ID}}",
            capturedOptions!.SuccessUrl);
        Assert.Equal(
            $"{TestPlatformUrl}/onboard?payment=cancelled",
            capturedOptions.CancelUrl);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ThrowsWhenStripeReturnsNullUrl()
    {
        var mockSessionService = new Mock<SessionService>();
        mockSessionService
            .Setup(s => s.CreateAsync(
                It.IsAny<SessionCreateOptions>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Session { Url = null });

        var config = BuildConfig();
        var service = new StripeService(
            config,
            NullLogger<StripeService>.Instance,
            mockSessionService.Object);

        var session = OnboardingSession.Create(null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateCheckoutSessionAsync(
                session.Id, "test@test.com", CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsWhenSecretKeyMissing(string? secretKey)
    {
        var config = BuildConfig(secretKey: secretKey);

        Assert.Throws<InvalidOperationException>(
            () => new StripeService(
                config,
                NullLogger<StripeService>.Instance));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_ThrowsWhenPriceIdMissing(string? priceId)
    {
        var config = BuildConfig(priceId: priceId);

        Assert.Throws<InvalidOperationException>(
            () => new StripeService(
                config,
                NullLogger<StripeService>.Instance));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_ThrowsWhenWebhookSecretMissing(string? webhookSecret)
    {
        var config = BuildConfig(webhookSecret: webhookSecret);

        Assert.Throws<InvalidOperationException>(
            () => new StripeService(
                config,
                NullLogger<StripeService>.Instance));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_ThrowsWhenPlatformUrlMissing(string? platformUrl)
    {
        var config = BuildConfig(platformUrl: platformUrl);

        Assert.Throws<InvalidOperationException>(
            () => new StripeService(
                config,
                NullLogger<StripeService>.Instance));
    }

    // --- Missing branch coverage ---

    [Fact]
    public void ConstructWebhookEvent_InvalidSignature_ThrowsStripeException()
    {
        var mockSessionService = new Mock<SessionService>();
        var config = BuildConfig();
        var service = new StripeService(
            config,
            NullLogger<StripeService>.Instance,
            mockSessionService.Object);

        // A real Stripe webhook signature validation will throw StripeException
        // when the payload and signature don't match the secret
        Assert.Throws<StripeException>(
            () => service.ConstructWebhookEvent(
                payload: """{"id":"evt_test","type":"checkout.session.completed","data":{}}""",
                signatureHeader: "t=1234567890,v1=invalidsignature"));
    }

    // NOTE: ConstructWebhookEvent happy-path return cannot be unit-tested because
    // Stripe.net v50 EventConverter.ReadJson throws NullReferenceException when deserializing
    // hand-crafted event JSON — even with correct api_version and StripeConfiguration initialized.
    // The SDK's thin-model architecture requires internal serializer context that only exists when
    // events are received through the real Stripe API. The method IS executed by the invalid-signature
    // test (line hit, throws before return). The StripeWebhookEndpoint tests cover the full pipeline
    // with mock IStripeService, which bypasses EventUtility.ConstructEvent entirely.

    [Fact]
    public void Constructor_WithWhitespacePlatformUrl_Throws()
    {
        // IsNullOrWhiteSpace covers the whitespace branch not hit by null/"" tests
        var config = BuildConfig(platformUrl: "   ");

        Assert.Throws<InvalidOperationException>(
            () => new StripeService(
                config,
                NullLogger<StripeService>.Instance));
    }

    [Fact]
    public void Constructor_WithWhitespaceSecretKey_Throws()
    {
        var config = BuildConfig(secretKey: "   ");

        Assert.Throws<InvalidOperationException>(
            () => new StripeService(
                config,
                NullLogger<StripeService>.Instance));
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_SetsIdempotencyKey()
    {
        var mockSessionService = new Mock<SessionService>();
        RequestOptions? capturedOptions = null;

        mockSessionService
            .Setup(s => s.CreateAsync(
                It.IsAny<SessionCreateOptions>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<SessionCreateOptions, RequestOptions?, CancellationToken>(
                (_, opts, _) => capturedOptions = opts)
            .ReturnsAsync(new Session { Url = "https://checkout.stripe.com/test" });

        var config = BuildConfig();
        var service = new StripeService(
            config,
            NullLogger<StripeService>.Instance,
            mockSessionService.Object);

        var session = OnboardingSession.Create(null);
        await service.CreateCheckoutSessionAsync(
            session.Id, "test@test.com", CancellationToken.None);

        Assert.NotNull(capturedOptions);
        Assert.Equal(session.Id, capturedOptions!.IdempotencyKey);
    }

    [Fact]
    public void InternalConstructor_WithWhitespaceSecretKey_Throws()
    {
        // The internal constructor only validates SecretKey via the shared config check
        var mockSessionService = new Mock<SessionService>();
        var config = BuildConfig(secretKey: "   ");

        Assert.Throws<InvalidOperationException>(
            () => new StripeService(
                config,
                NullLogger<StripeService>.Instance,
                mockSessionService.Object));
    }

    [Fact]
    public void InternalConstructor_WithAllValid_Succeeds()
    {
        var mockSessionService = new Mock<SessionService>();
        var config = BuildConfig();

        var service = new StripeService(
            config,
            NullLogger<StripeService>.Instance,
            mockSessionService.Object);

        Assert.NotNull(service);
    }
}
