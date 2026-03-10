# Agent & Skill Improvements — Lessons from Onboarding Review

> **Date:** 2026-03-10
> **Context:** 3 parallel agents built Phase A/B/C/D of the onboarding feature. A staff-level review found 10 CRITICAL, 14 HIGH issues. This document proposes improvements to our agents and skills so these classes of bugs don't recur.

---

## 1. Payment/Billing Checklist (NEW — add to security-reviewer agent)

The security-reviewer agent should have an explicit **payment flow checklist** that it runs whenever it detects Stripe, payment, billing, or subscription code:

```markdown
## Payment Security Checklist
- [ ] Payment confirmation driven by webhook, NEVER by client-side click
- [ ] Webhook signature verification tested (not just the handler logic)
- [ ] Webhook idempotency — processed event IDs stored, duplicates skipped
- [ ] Webhook endpoint excluded from rate limiting
- [ ] Price locked server-side (Stripe PriceId), never from client
- [ ] Stripe idempotency keys on all mutating API calls
- [ ] Webhook secret encapsulated — not exposed on public interfaces
- [ ] Empty/null email validated before creating Stripe sessions
- [ ] Test mode keys never hardcoded — always from config
- [ ] Frontend price display fetched from server, not hardcoded
```

**Why:** SEC-7, SEC-8, SEC-9, HIGH-1, HIGH-5, MED-3, MED-7, LOW-2, LOW-4 would all have been caught.

---

## 2. OAuth/Auth Checklist (NEW — add to security-reviewer agent)

```markdown
## OAuth Security Checklist
- [ ] `state` parameter is a CSRF token (unguessable nonce), NOT a session ID
- [ ] `state` nonce is single-use — cleared after validation
- [ ] `postMessage` uses exact target origin, never `'*'`
- [ ] HTML callback pages use JSON serialization for data, never string interpolation
- [ ] Token storage has explicit TTL with cleanup
- [ ] Profile cross-validation happens before state advances
- [ ] All endpoints accessing session data require authentication
- [ ] Session IDs have sufficient entropy (128+ bits) or are protected by auth tokens
```

**Why:** SEC-1, SEC-4, SEC-5, SEC-6, MED-2 would all have been caught.

---

## 3. File I/O Safety Checklist (NEW — add to security-reviewer agent)

```markdown
## File I/O Security Checklist
- [ ] All user-supplied path components validated with allowlist regex
- [ ] `Path.GetFullPath` + `StartsWith(basePath)` check on every file operation
- [ ] Concurrent access protected with per-key SemaphoreSlim or file locks
- [ ] Write operations use write-to-temp-then-rename for atomicity
- [ ] File store operations log errors but don't expose paths to clients
```

**Why:** SEC-2, SEC-3 would have been caught.

---

## 4. Mandatory Post-Build Smoke Test (ADD to code-reviewer agent)

The code-reviewer agent should run a **structural smoke check** after writing code:

```markdown
## Smoke Check (run after writing code)
- [ ] Every `catch (Exception)` block has a `logger.Log*` call
- [ ] No `handleSend()` called after `setInput()` (stale closure pattern)
- [ ] No `onComplete()` / `onSuccess()` called before async operation confirms
- [ ] Every duplicate function name in the codebase has identical implementation
- [ ] Every string used as an enum value has a constant or actual enum backing it
```

**Why:** HIGH-7, HIGH-8, HIGH-9, ARCH-1, SEC-8, MED-10 would have been caught.

---

## 5. React Concurrency Patterns (ADD to frontend-patterns or coding-standards skill)

```markdown
## React State + Async Anti-Patterns
- NEVER call a function that reads state immediately after `setState` — the value hasn't updated
- Payment/confirmation callbacks must be driven by server state, never by click handlers
- `postMessage` listeners must validate `event.origin`
- `useEffect` with fetch should check if the component is in a terminal state before firing
- Iframe `sandbox` — never combine `allow-scripts` with `allow-same-origin`
```

