# Remaining Work Plan

**Date:** 2026-03-11
**Status:** Draft
**Author:** Eddie Rosado + Claude

## Current State Summary

| Area | Status | Branch |
|------|--------|--------|
| Repo restructure | Complete | `main` |
| CMA pipeline API | Complete (134 tests passing) | `feat/cma-pipeline-impl` |
| Agent-site template engine | Complete (scaffolded, templates, middleware, sections, tests) | `feature/agent-site-template-engine` |
| Onboarding API | In progress (endpoints, tools, state machine, chat service) | `feat/deployment-ready-onboarding` |
| Platform (portal) | In progress (landing page, chat UI, onboard page) | `feat/deployment-ready-onboarding` |
| Security hardening | In progress (auth, rate limiting, SSRF allowlist, PII reduction) | `feat/deployment-ready-onboarding` |
| Legal compliance | In progress (EHO, Privacy, ToS, etc.) | being implemented now |

---

## Phase 1: Security Hardening (P0)

> Complete the remaining security items before any new features. These protect existing functionality.

### 1.1 DNS Rebinding Prevention in ProfileScraperService

- **Priority:** P0
- **Complexity:** S
- **Status:** NOT STARTED
- **Depends on:** Nothing
- **Location:** `apps/api/RealEstateStar.Api/Features/Onboarding/Services/ProfileScraperService.cs`

**Problem:** `ValidateUrl()` checks the domain against an allowlist and blocks direct IP URLs, but does NOT resolve the domain to an IP before fetching. An attacker who controls a domain in the allowlist's DNS (or exploits a subdomain of an allowed domain) could point it at `127.0.0.1` or `169.254.x.x`. The current code only catches literal IP addresses in the URL host, not DNS-resolved IPs.

**Implementation:**
1. After the domain allowlist check passes, call `Dns.GetHostAddressesAsync(uri.Host)` to resolve the hostname.
2. Check every resolved IP against `IsPrivateIp()` and `IPAddress.IsLoopback()`.
3. If any resolved IP is private/loopback, reject the URL with a clear log message.
4. Use the resolved IP to construct the actual HTTP request (pin the DNS resolution) to prevent TOCTOU race conditions where DNS changes between validation and fetch.
5. Add tests: mock DNS resolution returning `127.0.0.1`, `10.0.0.1`, `169.254.169.254`, and a valid public IP.

**Files to modify:**
- `ProfileScraperService.cs` — add DNS resolution step in `ScrapeAsync()` before the HTTP call
- `ProfileScraperTests.cs` — add DNS rebinding test cases

### 1.2 OAuth Profile Cross-Validation (MED-2)

- **Priority:** P0
- **Complexity:** S
- **Status:** NOT STARTED (TODO at line 62 of `GoogleOAuthCallbackEndpoint.cs`)
- **Depends on:** Nothing
- **Location:** `apps/api/RealEstateStar.Api/Features/Onboarding/ConnectGoogle/GoogleOAuthCallbackEndpoint.cs`

**Problem:** After Google OAuth, the callback advances the session state without verifying that the Google account email matches the email scraped from the agent's profile. An attacker could connect a different Google account to hijack an onboarding session.

**Implementation:**
1. After `ExchangeCodeAsync()` returns tokens, call the Google UserInfo endpoint (`https://www.googleapis.com/oauth2/v3/userinfo`) to get the authenticated email.
2. Compare the Google email against `session.ScrapedProfile.Email` (case-insensitive).
3. If they match or if the scraped profile has no email (allow it — the agent may not have had a public email), advance the state.
4. If they don't match, log a warning with both emails (hashed, not raw — PII rule), return an error message to the user ("The Google account email doesn't match your profile email. Please connect the correct account or update your profile."), and do NOT advance state.
5. Add a `skipEmailValidation` flag for edge cases where agents legitimately use different emails.

**Files to modify:**
- `GoogleOAuthCallbackEndpoint.cs` — add email validation before `stateMachine.Advance()`
- `GoogleOAuthService.cs` — add `GetUserEmailAsync()` method (or extract from token claims)
- `GoogleOAuthCallbackEndpointTests.cs` — add match/mismatch/null test cases

---

## Phase 2: Test Coverage Gaps (P1)

> Fill the gaps identified in security and code quality reviews. Required before shipping.

### 2.1 Frontend Behavioral Tests — GoogleAuthCard

