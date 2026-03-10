# Security Review Checklists

These checklists are triggered automatically when agents detect relevant code patterns.

## Payment Security Checklist
Triggered when: Stripe, payment, billing, subscription, checkout code is present.

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

## OAuth Security Checklist
Triggered when: OAuth, Google auth, token exchange, postMessage code is present.

- [ ] `state` parameter is a CSRF token (unguessable nonce), NOT a session ID
- [ ] `state` nonce is single-use — cleared after validation
- [ ] `postMessage` uses exact target origin, never `'*'`
- [ ] HTML callback pages use JSON serialization for data, never string interpolation
- [ ] Token storage has explicit TTL with cleanup
- [ ] Profile cross-validation happens before state advances
- [ ] All endpoints accessing session data require authentication
- [ ] Session IDs have sufficient entropy (128+ bits) or are protected by auth tokens

## File I/O Security Checklist
Triggered when: File.Read, File.Write, Path.Combine with user input.

- [ ] All user-supplied path components validated with allowlist regex
- [ ] `Path.GetFullPath` + `StartsWith(basePath)` check on every file operation
- [ ] Concurrent access protected with per-key SemaphoreSlim or file locks
- [ ] Write operations use write-to-temp-then-rename for atomicity
- [ ] File store operations log errors but don't expose paths to clients

## SSRF Prevention Checklist
Triggered when: HTTP requests made with user-supplied URLs (scraping, webhooks).

- [ ] Any URL from user input validated against domain allowlist before fetch
- [ ] Only HTTPS scheme allowed (no http://, file://, ftp://)
- [ ] Internal/private IP ranges blocked (127.0.0.1, 169.254.x.x, 10.x.x.x, 172.16-31.x.x, 192.168.x.x)
- [ ] DNS rebinding prevention — resolve hostname, check IP, then fetch
