# Deployment-Ready Onboarding — Staff Review

> **Date:** 2026-03-10
> **Branch:** `feat/deployment-ready-onboarding`
> **Reviewers:** Security, Architecture, Test Coverage (3 parallel agents)
> **Verdict:** **BLOCK — 10 unique CRITICAL findings, 14 HIGH, 12 MEDIUM, 8 LOW**

---

## How to Read This Document

Findings are deduplicated across all three reviews and organized by severity. Each finding includes:
- **ID** and **severity** (CRITICAL / HIGH / MEDIUM / LOW)
- **File:line** reference
- **Description** of the issue
- **Fix** — concrete code or approach

**CRITICAL** = must fix before any user touches this.
**HIGH** = must fix before production. Can demo locally with caution.
**MEDIUM** = fix before GA. Acceptable risk for internal demo.
**LOW** = fix when convenient.

---

## CRITICAL Findings (10)

### SEC-1: No Authentication on Any Onboarding Endpoint — Full IDOR

**Files:** `CreateSessionEndpoint.cs`, `PostChatEndpoint.cs`, `GetSessionEndpoint.cs`

No auth middleware, no token, no cookie. Anyone who knows a 12-hex-char session ID can:
- Read the full session (name, phone, email, brokerage, conversation history)
- Inject messages into another user's chat
- Drive the state machine forward (trigger Stripe checkout for someone else)

Session IDs are 48 bits of entropy — not brute-forceable, but not authentication.

**Fix:** Return a bearer token on session creation. Require it on all subsequent calls.

```csharp
// OnboardingSession.cs
public required string BearerToken { get; init; }

// CreateSessionEndpoint — return token in response
return Results.Ok(new CreateSessionResponse(session.Id, session.BearerToken));

// PostChatEndpoint, GetSessionEndpoint — validate
var bearer = httpContext.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
if (session.BearerToken != bearer) return Results.Unauthorized();
```

---

### SEC-2: Path Traversal in `JsonFileSessionStore`

**File:** `JsonFileSessionStore.cs:32`

```csharp
private string GetPath(string sessionId) => Path.Combine(basePath, $"{sessionId}.json");
```

`sessionId` comes from HTTP route params and OAuth `state` query param. `../../appsettings.json` reads arbitrary files.

**Fix:**
```csharp
[GeneratedRegex(@"^[a-f0-9]{12}$")]
private static partial Regex SessionIdRegex();

private string GetPath(string sessionId)
{
    if (!SessionIdRegex().IsMatch(sessionId))
        throw new ArgumentException("Invalid session ID format", nameof(sessionId));
    var fullPath = Path.GetFullPath(Path.Combine(basePath, $"{sessionId}.json"));
    if (!fullPath.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("Path traversal detected", nameof(sessionId));
    return fullPath;
}
```

---

### SEC-3: No File Locking on Session Store — Concurrent Write Corruption

**File:** `JsonFileSessionStore.cs:16-22`

`File.WriteAllTextAsync` truncates then writes. Two concurrent requests (chat + webhook) produce torn JSON or last-write-wins data loss. Payment state can be silently overwritten.

**Fix:**
```csharp
private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

public async Task SaveAsync(OnboardingSession session, CancellationToken ct)
{
    var sem = _locks.GetOrAdd(session.Id, _ => new SemaphoreSlim(1, 1));
    await sem.WaitAsync(ct);
    try
    {
        var path = GetPath(session.Id);
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(session, JsonOptions), ct);
        File.Move(tmp, path, overwrite: true); // Atomic replace
    }
    finally { sem.Release(); }
}
```

---

### SEC-4: OAuth `state` = Session ID — No CSRF Protection

**File:** `GoogleOAuthCallbackEndpoint.cs:29`

The `state` parameter is used directly as the session ID. An attacker who knows a session ID can craft a callback URL attaching their Google account to a victim's session.

**Fix:** Generate a separate `OAuthNonce` stored on the session. Pass `state = "{sessionId}:{nonce}"`. Validate nonce in callback.

```csharp
// StartGoogleOAuthEndpoint — before redirect:
session.OAuthNonce = Guid.NewGuid().ToString("N");
await sessionStore.SaveAsync(session, ct);
// Build URL with state = $"{session.Id}:{session.OAuthNonce}"

// GoogleOAuthCallbackEndpoint — in Handle:
var parts = state.Split(':');
if (parts.Length != 2) return Results.Content(BuildCallbackHtml(false, "Invalid state"), "text/html");
var session = await sessionStore.LoadAsync(parts[0], ct);
if (session?.OAuthNonce != parts[1]) return Results.Content(BuildCallbackHtml(false, "Invalid state"), "text/html");
session.OAuthNonce = null; // Single-use
```

