---
name: stripe-integration
description: Stripe payment integration patterns and security. Use when writing, reviewing, or debugging any Stripe-related code (checkout, webhooks, subscriptions, billing).
---

# Stripe Integration Patterns

## Core Principles

1. **Webhooks are the source of truth** — never advance state based on client-side events
2. **Server owns the price** — PriceId comes from config, never from the client
3. **Idempotency everywhere** — all mutating Stripe API calls need idempotency keys
4. **Test/live isolation** — keys from config, never hardcoded

## Checkout Session Creation

```csharp
// Required patterns:
var options = new SessionCreateOptions
{
    LineItems = [new() { Price = _priceId, Quantity = 1 }],  // Server-side price
    Mode = "payment",
    SuccessUrl = $"{_platformUrl}/success?session_id={{CHECKOUT_SESSION_ID}}",
    CancelUrl = $"{_platformUrl}/cancelled",
    CustomerEmail = validatedEmail,  // Validated before use
    Metadata = new Dictionary<string, string>
    {
        ["internal_session_id"] = sessionId,  // For webhook correlation
    },
};

// Always use idempotency key for creation
var requestOptions = new RequestOptions { IdempotencyKey = $"checkout_{sessionId}" };
var session = await _sessionService.CreateAsync(options, requestOptions, ct);
```

## Webhook Handler Pattern

```csharp
// 1. Verify signature FIRST
Event stripeEvent;
try
{
    stripeEvent = EventUtility.ConstructEvent(json, signature, _webhookSecret);
}
catch (StripeException) { return Results.BadRequest(); }

// 2. Check idempotency — skip already-processed events
if (session.LastStripeEventId == stripeEvent.Id)
    return Results.Ok();  // 200 to prevent Stripe retries

// 3. Process the event
session.LastStripeEventId = stripeEvent.Id;
// ... state transition ...
await sessionStore.SaveAsync(session, ct);

// 4. Always return 200 (even for unknown events or missing sessions)
return Results.Ok();
```

## Anti-Patterns to Flag

| Anti-Pattern | Why It's Wrong | Fix |
|---|---|---|
| `onPaymentComplete` on button click | Payment hasn't actually completed | Use webhook to confirm |
| `interface IStripeService { string WebhookSecret { get; } }` | Leaks secret to consumers | Encapsulate: `ValidateSignature(json, sig)` |
| `var price = request.Amount` | Client controls price | Use server-side `PriceId` from config |
| `if (string.IsNullOrEmpty(webhookSecret))` but assigned via `??` | Misses empty string from env var | Use `IsNullOrWhiteSpace` |
| No idempotency key on `CreateAsync` | Duplicate charges on retry | Add `RequestOptions.IdempotencyKey` |
| Returning 500 from webhook on error | Stripe retries aggressively | Always return 200, log error internally |

## Testing Requirements

- [ ] Webhook signature verification: valid signature passes
- [ ] Webhook signature verification: invalid signature returns 400
- [ ] Webhook signature verification: missing header returns 400
- [ ] Idempotency: duplicate event ID is skipped (200, no state change)
- [ ] Missing session ID in metadata: returns 200, no crash
- [ ] Unknown event type: returns 200, ignored
- [ ] State transition: only advances from expected state
- [ ] Checkout session creation: uses server-side price
- [ ] Checkout session creation: validates email before use

## Webhook Endpoint Configuration

- Exclude from rate limiting (Stripe has its own retry logic)
- Exclude from CSRF/antiforgery (`.DisableAntiforgery()`)
- No auth middleware (Stripe can't authenticate — signature is the auth)
- Set appropriate timeout (Stripe expects response within 30s)
