---
name: api-frontend-contract
description: Enforce API + frontend contract consistency. Use when modifying API endpoints that have frontend callers — auth, headers, request/response shapes, URLs.
---

# API–Frontend Contract Enforcement

## The Rule

**Every API change that affects the HTTP contract MUST include the corresponding frontend update in the same commit.**

## What Triggers This

- Adding or changing authentication (bearer tokens, cookies, API keys)
- Adding or changing required headers (Authorization, X-Custom-Header)
- Changing request body shape (new required fields, renamed fields)
- Changing response body shape (new fields the frontend needs to read)
- Changing URL patterns or route parameters
- Adding rate limiting that requires client-side handling (429 retry)
- Changing content type (JSON → SSE, etc.)

## Checklist

Before committing an API endpoint change:

1. **Find all frontend callers**: Search `apps/platform` and `apps/agent-site` for the endpoint URL pattern (e.g., `grep -r "/onboard" apps/platform/`)
2. **Update fetch calls**: Add new headers, update request body, handle new response fields
3. **Update TypeScript types**: If response shape changed, update the TS interface
4. **Update props chain**: If a new value flows from API → page → component (e.g., a token), update every layer
5. **Test the full flow**: Hit the endpoint from the browser, not just curl

## Common Mistakes

| API Change | Forgotten Frontend Update |
|------------|--------------------------|
| Added `Authorization: Bearer` requirement | Frontend fetch doesn't send the header → 401 |
| Changed response from `{ id }` to `{ sessionId, token }` | Frontend still reads `data.id` → undefined |
| Added rate limiting | Frontend shows generic error instead of "slow down" |
| Changed to SSE streaming | Frontend tries to parse as JSON → crash |

## Example: Adding Auth

```csharp
// API: Added bearer token validation
if (!ValidateBearerToken(httpContext, session))
    return Results.Unauthorized();
```

```tsx
// Frontend: MUST also update — same commit
const res = await fetch(`${API_BASE}/onboard/${sessionId}/chat`, {
  headers: {
    "Content-Type": "application/json",
    "Authorization": `Bearer ${token}`,  // <-- MUST ADD THIS
  },
  body: JSON.stringify({ message: text }),
});
```

```tsx
// Page: MUST capture and pass the token
const data = await res.json();
setSessionToken(data.token);  // <-- MUST CAPTURE
// ...
<ChatWindow sessionId={sessionId} token={token} />  // <-- MUST PASS
```
