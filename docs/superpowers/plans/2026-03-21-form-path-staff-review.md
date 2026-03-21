# Staff Engineer Review: Form Submission Path — Full Stack Audit

**Date:** 2026-03-21
**Reviewer:** Claude (Staff-level review requested by Eddie Rosado)
**Scope:** Complete form submission path — UI → server action → HMAC → API → storage → consent → background workers → notifications → Drive
**Goal:** Ensure the form path is legally compliant, observable, resilient, and production-ready before launch

---

## What Was Reviewed

```
LeadForm (packages/ui)
  → CmaSection (agent-site)
    → submit-lead server action
      → Turnstile verification
      → HMAC signing (hmac.ts)
        → POST /agents/{agentId}/leads
          → ApiKeyHmacMiddleware (auth)
          → SubmitLeadEndpoint (validation, save, consent log, enqueue)
            → LeadProcessingWorker (enrichment, notification, CMA, home search)
              → CascadingAgentNotifier (WhatsApp → Email → Drive fallback)
              → ScraperLeadEnricher (Claude + scraper APIs)
              → LeadStore (Google Drive)

Privacy pages: /{handle}/privacy/opt-out, /delete, /subscribe
Privacy endpoints: opt-out, request-deletion, subscribe, delete-data
```

---

## What's Working Well

The backend is strong. Highlights:

- **Structured logging** with unique `[PREFIX-NNN]` codes across the entire pipeline (`[LEAD-001]` through `[WORKER-051]`)
- **OpenTelemetry** with ActivitySource spans, counters (leads.received, leads.enriched, etc.), and histograms (pipeline duration, enrichment duration)
- **Polly resilience** on all external APIs: Claude (3x retry + CB), ScraperAPI (2x + CB), GWS/Drive (3x + CB), WhatsApp (3x + CB), Google Chat (1x + CB)
- **Write-before-enqueue** pattern: lead saved to Drive BEFORE returning 202 and before background processing
- **Cascading notification fallback**: WhatsApp → Email → Drive file → log `[LEAD-043]`
- **Background worker health tracking** with staleness detection (5-minute threshold)
- **HMAC auth** with constant-time comparison, timestamp window, per-agent key derivation
- **Marketing consent log** with timestamp, IP, user agent, channels, consent text (all captured server-side)
- **WhatsApp audit trail** in Azure Table Storage with full message lifecycle
- **Deletion workflow** with 128-bit random tokens, SHA-256 hash storage, 24-hour expiry, email hashing in audit
- **Privacy pages deployed** at `/{handle}/privacy/opt-out`, `/delete`, `/subscribe` with working forms and server actions
- **Rate limiting** on lead submission (20/hour per IP)
- **PII hashing** in all logs (email → SHA-256 first 12 chars)
- **Bounded channels** (capacity 100) with backpressure on all pipeline channels

---

## Blockers (Must Fix Before Launch)

### B1: TCPA Consent Text Doesn't Match Product Behavior

**Current state:** The TCPA consent checkbox text says "consent to receive calls and text messages" and hardcodes `channels: ["calls", "texts"]` in the submission payload.

**Actual product behavior:** The platform sends **email marketing communications** (CMA reports, lead follow-ups). The agent may call or text manually, but the platform doesn't make automated calls or send SMS.

**Legal risk:** Consent text must accurately describe the communications the platform will send. Claiming consent for "automated calls" when the platform doesn't make them, while not capturing consent for "email" when it does send emails, creates a mismatch that weakens consent validity.

**Files:** `packages/ui/LeadForm/LeadForm.tsx:14-18` (TCPA_CONSENT_TEXT), `LeadForm.tsx:243` (hardcoded channels)

### B2: Email Exposed in Thank-You Redirect URL

**Current state:** `CmaSection.tsx:54-55` redirects to `/thank-you?email=${emailParam}`.

**Risk:** Email visible in browser history, CDN logs, and `Referer` headers to any external resource on the thank-you page. GDPR data minimization violation.

**Fix:** Use a session cookie or token instead of query parameter.

### B3: No Timeout on Frontend Fetch Calls

**Current state:** `hmac.ts:21` and `turnstile.ts:6` both call `fetch()` with no timeout. If the API or Cloudflare is slow/unresponsive, the Next.js server action hangs indefinitely.

**Risk:** User sees infinite spinner. Cloudflare Worker has a 30-second CPU time limit, so it would eventually timeout, but the UX is poor and the error unhandled.

---

## High Priority (Observability & Resilience)