---

### SEC-5: XSS in OAuth Callback HTML

**File:** `GoogleOAuthCallbackEndpoint.cs:54-65`

`BuildCallbackHtml` embeds user-controlled data (Google display name, error params) into HTML and JS with manual escaping. Manual escaping is broken — doesn't cover backticks, template literals, HTML entities, `</script>` injection.

**Fix:** Serialize payload as JSON, embed in a `<script type="application/json">` tag, parse in JS:

```csharp
var payload = JsonSerializer.Serialize(new { success, message });
return $$"""
    <!DOCTYPE html><html><body>
    <script id="cfg" type="application/json">{{payload}}</script>
    <script>
      const d = JSON.parse(document.getElementById('cfg').textContent);
      window.opener?.postMessage({ type: 'google_oauth_callback', ...d }, '{{platformOrigin}}');
      window.close();
    </script></body></html>
    """;
```

---

### SEC-6: `postMessage` Target Origin Is `'*'`

**File:** `GoogleOAuthCallbackEndpoint.cs:63`

Any page open in the browser can receive the OAuth confirmation message (including Google name/email). Must be scoped to the platform origin.

**Fix:** Pass `Platform:BaseUrl` config through and use it as the target origin (see SEC-5 fix above).

---

### SEC-7: Webhook Replay — No Idempotency Check

**File:** `StripeWebhookEndpoint.cs:76-82`

Stripe delivers webhooks "at least once." No deduplication. Two overlapping deliveries can trigger duplicate downstream actions.

**Fix:**
```csharp
// OnboardingSession.cs
public string? ProcessedStripeSessionId { get; set; }

// StripeWebhookEndpoint — before Advance:
if (session.ProcessedStripeSessionId == checkoutSession.Id)
{
    logger.LogInformation("Stripe session {Id} already processed", checkoutSession.Id);
    return Results.Ok();
}
session.ProcessedStripeSessionId = checkoutSession.Id;
```

---

### SEC-8: `PaymentCard` Calls `onPaymentComplete()` Before Payment Confirmed

**File:** `PaymentCard.tsx:8-12`

```tsx
function handleClick() {
    window.open(checkoutUrl, "_blank", "noopener,noreferrer");
    onPaymentComplete(); // Fires IMMEDIATELY — before payment happens
}
```

Users see "Trial Activated" by clicking the button, whether they paid or not. This is a business-critical correctness bug.

**Fix:** Remove `onPaymentComplete()` from click handler entirely. Show "Waiting for payment confirmation..." after redirect. Drive UI state from polling `GET /onboard/{sessionId}` until `TrialActivated`, or use SSE.

---

### SEC-9: Webhook Signature Verification Is Never Tested

**File:** `StripeWebhookEndpointTests.cs`

Tests call `HandleWebhookEvent` directly with a pre-constructed `Event` object, completely skipping the `EventUtility.ConstructEvent(json, signature, secret)` code path. The entire HMAC verification layer — the primary defense against forged webhooks — has zero test coverage.

**Fix:** Write tests that exercise `Handle` with raw HTTP requests: missing signature → 400, invalid signature → 400, valid signature → 200.

---

### ARCH-1: `ChatWindow.handleAction` Has a Stale Closure — Card Actions Are Completely Broken

**File:** `ChatWindow.tsx:96-101`

```tsx
function handleAction(action: string, data?: unknown) {
    const text = ...;
    setInput(text);  // Async state update
    handleSend();    // Reads OLD input from closure
}
```

`handleSend()` reads `input` from the closure, not the updated value. All interactive card actions (confirm profile, approve site, etc.) silently send the wrong message.

**Fix:** Extract sending logic to accept text as a parameter:
```tsx
async function sendMessage(text: string) { /* ... */ }
function handleSend() { sendMessage(input.trim()); }
function handleAction(action: string, data?: unknown) {
    const text = data ? `[Action: ${action}] ${JSON.stringify(data)}` : `[Action: ${action}]`;
    sendMessage(text);
}
```

---

## HIGH Findings (14)

### HIGH-1: Webhook Endpoint Subject to Global Rate Limiter

**File:** `Program.cs:256`

Stripe webhook IPs share rate limit buckets. Legitimate webhooks can be dropped with 429.

**Fix:** `app.MapPost("/webhooks/stripe", ...).DisableRateLimiting();`

---

### HIGH-2: No Rate Limiting on Session Create or Chat Endpoints