- **Priority:** P1
- **Complexity:** S
- **Status:** PARTIAL (7 tests exist, missing edge cases)
- **Depends on:** Nothing
- **Location:** `apps/platform/__tests__/components/GoogleAuthCard.test.tsx`

**Existing tests cover:** render, popup open, postMessage success/error/ignore/untrusted origin.

**Missing behavioral tests:**
1. Listener cleanup on unmount — render, unmount, fire postMessage, verify no callback.
2. Multiple rapid clicks — verify only one popup opens (or behavior is idempotent).
3. Component with no `onError` prop — verify failure postMessage doesn't throw.
4. `apiOrigin` fallback — verify it defaults to `NEXT_PUBLIC_API_URL` when not provided.

### 2.2 Frontend Behavioral Tests — PaymentCard

- **Priority:** P1
- **Complexity:** S
- **Status:** PARTIAL (7 tests exist, missing edge cases)
- **Depends on:** Nothing
- **Location:** `apps/platform/__tests__/components/PaymentCard.test.tsx`

**Existing tests cover:** render default/custom price, button click opens URL, disabled state, waiting message.

**Missing behavioral tests:**
1. Button does not reappear after clicking (stays in "waiting" state — no regression to clickable).
2. Multiple rapid clicks — verify `window.open` called only once (or idempotent).
3. Accessibility — button has accessible name, waiting state is announced to screen readers.
4. Custom price with special characters renders correctly.

### 2.3 Streaming Endpoint Integration Tests — PostChatEndpoint

- **Priority:** P1
- **Complexity:** M
- **Status:** NOT STARTED
- **Depends on:** Nothing
- **Location:** `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/PostChat/PostChatEndpointTests.cs`

**Problem:** `PostChatEndpoint.Handle()` wraps SSE streaming in `Results.Stream()` with three catch blocks: `OperationCanceledException`, general `Exception`, and the inner catch for the error-event write attempt. These exception paths need integration-style tests.

**Test cases needed:**
1. **Happy path** — mock `chatService.StreamResponseAsync()` to yield chunks, verify SSE frames in response body (`data: ...`, `data: [DONE]`).
2. **Client cancellation** — cancel the `CancellationToken` mid-stream, verify `[CHAT-015]` is logged, no error event sent.
3. **Service exception** — mock `StreamResponseAsync()` to throw after yielding some chunks, verify `[CHAT-016]` is logged, verify `data: [ERROR]` event is sent to client.
4. **Broken stream on error** — mock both `StreamResponseAsync()` throw AND `StreamWriter.WriteAsync()` throw on the error event, verify the inner catch swallows gracefully (no unhandled exception).
5. **Card event** — yield a chunk starting with `[CARD:`, verify it's sent as `event: card\ndata: ...` instead of `data: ...`.
6. **Session save after success** — verify `sessionStore.SaveAsync()` is called after successful stream, NOT after exception.

**Challenge:** `Results.Stream()` requires an actual HTTP pipeline to test properly. Options:
- Use `WebApplicationFactory<Program>` for true integration tests.
- Use `DefaultHttpContext` with a `MemoryStream` as the response body and invoke `Handle()` directly, then read SSE frames from the memory stream.

---

## Phase 3: Agent-Site Template Engine Integration (P1)

> The agent-site app exists on `feature/agent-site-template-engine` with the Emerald Classic template, middleware, sections, and tests. This phase connects it to the onboarding pipeline.

### 3.1 Merge Agent-Site Branch to Main

- **Priority:** P1
- **Complexity:** S
- **Status:** NOT STARTED
- **Depends on:** Phase 1 (security items should land first)

**Work:**
1. Rebase `feature/agent-site-template-engine` onto current `main`.
2. Resolve any conflicts (likely minimal — agent-site is a standalone app).
3. Verify all agent-site tests pass (`npm test` in `apps/agent-site`).
4. Merge to `main`.

### 3.2 Connect Onboarding DeploySiteTool to Agent-Site

- **Priority:** P1
- **Complexity:** M
- **Status:** NOT STARTED
- **Depends on:** 3.1

**Problem:** `DeploySiteTool` in the onboarding flow needs to generate `config/agents/{id}.json` and `config/agents/{id}.content.json`, create a branch, and trigger the deploy pipeline. The agent-site reads these files at runtime.

