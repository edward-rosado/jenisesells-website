# OpenAPI Pipeline + Typed API Client Migration

## Context

The `@real-estate-star/api-client` package was scaffolded but never completed — the generated types are a stub, the `api-client` CI workflow fails because the OpenAPI export crashes without secrets, and all frontend API call sites use raw `fetch()` with no type safety.

This spec covers three phases:
1. Fix the OpenAPI export so CI can produce `openapi.json`
2. Generate real TypeScript types from the spec
3. Migrate all frontend API calls to the typed client

### Intentional Exclusions

- **Turnstile verification** (`turnstile.ts`) — calls Cloudflare's API, not ours
- **OAuth postMessage** (`GoogleAuthCard.tsx`) — not an HTTP call
- **Telemetry** (`telemetry.ts`) — fire-and-forget `fetch` with `keepalive: true`. `openapi-fetch` doesn't support `keepalive` natively, and the call is intentionally fire-and-forget with swallowed errors. Not worth migrating.

## Phase 1: Fix OpenAPI Export in CI

### Problem

`dotnet run -- --export-openapi` starts the full app, which validates required secrets (Anthropic, Google, Stripe, Platform) at startup. CI doesn't have these secrets.

### Solution: appsettings.OpenApiExport.json

Create a dedicated environment config with placeholder values for all required secrets. Pass `ASPNETCORE_ENVIRONMENT=OpenApiExport` in the CI export step.

**File**: `apps/api/RealEstateStar.Api/appsettings.OpenApiExport.json`

```json
{
  "Anthropic": { "ApiKey": "placeholder-for-openapi-export" },
  "Google": {
    "ClientId": "placeholder",
    "ClientSecret": "placeholder",
    "RedirectUri": "http://localhost"
  },
  "Stripe": {
    "SecretKey": "sk_test_placeholder",
    "WebhookSecret": "whsec_placeholder",
    "PriceId": "price_placeholder"
  },
  "Platform": { "BaseUrl": "http://localhost:3000" }
}
```

**File**: `apps/api/RealEstateStar.Api/Program.cs`

Revert the `isOpenApiExport` placeholder hacks from the previous PR. The appsettings file provides the values — no conditional logic needed in code.

**File**: `.github/workflows/api.yml`

```yaml
- name: Export OpenAPI spec
  env:
    ASPNETCORE_ENVIRONMENT: OpenApiExport
  run: dotnet run --project apps/api/RealEstateStar.Api --no-build --configuration Release -- --export-openapi apps/api/openapi.json

- uses: actions/upload-artifact@v4
  with:
    name: openapi-spec
    path: apps/api/openapi.json
```

Remove `continue-on-error` — this should fail the build if it breaks.

### api-client Workflow

Already fixed in the previous PR. When the API workflow completes with an `openapi-spec` artifact, the `api-client` workflow:
1. Downloads the artifact
2. Runs `openapi-typescript openapi.json -o generated/types.ts`
3. Commits and pushes the generated types

**Note**: The workflow pushes directly to `main`. Since `main` is branch-protected, the Actions bot needs push bypass in repo settings (Settings → Branches → Branch protection → "Allow specified actors to bypass"). Alternatively, change the workflow to open a PR — but for auto-generated files, direct push is simpler.

**Merge conflict prevention**: Add `packages/api-client/generated/types.ts` to `.gitattributes` with `merge=theirs` so long-lived branches auto-resolve conflicts on the generated file.

## Phase 2: Generate Typed API Client

### Update client.ts

**File**: `packages/api-client/client.ts`

```typescript
import createClient from "openapi-fetch";
import type { paths } from "./generated/types";

/**
 * Create a typed API client. Static headers (e.g., bearer token) can be set
 * at construction time. Per-request headers (e.g., HMAC signature) should be
 * passed at the call site: client.POST("/path", { headers: { ... } }).
 */
export function createApiClient(baseUrl: string, headers?: Record<string, string>) {
  return createClient<paths>({
    baseUrl,
    headers,
  });
}

export type ApiClient = ReturnType<typeof createApiClient>;
```

