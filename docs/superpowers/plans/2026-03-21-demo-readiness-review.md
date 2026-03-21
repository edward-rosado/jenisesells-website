# Staff Engineer Review — Demo Readiness

**Date:** 2026-03-21
**Reviewer:** Claude (Staff-level review requested by Eddie Rosado)
**Scope:** Full-stack review of all project layers — API, agent-site, platform, CI/CD, infrastructure
**Goal:** Identify every issue that would prevent a successful live demo of the seller lead submission flow

---

## Demo Path Under Review

```
Visitor → agent-site (jenise.real-estate-star.com)
  → Fills CMA/Lead form (address, name, email, phone, mode)
  → Turnstile bot protection validates
  → HMAC-signed POST → api.real-estate-star.com/agents/{agentId}/leads
  → API validates HMAC, stores lead in Google Drive
  → Lead processing worker triggers (email notification, WhatsApp notification)
  → Visitor redirected to /thank-you page

Privacy flows:
  → /opt-out, /delete, /subscribe pages → HMAC-signed POST → API privacy endpoints
```

---

## Blockers (Demo Will Fail)

### B1: HMAC Signing Key Mismatch — Frontend vs Backend

**Files:**
- `apps/agent-site/lib/hmac.ts:10` — signs with raw `hmacSecret`
- `apps/api/RealEstateStar.Api/Infrastructure/ApiKeyHmacMiddleware.cs:98` — verifies with `{hmacSecret}:{agentId}`

**Impact:** Every lead submission will get 401 Unauthorized in production. Currently masked because `ApiKeys` dict is empty, causing HMAC to be silently bypassed (see H1).

**Fix:** Align the signing. The backend's approach (scoping the key per agent) is more secure. Update `hmac.ts` to sign with `${hmacSecret}:${agentId}`.

---

### B2: Privacy Endpoint Path Mismatch — Deletion

**Files:**
- `apps/agent-site/actions/privacy.ts:28` — calls `agents/${agentId}/leads/delete-request`
- `apps/api/RealEstateStar.Api/Features/Leads/RequestDeletion/RequestDeletionEndpoint.cs:13` — expects `/agents/{agentId}/leads/request-deletion`

**Impact:** Deletion requests return 404.

**Fix:** Change `delete-request` → `request-deletion` in `privacy.ts`.

---

### B3: 6 Missing GitHub Secrets Block API Deploy

**Secrets needed:**
- `WHATSAPP_PHONE_NUMBER_ID`
- `WHATSAPP_ACCESS_TOKEN`
- `WHATSAPP_APP_SECRET`
- `WHATSAPP_VERIFY_TOKEN`
- `WHATSAPP_WABA_ID`
- `AZURE_STORAGE_CONNECTION_STRING`

**Impact:** `deploy-api.yml` fails at container app secret injection. Docker build/push succeeds but the app won't start.

**Fix:** Infrastructure task — Eddie must configure these in GitHub Settings > Secrets. For demo without WhatsApp, the secrets can be set to placeholder values since Program.cs gracefully disables WhatsApp when `PhoneNumberId` is empty.

---

## High Priority (Demo Works But Fragile)

### H1: HMAC Auth Silently Bypassed When ApiKeys Empty

**File:** `ApiKeyHmacMiddleware.cs:30`

```csharp
if (_options.ApiKeys.Count == 0 || string.IsNullOrEmpty(_options.HmacSecret))
{
    await _next(context);  // No auth at all
    return;
}
```

**Impact:** In production with empty `ApiKeys` config, anyone can submit leads without authentication. This is acceptable for local dev but dangerous if deployed.

**Fix:** Add startup validation — if `ASPNETCORE_ENVIRONMENT` is `Production` and `ApiKeys` is empty, throw at startup. This prevents accidental deployment without auth.

---

### H2: NoopEmailNotifier Drops Lead Notification Emails

**File:** `Program.cs:265`
```csharp
builder.Services.AddSingleton<IEmailNotifier, NoopEmailNotifier>();
```

**Impact:** Agent never gets notified about new leads via email. For demo, WhatsApp notification is the primary channel, but email is the fallback. Noop means silent failure.

**Fix:** This is a known placeholder — email channel not built yet. For demo, ensure WhatsApp notification is working (depends on B3). Log a warning at startup when Noop is registered so it's visible in observability.

---

