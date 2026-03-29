---
name: WhatsApp Business API Integration Research
description: Research findings on WhatsApp Business Platform signup, approval, pricing, and architecture for Real Estate Star agent communication channel
type: project
---

WhatsApp integration is for **platform-to-agent communication** — Real Estate Star sends lead cards/notifications to agents, agents reply to ask questions and trigger actions. NOT agent-to-customer messaging.

**Why:** Agents need a mobile-first conversational interface to manage leads on the go without opening the platform.

**How to apply:** All template design, pricing estimates, and architecture should assume the agent is the WhatsApp recipient.

## Account Type
- Must use **WhatsApp Business Platform (API)** — not the free Business App
- Meta's Direct Cloud API recommended over BSPs (Twilio, Wati) — full control, no markup fees
- On-Premises API deprecated since 2025; Cloud API is the only option

## Signup & Approval Process
1. Create Meta Business Manager account (business.facebook.com)
2. Enable 2FA (mandatory since 2024)
3. Submit business verification — legal docs must match registered name exactly
4. Register dedicated phone number (can't be active in personal WhatsApp)
5. Get display name approved
- **Timeline**: 2-14 days for verification, 3-4 weeks total including dev work
- **Common rejections**: spelling mismatches, unclear use case, missing docs

## Multi-Tenant Model
- **MVP (Option A)**: One Real Estate Star WhatsApp Business Account, agents are recipients. Simple — agents don't need their own accounts since they're receiving messages, not sending as a business.
- **Scale (Option B)**: If agents want to message their own customers via WhatsApp, each agent needs their own WABA.

## 24-Hour Window & Templates
- Customer (agent) messages back → 24hr freeform window opens
- Outside window → pre-approved template messages only
- Template categories: Authentication (minutes approval), Utility (minutes-hours), Marketing (up to 24hrs)
- **Templates needed**: lead notifications, CMA ready, follow-up reminders, listing alerts — all Utility category

## Pricing (July 2025 model — per-message)
- Within 24hr window: free for all message types
- Outside window: Utility ~$0.01-0.05, Marketing ~$0.02-0.22 (varies by country)
- Most agent communication will be within the 24hr window if agents are responsive
- Budget estimate: ~$10-30/month per active agent (mostly utility templates for initial lead notifications)

## Technical Architecture
- Webhook endpoint: `api.real-estate-star.com/webhooks/whatsapp` (HTTPS, respond 200 OK <20s)
- Sending: Meta Graph API `POST /v20.0/{PHONE_NUMBER_ID}/messages`
- .NET vertical slice: `Features/WhatsApp/` with webhook receiver + message sender
- Use `IHttpClientFactory` typed registration for Graph API client
- Idempotency required on webhook processing (Meta retries with exponential backoff)
- Webhook verification: GET with verify_token + challenge response

## Agent Config Extension
```json
{
  "whatsapp": {
    "phone_number": "+1234567890",
    "opted_in": true,
    "notification_preferences": ["new_lead", "cma_ready", "follow_up_reminder", "listing_alert"]
  }
}
```

## Design Spec
- Written: 2026-03-19
- Location: `docs/superpowers/specs/2026-03-19-whatsapp-agent-channel-design.md`
- Covers: full vertical slice architecture, 4 message templates, conversational reply handling via Claude Haiku, 24hr window management, Meta Business Platform setup timeline, observability, security, testing strategy
