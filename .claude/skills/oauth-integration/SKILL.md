---
name: oauth-integration
description: OAuth 2.0 integration patterns and security. Use when writing, reviewing, or debugging any OAuth flow (Google, GitHub, etc.) including token exchange, callback handling, and postMessage communication.
---

# OAuth Integration Patterns

## Core Principles

1. **CSRF protection via nonce** — `state` parameter must be an unguessable nonce, not a session ID
2. **Single-use nonces** — clear after validation to prevent replay
3. **Origin-locked postMessage** — never use `'*'` as target origin
4. **HTML-safe output** — all data in callback HTML must be properly encoded

## Authorization URL Construction

```csharp
// Generate cryptographic nonce
var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
var state = $"{sessionId}:{nonce}";

// Store nonce in session for validation
session.OAuthNonce = nonce;
await sessionStore.SaveAsync(session, ct);

// Build URL with encoded state
var url = $"{authEndpoint}?state={Uri.EscapeDataString(state)}&...";
```

## Callback Handler Pattern

```csharp
// 1. Parse state into sessionId + nonce
var parts = state.Split(':', 2);
if (parts.Length != 2) return Error("Invalid state");

var sessionId = parts[0];
var nonce = parts[1];

// 2. Load session and verify nonce (constant-time comparison)
var session = await sessionStore.LoadAsync(sessionId, ct);
if (session?.OAuthNonce is null || !CryptographicOperations.FixedTimeEquals(
    Encoding.UTF8.GetBytes(nonce),
    Encoding.UTF8.GetBytes(session.OAuthNonce)))
{
    return Error("Invalid state");
}

// 3. Clear nonce (single-use)
session.OAuthNonce = null;

// 4. Exchange code for tokens
var tokens = await oAuthService.ExchangeCodeAsync(code, ct);
```

## Callback HTML (postMessage to Parent Window)

```csharp
// Use $$""" raw string literals for HTML with JS objects
// HTML-encode ALL user-visible text
var htmlMessage = HttpUtility.HtmlEncode(message);

// JS-encode for string literals (escape quotes, angle brackets, newlines)
var jsMessage = message
    .Replace("\\", "\\\\")
    .Replace("'", "\\'")
    .Replace("\"", "\\\"")
    .Replace("<", "\\x3c")
    .Replace(">", "\\x3e");

// ALWAYS specify exact target origin (from Platform:BaseUrl config)
return $$"""
    <script>
        window.opener?.postMessage({
            type: 'oauth_callback',
            success: {{successJs}},
            message: '{{jsMessage}}'
        }, '{{jsOrigin}}');
        window.close();
    </script>
    """;
```

## Frontend: Listening for OAuth Callback

```tsx
useEffect(() => {
  function handleMessage(event: MessageEvent) {
    // ALWAYS validate origin
    if (event.origin !== process.env.NEXT_PUBLIC_API_URL) return;
    if (event.data?.type !== "oauth_callback") return;

    if (event.data.success) {
      onConnected(event.data.email);
    } else {
      onError(event.data.message);
    }
  }

  window.addEventListener("message", handleMessage);
  return () => window.removeEventListener("message", handleMessage);
}, []);
```

## Anti-Patterns to Flag

| Anti-Pattern | Fix |
|---|---|
| `state=sessionId` (no nonce) | `state=sessionId:cryptoNonce` |
| `postMessage(data, '*')` | `postMessage(data, platformOrigin)` |
| `<p>${message}</p>` in HTML | `HttpUtility.HtmlEncode(message)` |
| Nonce not cleared after use | `session.OAuthNonce = null` after validation |
| `string.Equals(nonce, stored)` | `CryptographicOperations.FixedTimeEquals()` |
| Token stored without TTL | Set `ExpiresAt` and implement refresh/cleanup |

## Testing Requirements

- [ ] State parsing: valid `sessionId:nonce` format accepted
- [ ] State parsing: missing nonce rejected
- [ ] Nonce validation: correct nonce passes
- [ ] Nonce validation: wrong nonce rejected
- [ ] Nonce validation: reused nonce rejected (cleared after first use)
- [ ] Error parameter: Google error message handled gracefully
- [ ] Missing code parameter: returns error HTML
- [ ] Session not found: returns error HTML
- [ ] Token exchange failure: returns error HTML, nonce still cleared
- [ ] HTML output: no XSS vectors in message rendering
- [ ] postMessage: uses exact origin, not `'*'`