**Important**: HMAC-signed calls (agent-site) MUST pass `X-Signature`, `X-Timestamp`, and `X-API-Key` **per-request** via the call-site `headers` option, NOT at client construction time. These values change with every request (timestamp + body-derived signature). Passing them at construction time would produce stale/replayable signatures.

### Update index.ts

**File**: `packages/api-client/index.ts`

```typescript
export { createApiClient } from "./client";
export type { ApiClient } from "./client";
export type { paths, components } from "./generated/types";
```

### Bootstrap Types Locally

Before CI generates types, we need a one-time manual generation:
1. Run the API locally (`dotnet run`)
2. Run `npm run generate --workspace=packages/api-client`
3. Commit the generated `types.ts`

This gives us real types to code against. CI will keep them up-to-date going forward.

## Phase 3: Migrate Frontend API Calls

### Migration Strategy

- **HMAC signing is unchanged** — `signAndForward()` keeps its signing logic, only the `fetch()` call inside gets replaced with the typed client
- **SSE streaming stays raw fetch** — `openapi-fetch` returns parsed JSON, but the chat endpoint returns `text/event-stream`. We keep raw `fetch` but import the request body type from generated types
- **OAuth postMessage stays as-is** — not an HTTP call, no migration needed

### Call Site Migrations

#### 1. Health Check — `apps/platform/features/status/useHealthCheck.ts`

**Before**: `fetch(API_URL + "/health/ready", { cache: "no-store" })`
**After**: `client.GET("/health/ready", { init: { cache: "no-store" } })`

The `cache: "no-store"` option is critical — without it, Next.js may serve cached health data, defeating the 30s polling.

#### 2. Create Onboarding Session — `apps/platform/app/onboard/page.tsx`

**Before**: `fetch(API_BASE + "/onboard", { method: "POST", body })`
**After**: `client.POST("/onboard", { body })`

#### 3. Verify Payment Status — `apps/platform/app/onboard/page.tsx`

**Before**: `fetch(API_BASE + "/onboard/" + sessionId)`
**After**: `client.GET("/onboard/{sessionId}", { params: { path: { sessionId } } })`

#### 4. Chat Stream — `apps/platform/features/onboarding/ChatWindow.tsx`

**Stays raw fetch** — SSE streaming. Import request body type:
```typescript
import type { components } from "@real-estate-star/api-client";
type ChatRequest = components["schemas"]["ChatRequest"]; // or equivalent
```

#### 5. OAuth Callback — `apps/platform/features/onboarding/GoogleAuthCard.tsx`

**No change** — postMessage, not an HTTP call.

#### 6. Lead Submission — `apps/agent-site/features/lead-capture/submit-lead.ts` + `hmac.ts`

`signAndForward()` computes HMAC headers, then calls `fetch`. After migration, it creates a typed client (no static headers) and passes HMAC headers **per-request**:

```typescript
// signAndForward() — HMAC computation unchanged
const signedHeaders = { "X-API-Key": apiKey, "X-Signature": sig, "X-Timestamp": ts };
const client = createApiClient(baseUrl); // no static headers
const { data, error } = await client.POST("/agents/{agentId}/leads", {
  params: { path: { agentId } },
  body: payload,
  headers: signedHeaders, // per-request — fresh signature each call
});
```

#### 7-8. Privacy Actions (via signAndForward) — `apps/agent-site/features/privacy/privacy.ts`

`requestOptOut`, `requestSubscribe`, and `requestDeletion` all go through `signAndForward()`. Same pattern as lead submission — HMAC headers per-request:

- `requestOptOut` → `client.POST("/agents/{agentId}/leads/opt-out", { headers: signedHeaders })`
- `requestSubscribe` → `client.POST("/agents/{agentId}/leads/subscribe", { headers: signedHeaders })`
- `requestDeletion` → `client.POST("/agents/{agentId}/leads/request-deletion", { headers: signedHeaders })`