### H1: Frontend Has Zero Observability

**Current state:** No Sentry, no error telemetry, no form funnel tracking, no performance metrics. `submit-lead.ts` returns generic errors to the user with no server-side logging. If form submissions start failing, Eddie has no way to know from the frontend side.

**What's needed:** Sentry for error tracking. Form funnel events (started, field_completed, submitted, success, failure). API response time tracking.

### H2: No ActivitySource Span on Lead Submission Endpoint

**Current state:** `SubmitLeadEndpoint.cs` increments the `LeadsReceived` counter and logs `[LEAD-001]`/`[LEAD-002]`, but doesn't create an OpenTelemetry Activity span. The downstream `LeadProcessingWorker` creates a span, but the HTTP request itself only has generic ASP.NET Core instrumentation — no custom span with business context (agentId, leadType, correlationId).

### H3: Notification Failures Not Retried

**Current state:** `LeadProcessingWorker.cs:128-134` catches notification failures, logs them, but does not retry. The `CascadingAgentNotifier` provides fallback channels (WhatsApp → Email → Drive), but each channel attempt is fire-once with no backoff. If all three fail, the agent never learns about a new lead.

### H4: No Dead Letter Queue for Failed Pipeline Steps

**Current state:** If enrichment or notification fails, it's logged with correlation ID and the pipeline continues. But there's no mechanism to retry failed steps or move them to a DLQ for manual review. CMA and HomeSearch dispatch requests are in-memory only — lost on crash if not yet dequeued.

### H5: Consent Log Not Tamper-Evident

**Current state:** `MarketingConsentLog` stores consent records as CSV rows appended to a Google Drive file. No HMAC signature, no hash chain, no digital signature. If agent gains access to Drive folder, records can be edited after the fact.

**Risk:** In a legal dispute, consent records cannot be proven authentic. For v1 this is acceptable since the agent is the customer and the data is in their Drive, but for multi-agent/enterprise this becomes a real issue.

---

## Medium Priority (ADA, Health Checks, Polish)

### M1: ADA Color Contrast Issues
- Red required asterisk (`color: "red"`) on colored backgrounds: borderline AA
- Error messages in red on white: borderline AA, fails AAA
- Disclaimer text `#767676` on white: barely AA
- Submit button gold background with white text: ~4.2:1

### M2: No Privacy Policy / Terms Links in Consent Area
The TCPA consent text doesn't link to a privacy policy or terms of service.

### M3: Google Maps Autocomplete Silent Failure
If Google Maps API fails to load, the error is swallowed silently. User gets a text field with no autocomplete and no indication why.

### M4: ScraperAPI / Turnstile Health Checks Are Config-Only
They check if the API key is configured but don't test connectivity. `/health/ready` shows Healthy even if these services are down.

### M5: No WhatsApp or Email Health Checks
No health check for Meta Graph API or Gmail connectivity.

### M6: No Alerting Infrastructure
Status dashboard polls every 30 seconds but there's no Slack/email/PagerDuty alerting. Eddie would have to manually check `/status` to discover an outage.

### M7: ReadOnly State Field Missing aria-readonly
`LeadForm.tsx:533` — `readOnly` but no `aria-readonly="true"`.

---

## Summary Table

| Priority | ID | Issue | Type | Effort |
|----------|----|-------|------|--------|
| BLOCKER | B1 | TCPA consent text doesn't match product | Copy fix | Small |
| BLOCKER | B2 | Email in redirect URL | Code fix | Small |
| BLOCKER | B3 | No timeout on frontend fetch | Code fix | Small |
| HIGH | H1 | Frontend zero observability | New feature | Medium |
| HIGH | H2 | No OTel span on submit endpoint | Code fix | Small |
| HIGH | H3 | Notification failures not retried | Enhancement | Medium |
| HIGH | H4 | No DLQ for failed pipeline steps | New feature | Medium |
| HIGH | H5 | Consent log not tamper-evident | Enhancement | Medium |
| MEDIUM | M1 | ADA color contrast | CSS fix | Small |
| MEDIUM | M2 | No privacy/terms links in consent | Copy fix | Small |
| MEDIUM | M3 | Maps autocomplete silent failure | UX fix | Small |
| MEDIUM | M4 | Health checks config-only | Enhancement | Small |
| MEDIUM | M5 | No WhatsApp/email health checks | New feature | Small |
| MEDIUM | M6 | No alerting infrastructure | New feature | Medium |
| MEDIUM | M7 | aria-readonly missing | Accessibility | Small |