### H3: Noop WhatsApp Classifiers (Intent + Response)

**File:** `Program.cs:272-273`
```csharp
builder.Services.AddSingleton<IIntentClassifier, NoopIntentClassifier>();
builder.Services.AddSingleton<IResponseGenerator, NoopResponseGenerator>();
```

**Impact:** WhatsApp conversations get no AI-powered intent classification or response generation. The conversation handler still works but gives generic responses.

**Fix:** Known placeholder for Phase 3 of WhatsApp integration. Not blocking demo — WhatsApp notification of new leads works independently of AI conversation handling.

---

### H4: No Local Dev Server Secrets Configuration

**Impact:** New developer cloning the repo can't run the API locally without manually discovering which secrets are needed.

**Fix:** Add `appsettings.Development.json` with placeholder values and comments explaining each secret. Not blocking demo but blocks onboarding.

---

### H5: Turnstile Silently Disabled Without Secret Key

**File:** `apps/agent-site/lib/turnstile.ts:3`
```typescript
if (!secret) return false;
```

**Impact:** When `TURNSTILE_SECRET_KEY` is not set, Turnstile validation returns `false`, which blocks ALL form submissions. This is the safe direction (deny by default) but needs the secret configured.

**Fix:** Ensure `TURNSTILE_SECRET_KEY` is set in Cloudflare Worker secrets via `wrangler secret bulk`. The Turnstile site key is already in `wrangler.jsonc`.

---

## Medium Priority (Polish & Hardening)

### M1: Hardcoded Google Maps API Key in Source

**Files:**
- `apps/agent-site/.env.production:2`
- `apps/agent-site/wrangler.jsonc:19`

The API key `AIzaSyCD8Lw5xaMCxoWEzkTRCT284Jv37AMfd2Y` is committed to the repo. Google Maps API keys are typically restricted by HTTP referrer, so exposure risk is low if restrictions are configured.

**Fix:** Verify API key restrictions in Google Cloud Console (restrict to `*.real-estate-star.com`). Move to Cloudflare Worker secrets if unrestricted.

---

### M2: No Admin Dashboard for Lead Management

**Impact:** After demo, Eddie has no way to view submitted leads except checking Google Drive directly.

**Fix:** Future work — platform portal lead management UI.

---

### M3: WebhookProcessorWorker Agent Resolution Placeholder

**File:** `WebhookProcessorWorker.cs:74-75` — passes empty strings for `agentId` and `firstName`

**Impact:** WhatsApp messages processed without agent context. Not blocking demo lead submission flow.

**Fix:** Phase 3 of WhatsApp integration — resolve agentId from phone number mapping.

---

### M4: IMemoryCache Without SizeLimit

**Impact:** Potential unbounded memory growth if WhatsApp message volume is high.

**Fix:** Set `SizeLimit` on `MemoryCacheOptions` and `Size = 1` on cache entries.

---

## Summary

| Priority | ID | Issue | Fix Type | Demo Impact |
|----------|----|-------|----------|-------------|
| BLOCKER | B1 | HMAC key mismatch | Code fix | 401 on all leads |
| BLOCKER | B2 | Deletion path mismatch | Code fix | 404 on deletion |
| BLOCKER | B3 | Missing GitHub secrets | Infrastructure | Deploy fails |
| HIGH | H1 | HMAC bypass when empty | Code fix | Unauthenticated access |
| HIGH | H2 | Noop email notifier | Known placeholder | No email notifications |
| HIGH | H3 | Noop WhatsApp AI | Known placeholder | Generic responses |
| HIGH | H4 | No dev secrets docs | Documentation | Onboarding blocked |
| HIGH | H5 | Turnstile needs secret | Infrastructure | Forms blocked |
| MEDIUM | M1 | Hardcoded Maps key | Security review | None |
| MEDIUM | M2 | No admin dashboard | Future work | Manual Drive checks |
| MEDIUM | M3 | Agent resolution stub | Phase 3 | WhatsApp context missing |
| MEDIUM | M4 | Unbounded cache | Hardening | Memory growth |

## Actionable Items for This PR

**Code fixes (B1, B2, H1):** Fix HMAC signing alignment, fix deletion path, add production startup validation for empty ApiKeys.

**Infrastructure (B3, H5):** Eddie configures GitHub secrets and Cloudflare Worker secrets.

**Everything else:** Tracked as future work, not blocking this PR.