#### 9. Data Export — `apps/agent-site/features/privacy/privacy.ts` (requestExport)

**Special case**: `requestExport` does NOT use `signAndForward()`. It has its own HMAC implementation that signs with the **raw** `hmacSecret` (not `secret:agentId` like `signAndForward`). This divergent signing MUST be preserved — do not refactor it into `signAndForward`.

```typescript
// requestExport — keeps its own signing, uses typed client for the HTTP call
const signedHeaders = { "X-API-Key": apiKey, "X-Signature": sig, "X-Timestamp": ts };
const client = createApiClient(baseUrl);
const { data, error } = await client.GET("/agents/{agentId}/leads/export", {
  params: { path: { agentId }, query: { email } }, // query param, not body
  headers: signedHeaders,
});
```

### Shared Client Instances

**Platform**: Create `apps/platform/lib/api.ts`:
```typescript
import { createApiClient } from "@real-estate-star/api-client";
const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5135";
export const api = createApiClient(API_URL);
```

**Agent Site**: The HMAC-signed calls create per-request clients (because headers include per-request signature). No shared instance needed.

## Phase 4: Google Maps API Key Restriction

Manual step in Google Cloud Console:
1. Go to APIs & Services → Credentials
2. Find the Maps API key (`AIzaSyCD8Lw5...`)
3. Under "Application restrictions": HTTP referrers
   - `*.real-estate-star.com/*`
   - `localhost:*`
4. Under "API restrictions": Places API (New) only

## Files to Modify

