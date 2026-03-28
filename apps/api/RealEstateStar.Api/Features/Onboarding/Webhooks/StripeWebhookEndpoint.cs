using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;
using Stripe;
using Stripe.Checkout;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Services;

namespace RealEstateStar.Api.Features.Onboarding.Webhooks;

public class StripeWebhookEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapPost("/webhooks/stripe", Handle)
            .DisableAntiforgery()
            .DisableRateLimiting();
    }

    internal static async Task<IResult> Handle(
        HttpContext httpContext,
        IStripeService stripeService,
        ISessionDataService sessionStore,
        OnboardingStateMachine stateMachine,
        ILogger<StripeWebhookEndpoint> logger,
        CancellationToken ct)
    {
        var signatureHeader = httpContext.Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrEmpty(signatureHeader))
        {
            logger.LogWarning("Stripe webhook request missing Stripe-Signature header");
            return Results.BadRequest("Invalid signature");
        }

        var json = await new StreamReader(httpContext.Request.Body).ReadToEndAsync(ct);

        Event stripeEvent;
        try
        {
            stripeEvent = stripeService.ConstructWebhookEvent(json, signatureHeader);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return Results.BadRequest("Invalid signature");
        }

        return await HandleWebhookEvent(stripeEvent, sessionStore, stateMachine, logger, ct);
    }

    internal static async Task<IResult> HandleWebhookEvent(
        Event stripeEvent,
        ISessionDataService sessionStore,
        OnboardingStateMachine stateMachine,
        ILogger<StripeWebhookEndpoint> logger,
        CancellationToken ct)
    {
        if (stripeEvent.Type != EventTypes.CheckoutSessionCompleted)
        {
            logger.LogInformation("Ignoring Stripe event type {EventType}", stripeEvent.Type);
            return Results.Ok();
        }

        if (stripeEvent.Data.Object is not Session checkoutSession)
        {
            logger.LogWarning("checkout.session.completed event missing Session object");
            return Results.Ok();
        }

        if (!checkoutSession.Metadata.TryGetValue("onboarding_session_id", out var onboardingSessionId)
            || string.IsNullOrEmpty(onboardingSessionId))
        {
            logger.LogWarning("Stripe checkout session missing onboarding_session_id in metadata");
            return Results.Ok();
        }

        var session = await sessionStore.LoadAsync(onboardingSessionId, ct);
        if (session is null)
        {
            logger.LogWarning("Onboarding session {SessionId} not found for Stripe webhook", onboardingSessionId);
            return Results.Ok();
        }

        // SEC-7: Idempotency — skip if we already processed this event
        if (session.LastStripeEventId == stripeEvent.Id)
        {
            logger.LogInformation(
                "Duplicate Stripe event {EventId} for session {SessionId}, skipping",
                stripeEvent.Id, onboardingSessionId);
            return Results.Ok();
        }

        if (stateMachine.CanAdvance(session, OnboardingState.TrialActivated))
        {
            session.LastStripeEventId = stripeEvent.Id;
            stateMachine.Advance(session, OnboardingState.TrialActivated);
            await sessionStore.SaveAsync(session, ct);
            logger.LogInformation(
                "Onboarding session {SessionId} advanced to TrialActivated via Stripe webhook",
                onboardingSessionId);
        }
        else
        {
            logger.LogWarning(
                "Onboarding session {SessionId} cannot advance to TrialActivated from {CurrentState}",
                onboardingSessionId, session.CurrentState);
        }

        return Results.Ok();
    }
}