**Files:** `CreateSessionEndpoint.cs`, `PostChatEndpoint.cs`

Unlimited session creation = disk exhaustion. Unlimited chat = Claude API cost bomb.

**Fix:** Add named policies: `session-create` (5/hour per IP), `chat-message` (20/min per sessionId).

---

### HIGH-3: Rate Limiter Uses `RemoteIpAddress` Without `ForwardedHeaders`

**File:** `Program.cs:184`

Behind any proxy (Cloudflare, Railway), all users share one IP bucket.

**Fix:** Add `app.UseForwardedHeaders()` with `KnownProxies` before rate limiter.

---

### HIGH-4: `GetSessionResponse` Leaks Full PII + Message History

**File:** `OnboardingMappers.cs:14-24`

Unauthenticated `GET /onboard/{sessionId}` returns name, phone, email, full conversation. Even after auth is added, evaluate whether Messages need to be in the response.

---

### HIGH-5: `WebhookSecret` Exposed as Public Interface Property

**File:** `IStripeService.cs:6`

Raw secret accessible to any code holding `IStripeService`. Should be encapsulated.

**Fix:** Replace with `Event ConstructWebhookEvent(string payload, string signatureHeader)`.

---

### HIGH-6: Singleton Services Defeat `IHttpClientFactory`

**File:** `Program.cs:69-82`

`OnboardingChatService`, `GoogleOAuthService`, `ProfileScraperService` all resolve `HttpClient` once at construction. Defeats DNS rotation and handler lifecycle.

**Fix:** Register as Transient, or inject `IHttpClientFactory` and call `CreateClient()` per-request.

---

### HIGH-7: `SubmitCmaFormTool` and `DeploySiteTool` Swallow All Exceptions Without Logging

**Files:** `SubmitCmaFormTool.cs:43`, `DeploySiteTool.cs:17`

```csharp
catch (Exception) { return "The CMA demo encountered an issue..."; }
```

No logging. "The team has been notified" is false. Pipeline failures are invisible.

**Fix:** Inject `ILogger`, log at Error level with session ID before returning friendly message.

---

### HIGH-8: Duplicate User Message in Claude Conversation History

**Files:** `PostChatEndpoint.cs:23-27`, `OnboardingChatService.cs:126-142`

`PostChatEndpoint` appends user message to `session.Messages`. `BuildMessages` iterates `session.Messages` then adds `userMessage` again. Claude sees the user message twice.

**Fix:** Remove the duplicate add — either append in the endpoint OR in `BuildMessages`, not both.

---

### HIGH-9: `GenerateSlug` Duplicated With Different Implementations

**Files:** `SiteDeployService.cs:48-54` vs `SubmitCmaFormTool.cs:97-98`

`SiteDeployService` strips special chars. `SubmitCmaFormTool` only lowercases and replaces spaces. "O'Brien" → `o'brien` (unsafe).

**Fix:** Extract to a shared `OnboardingHelpers.GenerateSlug()` using the sanitizing version.

---

### HIGH-10: `DriveFolderInitializer` In-Memory State Lost on Restart

**File:** `DriveFolderInitializer.cs:26-33`

`ConcurrentDictionary` tracking initialized agents is lost on restart. Folders will be re-created on every deploy. Also: unbounded memory growth.

**Fix:** Store initialized flag in session. Or rely on Drive's idempotency (check-before-create in GwsService).

---

### HIGH-11: State Machine Allows No Terminal State Guard

**File:** `OnboardingStateMachine.cs`

Nothing prevents operating on a `TrialActivated` session. No "completed" check. Sessions should be immutable once terminal.

---

### HIGH-12: `OnboardingChatService` SSE Pipeline Has Zero Test Coverage

**File:** `ChatServiceTests.cs`

The entire SSE parsing, tool dispatch, message accumulation, and system prompt logic is tested with only `Assert.NotNull(_service)`. The `HttpMessageHandler` mock pattern already exists in the codebase (see `ProfileScraperTests`).

---

### HIGH-13: `ScrapeUrlTool` Has No Test File

No tests for: missing `url` param, scraper returns null, URL with control characters.

---

### HIGH-14: `ChatWindow` Interactive Flows Are Untested

**File:** `ChatWindow.test.tsx`

Only static rendering tested. No tests for: sending messages, SSE streaming, error fallback, Enter key submit, disabled state during send.

---

## MEDIUM Findings (12)