| File | Action | Phase |
|------|--------|-------|
| `apps/api/RealEstateStar.Api/appsettings.OpenApiExport.json` | **Create** — placeholder secrets for CI | 1 |
| `apps/api/RealEstateStar.Api/Program.cs` | **Modify** — revert placeholder hacks | 1 |
| `.github/workflows/api.yml` | **Modify** — add ASPNETCORE_ENVIRONMENT | 1 |
| `.gitattributes` | **Modify** — add `merge=theirs` for generated types | 1 |
| `packages/api-client/generated/types.ts` | **Regenerate** — from OpenAPI spec | 2 |
| `packages/api-client/client.ts` | **Modify** — import real types, document per-request headers | 2 |
| `packages/api-client/index.ts` | **Modify** — export types | 2 |
| `apps/platform/package.json` | **Modify** — add `@real-estate-star/api-client` dependency | 3 |
| `apps/agent-site/package.json` | **Modify** — add `@real-estate-star/api-client` dependency | 3 |
| `apps/platform/lib/api.ts` | **Create** — shared client instance | 3 |
| `apps/platform/features/status/useHealthCheck.ts` | **Modify** — use typed client with `cache: "no-store"` | 3 |
| `apps/platform/app/onboard/page.tsx` | **Modify** — use typed client | 3 |
| `apps/platform/features/onboarding/ChatWindow.tsx` | **Modify** — import request types | 3 |
| `apps/agent-site/features/shared/hmac.ts` | **Modify** — use typed client inside signAndForward (per-request headers) | 3 |
| `apps/agent-site/features/privacy/privacy.ts` | **Modify** — use typed client (preserve requestExport's divergent HMAC signing) | 3 |
| | | |
| **Documentation & CI** | | |
| `packages/api-client/README.md` | **Create** — usage guide, HMAC pattern, generation instructions | docs |
| `.gitattributes` | **Modify** — add `merge=theirs` for generated types | CI |
| `docs/architecture/google-maps-autocomplete-lifecycle.md` | **Already updated** in previous PR | done |
| `docs/architecture/shared-lead-form-component-hierarchy.md` | **Modify** — rename hook reference to `useGooglePlacesAutocomplete` | docs |
| `docs/architecture/observability-flow.md` | **Modify** — add correlation ID auto-injection detail | docs |
| `.claude/CLAUDE.md` | **Modify** — add api-client section with import examples | docs |
| `docs/onboarding.md` | **Modify** — add "Using the Typed API Client" section | docs |

## Observability Requirements

Every observability touchpoint must be preserved. The typed client changes the transport layer, not the telemetry.

| Touchpoint | Where | Preservation Strategy |
|------------|-------|----------------------|
| `X-Correlation-ID` | `api-client/client.ts` | Already auto-injected by `createApiClient` — no change needed |
| Sentry `captureException` | `submit-lead.ts`, `ChatWindow.tsx` | Keep `catch` blocks that call `Sentry.captureException(error)` |
| Sentry breadcrumbs | `ChatWindow.tsx` | Keep `trackOnboarding()` calls around API interactions |
| `trackFormEvent` telemetry | `CmaSection.tsx`, `telemetry.ts` | Excluded from migration — stays fire-and-forget raw `fetch` |
| GA4/Meta/GTM conversions | `Analytics.tsx` | Client-side only, not API calls — unaffected |
| 15-second `AbortController` timeout | `hmac.ts`, `privacy.ts` | Pass `signal` via `init` option: `client.POST("/path", { init: { signal } })` |
| Error response text capture | `submit-lead.ts` | `openapi-fetch` returns `{ error }` with parsed response — adapt error extraction |
| `cache: "no-store"` | `useHealthCheck.ts` | Pass via `init`: `client.GET("/path", { init: { cache: "no-store" } })` |
| Web Vitals | `WebVitalsReporter.tsx` | Console.debug only, no API call — unaffected |

### Correlation ID Flow (preserved)

```
createApiClient(baseUrl)
  → every request auto-includes X-Correlation-ID: crypto.randomUUID()
  → API logs correlate request → enrichment → notification
  → Grafana dashboards filter by correlation ID
```

This is already built into `client.ts` and carries forward unchanged.

## Security Impact

**None.** The HMAC signing, bearer token auth, origin validation, and all security headers are **unchanged**. The typed client is a type-safe wrapper around `fetch` — same bytes on the wire, same headers, same authentication. The only difference is compile-time type checking on request/response shapes.

## CI/CD Pipeline Impact

### Workflows that need changes

| Workflow | Change | Why |
|----------|--------|-----|
| `api.yml` | Add `ASPNETCORE_ENVIRONMENT=OpenApiExport`, remove `continue-on-error` | Fix OpenAPI export |
| `api-client.yml` | Already fixed (run-id, graceful skip) | Previous PR |

### Workflows that need NO changes (but must pass)

| Workflow | Validates | Status |
|----------|-----------|--------|
| `agent-site.yml` | Lint + test + coverage + build | Must pass with new api-client imports |
| `platform.yml` | Lint + test + coverage + build | Must pass with new api-client imports |
| `deploy-agent-site.yml` | Full test + Cloudflare deploy | Same validation as agent-site.yml |
| `deploy-platform.yml` | Full test + Cloudflare deploy | Same validation as platform.yml |
| `deploy-api.yml` | .NET build + test + Docker | Unaffected (backend only) |

### Prerequisites before merge

1. Both apps must declare `"@real-estate-star/api-client": "*"` in `package.json`
2. All `npm ci` workspace installs must resolve the api-client package
3. The api-client CI workflow needs either:
   - GitHub Actions bot bypass on branch protection (recommended), OR
   - Convert to PR-based workflow for generated type commits

## Verification

1. **CI pipeline**: API build produces `openapi-spec` artifact, `api-client` workflow generates types
2. **Type safety**: `tsc --noEmit` passes in both apps with the new typed calls
3. **Unit tests**: All existing tests pass (mocks updated for new import paths)
4. **Manual test**: All 10 call sites work end-to-end:
   - Platform: health check, onboarding create/verify/chat
   - Agent site: lead submission, opt-out, subscribe, delete, export
5. **Security**: HMAC signatures validate, bearer tokens work, origin checks pass
6. **Coverage**: 100% branch coverage maintained on all modified files