**Why:** ARCH-1, SEC-8, MED-6, MED-11, LOW-4 would have been caught.

---

## 6. DI Registration Patterns (ADD to dotnet skill)

```markdown
## .NET DI Anti-Patterns to Flag
- Singleton service that resolves HttpClient at construction → defeats IHttpClientFactory
  Fix: Register as Transient or inject IHttpClientFactory, call CreateClient() per-request
- BackgroundService with direct deps → use IServiceScopeFactory for future-proofing
- Config validated with `??` (catches null) but not `IsNullOrWhiteSpace` (misses empty)
  Fix: Single validation helper: `GetRequiredString(key)` that rejects null AND whitespace
- Interface property exposing secrets → encapsulate behind a method
```

**Why:** HIGH-6, HIGH-5, MED-9, MED-12, LOW-3 would have been caught.

---

## 7. Test Quality Gates (ADD to tdd-guide agent)

The tdd-guide agent should enforce these gates before marking tests as "done":

```markdown
## Test Quality Gates
- [ ] Every `catch` block in production code has a test that triggers it
- [ ] Every HTML-producing endpoint has content assertions (not just `Assert.NotNull`)
- [ ] Every file I/O operation has a path traversal test case
- [ ] Every webhook handler has signature validation tests (valid, invalid, missing)
- [ ] Every state machine transition has a test, including terminal state
- [ ] Frontend components with user interaction have behavioral tests (click, type, submit)
- [ ] Serialization roundtrip tested for all domain types stored in JSON/DB
- [ ] Concurrent access scenarios tested for shared-state services
- [ ] Every `IAsyncEnumerable` / streaming endpoint has at least one integration-style test
```

**Why:** SEC-9, and all 11 test coverage gaps would have been caught.

---

## 8. SSRF Prevention (ADD to security-reviewer agent)

```markdown
## SSRF Checklist
- [ ] Any URL from user input validated against domain allowlist before fetch
- [ ] Only HTTPS scheme allowed (no http://, file://, ftp://)
- [ ] Internal/private IP ranges blocked (127.0.0.1, 169.254.x.x, 10.x.x.x, 172.16-31.x.x, 192.168.x.x)
- [ ] DNS rebinding prevention — resolve hostname, check IP, then fetch
```

**Why:** MED-1 would have been caught.

---

## 9. Observability Mandate (ADD to code-reviewer agent)

```markdown
## Observability Check (for any new feature with 3+ endpoints)
- [ ] ActivitySource with spans for key operations
- [ ] Meter with counters for business events (sessions created, states reached, payments)
- [ ] Structured logging with correlation IDs on all service methods
- [ ] No PII in span tags or log fields (hash or omit)
```

**Why:** MED-8, MED-4 would have been caught.

---

## 10. Slug/Identifier Deduplication (ADD to refactor-cleaner agent trigger)

The refactor-cleaner agent should scan for:
- Functions with the same name in different files but different implementations
- String constants used in multiple places without a shared constant
- Domain logic duplicated between tools and services

**Why:** HIGH-9 (duplicate `GenerateSlug`), LOW-6 (duplicate Anthropic client), MED-10 (magic strings).

---

## Summary: What Each Agent Gets

| Agent/Skill | New Additions |
|-------------|---------------|
| **security-reviewer** | Payment checklist, OAuth checklist, File I/O checklist, SSRF checklist |
| **code-reviewer** | Post-build smoke test, Observability mandate |
| **tdd-guide** | Test quality gates (9 mandatory checks) |
| **dotnet skill** | DI anti-patterns section |
| **frontend-patterns** | React concurrency anti-patterns |
| **refactor-cleaner** | Duplicate function detection trigger |

These additions would have caught **38 of the 44 findings** in this review. The remaining 6 are domain-specific edge cases that require human judgment.