| ID | Issue | File |
|----|-------|------|
| MED-1 | SSRF — `profileUrl` fetched without domain allowlist | `ProfileScraperService.cs:43` |
| MED-2 | Google OAuth advances state without email cross-validation | `GoogleOAuthCallbackEndpoint.cs:37` |
| MED-3 | Empty string email silently passed to Stripe | `CreateStripeSessionTool.cs:11` |
| MED-4 | Agent PII sent to Claude system prompt every turn (unnecessary fields per state) | `OnboardingChatService.cs:179` |
| MED-5 | Trial expiry service is a stub — agents never charged | `TrialExpiryService.cs:22` |
| MED-6 | `SitePreview` iframe `allow-scripts + allow-same-origin` defeats sandbox | `SitePreview.tsx:12` |
| MED-7 | `$900` hardcoded in frontend PaymentCard AND system prompt | `PaymentCard.tsx:16`, `OnboardingChatService.cs:173` |
| MED-8 | No observability on Onboarding — zero ActivitySource spans or metrics | All services |
| MED-9 | `Cloudflare:ApiToken` defaults to empty string instead of fail-fast | `Program.cs:61` |
| MED-10 | `ChatMessage.Role` is raw string — "user"/"assistant" are magic strings | `ChatMessage.cs:5` |
| MED-11 | `postMessage` origin not validated in `GoogleAuthCard.tsx` | `GoogleAuthCard.tsx` |
| MED-12 | `TrialExpiryService` should use `IServiceScopeFactory` for future scoped deps | `TrialExpiryService.cs` |

---

## LOW Findings (8)

| ID | Issue | File |
|----|-------|------|
| LOW-1 | Session ID only 48 bits (acceptable once auth added) | `OnboardingSession.cs:20` |
| LOW-2 | Stripe Checkout created without idempotency key | `StripeService.cs:59` |
| LOW-3 | Stripe key validation duplicated at startup | `Program.cs:51`, `StripeService.cs:34` |
| LOW-4 | `OnboardPage` creates orphan session on payment return navigation | `onboard/page.tsx:16` |
| LOW-5 | `DomainService` mutates session directly instead of returning result | `DomainService.cs:16` |
| LOW-6 | Anthropic API client logic duplicated in ProfileScraper and ChatService | Both files |
| LOW-7 | Google "G" logo SVG duplicated in GoogleAuthCard | `GoogleAuthCard.tsx` |
| LOW-8 | `GoogleTokens.IsExpired` 5-minute buffer boundary not precisely tested | `GoogleTokensTests.cs` |

---

## Test Coverage Gaps (from QA Review)

### Critical Gaps
1. **Stripe webhook signature verification** — never tested (SEC-9)
2. **XSS in OAuth callback HTML** — no content assertions on HTML output
3. **Path traversal in session store** — no test for `../../` session IDs
4. **`OnboardingChatService` streaming** — placeholder test only

### High Gaps
1. **`ChatWindow` user interactions** — no send, SSE, error tests
2. **`ScrapeUrlTool`** — no test file at all
3. **`PostChatEndpoint` input validation** — no null/empty/huge message tests
4. **OAuth callback wrong-state replay** — not tested
5. **Session `GoogleTokens` serialization roundtrip** — not tested
6. **`ProfileScraperService` "not_agent_profile" error path** — not tested

### Medium Gaps
1. **Frontend `onboard.test.tsx`** — fetch failure not tested
2. **`MessageRenderer` card actions** — `onAction` callbacks never asserted
3. **`StateMachine` terminal state** — calling `Advance` from `TrialActivated` not tested
4. **`GoogleOAuthService` profile endpoint failure** — wrong exception type
5. **Concurrent session writes** — no race condition test

---

## Remediation Priority

### Before ANY user demo (do these first)
1. SEC-8: Fix `PaymentCard.onPaymentComplete()` firing on click
2. ARCH-1: Fix `handleAction` stale closure (card actions are broken)
3. HIGH-8: Fix duplicate user message in Claude history
4. SEC-2: Add session ID format validation (path traversal)
5. SEC-3: Add session file locking

### Before sharing the URL
6. SEC-1: Add bearer token auth on all endpoints
7. SEC-4: Add OAuth CSRF nonce
8. SEC-5 + SEC-6: Fix XSS and `postMessage` origin
9. SEC-7: Add webhook idempotency
10. HIGH-7: Add exception logging to tools

### Before production
11. HIGH-1-3: Fix rate limiting (webhook exclusion, per-endpoint limits, forwarded headers)
12. HIGH-6: Fix HttpClient singleton anti-pattern
13. MED-1: Add SSRF domain allowlist
14. MED-8: Add observability
15. All test coverage gaps