**Implementation:**
1. `DeploySiteTool.ExecuteAsync()` should write the agent config and content JSON files.
2. Create the `onboard/{agent-id}` branch via git operations (or GitHub API).
3. Commit the config files to the branch.
4. Open a PR against `main`.
5. The agent-site's ISR will pick up new config on merge.

**Files to modify:**
- `DeploySiteTool.cs` — generate config + content JSON from session data
- `SiteDeployService.cs` — implement git/GitHub operations
- `OnboardingMappers.cs` — add mappers from `ScrapedProfile` to agent config/content JSON models
- Tests for all of the above

### 3.3 Agent-Site Content JSON Schema Validation

- **Priority:** P1
- **Complexity:** S
- **Status:** NOT STARTED
- **Depends on:** 3.1

**Work:**
1. Create `config/agent-content.schema.json` to validate `{id}.content.json` files.
2. Add schema validation to the deploy pipeline (CI check).
3. Add schema validation in `DeploySiteTool` before committing files.

### 3.4 Cloudflare Pages Deploy Pipeline

- **Priority:** P1
- **Complexity:** M
- **Status:** NOT STARTED
- **Depends on:** 3.1

**Work:**
1. Set up Cloudflare Pages project for `apps/agent-site`.
2. Configure OpenNext adapter for Cloudflare Workers runtime.
3. Set up GitHub Actions workflow: on PR merge to `main`, build and deploy.
4. Configure wildcard subdomain routing (`*.realestatestar.com`).
5. Test with `jenise-buckalew.realestatestar.com`.

---

## Phase 4: CMA Pipeline Frontend (P1)

> API is complete on `feat/cma-pipeline-impl`. Frontend integration is blocked by agent-site template engine (the CMA form lives on agent sites).

### 4.1 Merge CMA Pipeline Branch to Main

- **Priority:** P1
- **Complexity:** S
- **Status:** NOT STARTED
- **Depends on:** 3.1 (agent-site merge — CMA form component already exists there)

**Work:**
1. Rebase `feat/cma-pipeline-impl` onto current `main`.
2. Resolve conflicts.
3. Verify all 134 tests still pass.
4. Merge to `main`.

### 4.2 Wire CMA Form to API

- **Priority:** P1
- **Complexity:** M
- **Status:** NOT STARTED
- **Depends on:** 4.1, 3.1

**Problem:** The `CmaForm` section component in `apps/agent-site/components/sections/CmaForm.tsx` needs to submit to the CMA API (`POST /agents/{id}/cma`).

**Implementation:**
1. Update `CmaForm` to POST form data to the API endpoint.
2. Handle SSE streaming response for real-time status updates (Researching... Analyzing... Generating PDF...).
3. Show success state with "Check your email" message.
4. Handle error states gracefully.
5. Add behavioral tests for form submission, streaming status, success/error states.

### 4.3 Wow Moment Flow (Onboarding Integration)

- **Priority:** P1
- **Complexity:** L
- **Status:** NOT STARTED
- **Depends on:** 4.2, 3.2

**Problem:** After the agent's site deploys during onboarding, the AI needs to embed the live site in the chat, pre-fill the CMA form, and walk the agent through the demo.

**Implementation:**
1. `SubmitCmaFormTool` (already exists in onboarding tools) needs to be connected to the real CMA pipeline API.
2. Chat UI `SitePreview` component needs to iframe the deployed agent site.
3. `postMessage` bridge between chat and iframe to pre-fill the CMA form fields.
4. AI instructs agent to click submit; CMA generates and emails arrive.
5. Security: validate `postMessage` origin on both sides.

---

## Phase 5: Legal Compliance (P1)

> Being implemented now. Noting remaining items for tracking.

### 5.1 Equal Housing Opportunity (EHO)

- **Priority:** P1
- **Complexity:** S
- **Status:** IN PROGRESS
- **Depends on:** 3.1 (agent-site merge)

EHO logo and fair housing statement must appear on every agent site. Add to the `Footer` component in `apps/agent-site/components/sections/Footer.tsx`.

### 5.2 Privacy Policy, Terms of Service, DMCA

- **Priority:** P1
- **Complexity:** M
- **Status:** IN PROGRESS

Static pages needed on both the platform (`apps/platform`) and agent sites (`apps/agent-site`). Agent-site privacy policy must reference the specific agent's data practices.

### 5.3 Cookie Consent Banner

- **Priority:** P1
- **Complexity:** S
- **Status:** IN PROGRESS

Required on agent sites that use Google Analytics or Facebook Pixel (configured via `agent.integrations.analytics`).

