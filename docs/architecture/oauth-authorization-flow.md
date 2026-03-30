# OAuth Authorization Link Flow

Enables agents to connect their Google Workspace account to Real Estate Star.
Uses stateless HMAC-signed links (no session required) with single-use CSRF nonces.

---

## Overview

```
Eddie (admin)                    Agent                            Google
     │                               │                               │
     │  POST /internal/oauth/        │                               │
     │   generate-link               │                               │
     │  (Bearer AdminToken)          │                               │
     ▼                               │                               │
 GenerateAuthLinkEndpoint            │                               │
 - Validates Bearer token            │                               │
   (constant-time compare)           │                               │
 - HMAC-signs: accountId.            │                               │
   agentId.email.exp                 │                               │
 - Returns signed URL                │                               │
     │                               │                               │
     │  Sends signed URL to agent ──►│                               │
     │  (e.g., via email)            │                               │
     │                               │                               │
     │                               │  Opens URL in browser         │
     │                               │  GET /oauth/google/authorize  │
     │                               ▼                               │
     │                        AuthorizeLinkEndpoint                  │
     │                        - Validates HMAC sig                   │
     │                          (constant-time compare)              │
     │                        - Renders landing page                 │
     │                          (hello email, "Connect with Google") │
     │                               │                               │
     │                               │  Clicks "Connect with Google" │
     │                               │  POST /oauth/google/authorize/connect
     │                               ▼                               │
     │                        ConnectEndpoint                        │
     │                        - Re-validates HMAC                    │
     │                        - Generates 32-char hex nonce          │
     │                        - Binds nonce → (accountId,            │
     │                          agentId, email) in memory            │
     │                        - state = nonce (nonce-only —          │
     │                          avoids delimiter injection)          │
     │                        - Redirects to Google OAuth ──────────►│
     │                               │                               │
     │                               │  Agent completes OAuth ───────┤
     │                               │                               │
     │                               │  Google redirects to ─────────►
     │                               │  GET /oauth/google/authorize/callback
     │                               ▼                               │
     │                        AuthorizeLinkCallbackEndpoint          │
     │                        - Validates state format               │
     │                          (32-char lowercase hex)              │
     │                        - ValidateAndConsumeNonce(state)       │
     │                          → extracts bound identity            │
     │                          (single-use, atomic remove)          │
     │                        - Exchanges code for tokens            │
     │                        - Verifies Google email ==             │
     │                          nonce-bound email                    │
     │                        - Persists tokens → ITokenStore        │
     │                        - Enqueues ActivationRequest           │
     │                          → Channel<ActivationRequest>         │
     │                        - Renders success HTML                 │
     │                               │                               │
     │                               │  "Google account connected!"  │
     │                               ▼                               │
     │                    ActivationOrchestrator                     │
     │                    (5-phase background pipeline)              │
```

---

## Security Design

### HMAC Link Signing

Payload: `{accountId}.{agentId}.{email}.{exp}` (dot-delimited, URL-safe)

- Signed with `OAuthLink:Secret` (HMACSHA256, 64-char hex output)
- Expiry enforced server-side; default 24 hours, max 72 hours
- Validated on both `GET /authorize` (landing) and `POST /authorize/connect`

### Bearer Token (Admin Endpoint)

`POST /internal/oauth/generate-link` requires `Authorization: Bearer <OAuthLink:AdminToken>`.
Comparison uses `CryptographicOperations.FixedTimeEquals` to prevent timing oracle attacks.
Token is optional in dev (if not configured, the endpoint is open — protect with network rules).

### CSRF Nonce

- Generated per-`POST /connect` request: 16 random bytes → 32-char lowercase hex
- Stored in `ConcurrentDictionary<nonce, NonceEntry>` (10-minute TTL, cleanup every 5 min)
- Bound to `(accountId, agentId, email)` at generation time (not derivable from state)
- State parameter sent to Google is the **nonce only** — identity is never embedded in state
  (prevents delimiter injection and forged accountId/agentId substitution)
- Single-use: `TryRemove` atomically consumes the nonce; second use always fails

### Email Cross-Validation

After token exchange, Google's `id_token` email is compared against the nonce-bound email:
- Case-insensitive, trimmed comparison
- Mismatch returns a "Google Account Mismatch" error (tokens NOT persisted)
- Prevents an agent from authorizing with a different Google account than requested

---

## Endpoints

| Method | Path | Auth | Rate Limit | Description |
|--------|------|------|------------|-------------|
| `POST` | `/internal/oauth/generate-link` | Bearer `OAuthLink:AdminToken` | `oauth-link-generate` (10/hr) | Generate HMAC-signed link |
| `GET` | `/oauth/google/authorize` | HMAC sig in query params | `oauth-link-authorize` (5/hr) | Landing page |
| `POST` | `/oauth/google/authorize/connect` | HMAC sig in form body | `oauth-link-authorize` (5/hr) | Generate nonce + redirect to Google |
| `GET` | `/oauth/google/authorize/callback` | Nonce in `state` param | `oauth-link-authorize` (5/hr) | Exchange code, store tokens, enqueue activation |

---

## Token Storage

Tokens are persisted via `ITokenStore` (Domain interface) → `AzureTableTokenStore` (Clients.Azure).
- DPAPI-encrypted at rest
- ETag-based optimistic locking (concurrent updates safe)
- Keyed by `(accountId, agentId, provider=Google)`

---

## Error Codes

| Code | Condition |
|------|-----------|
| `[OAUTH-LINK-090]` | Invalid/missing Bearer token on generate-link |
| `[OAUTH-LINK-100]` | Link generated successfully |
| `[OAUTH-LINK-200]` | Expired link accessed (410 Gone) |
| `[OAUTH-LINK-201]` | Invalid HMAC signature on landing page |
| `[OAUTH-LINK-202]` | Valid link accessed |
| `[OAUTH-LINK-300]` | Invalid HMAC on connect POST |
| `[OAUTH-LINK-301]` | Redirecting to Google OAuth |
| `[OAUTH-LINK-400]` | Google returned error param |
| `[OAUTH-LINK-401]` | Invalid state format |
| `[OAUTH-LINK-402]` | Invalid or expired nonce |
| `[OAUTH-LINK-403]` | No auth code in callback |
| `[OAUTH-LINK-404]` | Google email mismatch |
| `[OAUTH-LINK-405]` | Tokens persisted |
| `[OAUTH-LINK-406]` | Activation enqueued |
| `[OAUTH-LINK-407]` | Token exchange failed |

---

## Project Locations

```
Api/Features/OAuth/
  GenerateLink/
    GenerateAuthLinkEndpoint.cs     ← POST /internal/oauth/generate-link
  AuthorizeLink/
    AuthorizeLinkEndpoint.cs        ← GET /oauth/google/authorize (landing page)
    ConnectEndpoint.cs              ← POST /oauth/google/authorize/connect
    AuthorizeLinkCallbackEndpoint.cs← GET /oauth/google/authorize/callback
  Services/
    AuthorizationLinkService.cs     ← HMAC signing + nonce management
```