### 5.4 ADA/WCAG Compliance

- **Priority:** P1
- **Complexity:** M
- **Status:** IN PROGRESS

Audit all components for keyboard navigation, ARIA labels, color contrast, screen reader support.

---

## Phase 6: Business Items (P2)

> Non-engineering items. Tracked here for completeness.

### 6.1 Tech E&O Insurance

- **Priority:** P2
- **Complexity:** N/A (business)
- **Status:** NOT STARTED
- **Depends on:** Nothing

Professional liability insurance covering software errors that could affect agent transactions. Research providers, get quotes. Typical coverage: $1M-$2M per occurrence.

### 6.2 LLC Formation

- **Priority:** P2
- **Complexity:** N/A (business)
- **Status:** NOT STARTED
- **Depends on:** Nothing

Form LLC for Real Estate Star before accepting payments. Required before Stripe goes live in production. Jurisdiction: likely NJ or DE.

---

## Dependency Graph

```
Phase 1 (Security)
  1.1 DNS Rebinding ────────────────────┐
  1.2 OAuth Cross-Validation ───────────┤
                                        ▼
Phase 2 (Test Gaps)              Phase 3 (Agent-Site)
  2.1 GoogleAuthCard tests         3.1 Merge agent-site ──┐
  2.2 PaymentCard tests            3.2 Deploy tool ───────┤
  2.3 Streaming tests              3.3 Content schema     │
                                   3.4 Cloudflare deploy  │
                                        │                 │
                                        ▼                 ▼
                                 Phase 4 (CMA Frontend)
                                   4.1 Merge CMA branch
                                   4.2 Wire CMA form
                                   4.3 Wow moment flow
                                        │
                                        ▼
                                 Phase 5 (Legal)
                                   5.1-5.4 (in progress)
                                        │
                                        ▼
                                 Phase 6 (Business)
                                   6.1 E&O Insurance
                                   6.2 LLC Formation
```

**Critical path:** 1.1/1.2 -> 3.1 -> 3.2/3.4 -> 4.1 -> 4.2 -> 4.3

Phases 2 and 5 can proceed in parallel with everything else.

---

## Effort Summary

| Item | Priority | Complexity | Status |
|------|----------|------------|--------|
| 1.1 DNS Rebinding Prevention | P0 | S | NOT STARTED |
| 1.2 OAuth Cross-Validation | P0 | S | NOT STARTED |
| 2.1 GoogleAuthCard Tests | P1 | S | PARTIAL |
| 2.2 PaymentCard Tests | P1 | S | PARTIAL |
| 2.3 Streaming Endpoint Tests | P1 | M | NOT STARTED |
| 3.1 Merge Agent-Site | P1 | S | NOT STARTED |
| 3.2 Deploy Tool Integration | P1 | M | NOT STARTED |
| 3.3 Content Schema | P1 | S | NOT STARTED |
| 3.4 Cloudflare Deploy | P1 | M | NOT STARTED |
| 4.1 Merge CMA Branch | P1 | S | NOT STARTED |
| 4.2 Wire CMA Form | P1 | M | NOT STARTED |
| 4.3 Wow Moment Flow | P1 | L | NOT STARTED |
| 5.1 EHO | P1 | S | IN PROGRESS |
| 5.2 Privacy/ToS/DMCA | P1 | M | IN PROGRESS |
| 5.3 Cookie Consent | P1 | S | IN PROGRESS |
| 5.4 ADA/WCAG | P1 | M | IN PROGRESS |
| 6.1 Tech E&O Insurance | P2 | N/A | NOT STARTED |
| 6.2 LLC Formation | P2 | N/A | NOT STARTED |

**Total engineering items:** 12 NOT STARTED, 4 IN PROGRESS, 2 PARTIAL
**Deadline:** Friday 2026-03-13 — all P0 and P1 items complete

### Sprint Schedule (Tue-Fri)

| Day | Focus | Items |
|-----|-------|-------|
| Tue (3/11) | Security + Merge branches | 1.1, 1.2, 3.1, 4.1 |
| Wed (3/12) | Integration + Tests | 2.1-2.3, 3.2, 3.3 |
| Thu (3/13) | Deploy + CMA + Legal | 3.4, 4.2, 5.1-5.4 |
| Fri (3/14) | Wow moment + Polish | 4.3, final integration testing |

Phases 2 (tests) and 5 (legal) run in parallel throughout.
