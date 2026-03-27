# OpenAPI Pipeline + Typed API Client Migration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the OpenAPI export pipeline, generate real TypeScript types, and migrate all frontend API calls to the typed `@real-estate-star/api-client`.

**Architecture:** Five phases — (1) fix CI to produce `openapi.json`, (2) generate types and update the client package, (3) migrate 9 frontend call sites to the typed client, (4) documentation, (5) verify and ship. Plus a manual prerequisite (Google Maps API key restriction).

**Manual prerequisite (do before deploying):** Restrict the Google Maps API key in Google Cloud Console — HTTP referrers: `*.real-estate-star.com/*` + `localhost:*`, API restriction: Places API (New) only.

**Tech Stack:** .NET 10 (API), openapi-typescript + openapi-fetch (client), Next.js 16 (both apps), GitHub Actions (CI)

**Spec:** `docs/superpowers/specs/2026-03-23-openapi-pipeline-typed-client-design.md`

---

## Phase 1: Fix OpenAPI Export in CI

### Task 1: Create appsettings.OpenApiExport.json

**Files:**
- Create: `apps/api/RealEstateStar.Api/appsettings.OpenApiExport.json`

- [ ] **Step 1: Create the placeholder config file**

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

- [ ] **Step 2: Verify .NET picks it up**

Run: `ASPNETCORE_ENVIRONMENT=OpenApiExport dotnet run --project apps/api/RealEstateStar.Api -- --export-openapi test-openapi.json`
Expected: File `test-openapi.json` is created (may still crash on DI, that's OK — config validation passes)

- [ ] **Step 3: Commit**

```bash
git add apps/api/RealEstateStar.Api/appsettings.OpenApiExport.json
git commit -m "feat: add appsettings.OpenApiExport.json for CI OpenAPI export"
```

### Task 2: Clean up Program.cs placeholder hacks

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Program.cs:109-130`

- [ ] **Step 1: Revert the isOpenApiExport conditional logic**

Replace lines 109-130 with clean validation (the appsettings file now provides values):

```csharp
// Configuration keys
var anthropicKey = builder.Configuration["Anthropic:ApiKey"]
    ?? throw new InvalidOperationException("Anthropic:ApiKey configuration is required");
var googleClientId = builder.Configuration["Google:ClientId"]
    ?? throw new InvalidOperationException("Google:ClientId configuration is required");
var googleClientSecret = builder.Configuration["Google:ClientSecret"]
    ?? throw new InvalidOperationException("Google:ClientSecret configuration is required");
var googleRedirectUri = builder.Configuration["Google:RedirectUri"]
    ?? throw new InvalidOperationException("Google:RedirectUri configuration is required");

// Stripe config validation
_ = builder.Configuration["Stripe:SecretKey"]
    ?? throw new InvalidOperationException("Stripe:SecretKey configuration is required");
_ = builder.Configuration["Stripe:WebhookSecret"]
    ?? throw new InvalidOperationException("Stripe:WebhookSecret configuration is required");
_ = builder.Configuration["Stripe:PriceId"]
    ?? throw new InvalidOperationException("Stripe:PriceId configuration is required");
_ = builder.Configuration["Platform:BaseUrl"]
    ?? throw new InvalidOperationException("Platform:BaseUrl configuration is required");
```

Also remove the `var isOpenApiExport = args.Contains("--export-openapi");` line above (line 110). The `--export-openapi` check later in Program.cs (around line 668) stays — it only needs `args`, not the removed variable.

- [ ] **Step 2: Verify build**

Run: `dotnet build apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj`
Expected: Build succeeded. 0 Error(s)

- [ ] **Step 3: Verify export works with new appsettings**

Run: `ASPNETCORE_ENVIRONMENT=OpenApiExport dotnet run --project apps/api/RealEstateStar.Api -- --export-openapi test-openapi.json && cat test-openapi.json | head -5 && rm test-openapi.json`
Expected: JSON output with OpenAPI spec

- [ ] **Step 4: Run API tests**

Run: `dotnet test apps/api/RealEstateStar.Api.sln --configuration Release`
Expected: All tests pass

- [ ] **Step 5: Commit**

```bash
git add apps/api/RealEstateStar.Api/Program.cs
git commit -m "refactor: remove isOpenApiExport placeholder hacks — appsettings handles it"
```

### Task 3: Update api.yml workflow

**Files:**
- Modify: `.github/workflows/api.yml:55-63`

- [ ] **Step 1: Update the Export OpenAPI spec step**

Replace the export and upload steps:

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

Key changes: added `ASPNETCORE_ENVIRONMENT: OpenApiExport`, removed `continue-on-error: true`, removed `if: hashFiles(...)` condition on upload.

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/api.yml
git commit -m "fix: pass OpenApiExport environment to CI export step"
```

### Task 4: Add .gitattributes merge strategy

**Files:**
- Modify: `.gitattributes` (create if doesn't exist)

- [ ] **Step 1: Add merge=theirs for generated types**

Append to `.gitattributes`:
```
packages/api-client/generated/types.ts merge=theirs
```

- [ ] **Step 2: Commit**

```bash
git add .gitattributes
git commit -m "chore: add merge=theirs for auto-generated api-client types"
```

---

## Phase 2: Generate Typed API Client

### Task 5: Bootstrap types locally + update client

**Files:**
- Modify: `packages/api-client/client.ts`
- Modify: `packages/api-client/index.ts`
- Regenerate: `packages/api-client/generated/types.ts`

- [ ] **Step 1: Generate types from running API**

Start the API locally, then generate:

```bash
# Terminal 1: start API
dotnet run --project apps/api/RealEstateStar.Api

# Terminal 2: generate types
npm run generate --workspace=packages/api-client
```

Expected: `packages/api-client/generated/types.ts` contains real type definitions (interfaces for paths, components, etc.)

- [ ] **Step 2: Update client.ts with real types**

Replace `packages/api-client/client.ts`:

```typescript
import createClient from "openapi-fetch";
import type { paths } from "./generated/types";
import { createCorrelationId } from "@real-estate-star/domain";

/**
 * Create a typed API client. Static headers (e.g., bearer token) can be set
 * at construction time. Per-request headers (e.g., HMAC signature) should be
 * passed at the call site: client.POST("/path", { headers: { ... } }).
 */
export function createApiClient(baseUrl: string, headers?: Record<string, string>) {
  return createClient<paths>({
    baseUrl,
    headers: {
      "X-Correlation-ID": createCorrelationId(),
      ...headers,
    },
  });
}

export type ApiClient = ReturnType<typeof createApiClient>;
```

- [ ] **Step 3: Update index.ts exports**

Replace `packages/api-client/index.ts`:

```typescript
export { createApiClient } from "./client";
export type { ApiClient } from "./client";
export type { paths, components } from "./generated/types";
```

- [ ] **Step 4: Verify TypeScript compiles**

Run: `npx tsc --noEmit -p packages/api-client/tsconfig.json`
Expected: No errors

- [ ] **Step 5: Commit**

```bash
git add packages/api-client/
git commit -m "feat: generate real OpenAPI types and update typed client"
```

### Task 6: Add api-client dependency to both apps

**Files:**
- Modify: `apps/platform/package.json`
- Modify: `apps/agent-site/package.json`

- [ ] **Step 1: Add dependency to platform**

Add `"@real-estate-star/api-client": "*"` to the `dependencies` section of `apps/platform/package.json`.

- [ ] **Step 2: Add dependency to agent-site**

Add `"@real-estate-star/api-client": "*"` to the `dependencies` section of `apps/agent-site/package.json`.

- [ ] **Step 3: Install**

Run: `npm install`
Expected: Lock file updated, no errors

- [ ] **Step 4: Commit**

```bash
git add apps/platform/package.json apps/agent-site/package.json package-lock.json
git commit -m "chore: add @real-estate-star/api-client dependency to both apps"
```

---

## Phase 3: Migrate Frontend API Calls

### Task 7: Create shared platform client instance

**Files:**
- Create: `apps/platform/lib/api.ts`

- [ ] **Step 1: Create the shared client**

```typescript
import { createApiClient } from "@real-estate-star/api-client";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5135";

export const api = createApiClient(API_URL);
```

- [ ] **Step 2: Commit**

```bash
git add apps/platform/lib/api.ts
git commit -m "feat: create shared typed API client instance for platform"
```

### Task 8: Migrate health check

**Files:**
- Modify: `apps/platform/features/status/useHealthCheck.ts`
- Check: `apps/platform/features/status/__tests__/` (if tests exist)

- [ ] **Step 1: Replace raw fetch with typed client**

In `useHealthCheck.ts`, import the client at the top:

```typescript
import { createApiClient } from "@real-estate-star/api-client";
```

Create the client once outside the hook (module-level), NOT inside `doFetch`:

```typescript
const client = createApiClient(apiUrl);
```

Replace the `doFetch` body. Before:

```typescript
const res = await fetch(`${apiUrl}/health/ready`, { cache: "no-store" });
if (!res.ok) { /* error handling */ }
const data: HealthResponse = await res.json();
```

After:

```typescript
const { data, error, response } = await client.GET("/health/ready", {
  init: { cache: "no-store" },
});
if (error || !response.ok) {
  const sample: UptimeSample = { time: new Date(), status: "Error" };
  historyRef.current = [...historyRef.current, sample].slice(-MAX_HISTORY);
  setState({ current: null, error: `API returned ${response?.status ?? "unknown"}`, loading: false, history: historyRef.current });
  return;
}
const sample: UptimeSample = { time: new Date(), status: data!.status };
historyRef.current = [...historyRef.current, sample].slice(-MAX_HISTORY);
setState({ current: data as HealthResponse, error: null, loading: false, history: historyRef.current });
```

Note: `client` needs to be created inside the hook or passed the `apiUrl` param. Adjust based on how the hook receives the URL.

**Observability**: The `X-Correlation-ID` header is auto-injected by the client. No observability loss.

- [ ] **Step 2: Run platform tests**

Run: `cd apps/platform && npx vitest run`
Expected: All tests pass

- [ ] **Step 3: Commit**

```bash
git add apps/platform/features/status/useHealthCheck.ts
git commit -m "refactor: migrate health check to typed API client"
```

### Task 9: Migrate onboarding session create + verify

**Files:**
- Modify: `apps/platform/app/onboard/page.tsx:48-90`

- [ ] **Step 1: Replace raw fetch calls**

Import the shared client:

```typescript
import { api } from "@/lib/api";
```

Remove the `API_BASE` constant. Replace the two `fetch` calls:

**Verify payment** (line 54): Replace `fetch(API_BASE + "/onboard/" + sessionIdParam, ...)` with:
```typescript
const { data, error } = await api.GET("/onboard/{sessionId}", {
  params: { path: { sessionId: sessionIdParam! } },
});
if (error) throw new Error("Failed to verify payment");
setPaymentVerified(data?.state === "TrialActivated");
```

**Create session** (line 73): Replace `fetch(API_BASE + "/onboard", ...)` with:
```typescript
const { data, error } = await api.POST("/onboard", {
  body: { profileUrl },
});
if (error) throw new Error("Failed to create session");
setSessionId(data.sessionId);
setSessionToken(data.token);
```

- [ ] **Step 2: Run platform tests**

Run: `cd apps/platform && npx vitest run`
Expected: All tests pass

- [ ] **Step 3: Commit**

```bash
git add apps/platform/app/onboard/page.tsx
git commit -m "refactor: migrate onboarding session endpoints to typed API client"
```

### Task 10: Type the chat stream request (SSE stays raw fetch)

**Files:**
- Modify: `apps/platform/features/onboarding/ChatWindow.tsx`

- [ ] **Step 1: Import request/response types from generated client**

Import the generated types and use them for the chat request body and session response:

```typescript
import type { components } from "@real-estate-star/api-client";
```

Type the chat request body and session response using the generated schemas. If the generated types don't have a matching schema name, use the paths type to extract it:

```typescript
import type { paths } from "@real-estate-star/api-client";
type ChatBody = paths["/onboard/{sessionId}/chat"]["post"]["requestBody"]["content"]["application/json"];
```

Add a comment explaining why SSE stays raw fetch:

```typescript
// SSE streaming endpoint — stays raw fetch because openapi-fetch
// parses JSON responses, but this endpoint returns text/event-stream.
// Request body is typed via the API contract above.
```

Replace `API_BASE` constant with import from shared client:

```typescript
import { api } from "@/lib/api";
// Use api.baseUrl for the SSE fetch URL if exposed, otherwise keep the env var
```

**Observability**: Sentry breadcrumbs (`trackOnboarding()`) and Bearer token auth are untouched. Do NOT modify any `trackOnboarding()` calls or Sentry.addBreadcrumb calls.

- [ ] **Step 2: Run platform tests**

Run: `cd apps/platform && npx vitest run`
Expected: All tests pass

- [ ] **Step 3: Commit**

```bash
git add apps/platform/features/onboarding/ChatWindow.tsx
git commit -m "docs: annotate SSE chat endpoint as intentionally raw fetch"
```

### Task 11: Migrate signAndForward (HMAC lead + privacy endpoints)

**Files:**
- Modify: `apps/agent-site/features/shared/hmac.ts`

- [ ] **Step 1: Replace raw fetch with typed client (per-request headers)**

Import the client:

```typescript
import { createApiClient } from "@real-estate-star/api-client";
```

Replace the `fetch` call in `signAndForward`. The function currently returns `Response` — keep that contract so callers don't change. Extract `.response` from the typed client result:

```typescript
import { createApiClient } from "@real-estate-star/api-client";

// Inside signAndForward, replace the fetch block:
const client = createApiClient(apiUrl);
const endpoint = path ?? `agents/${agentId}/leads`;
const result = await client.POST(`/${endpoint}` as never, {
  body: JSON.parse(body),
  headers: {
    "Content-Type": "application/json",
    "X-API-Key": apiKey,
    "X-Signature": signature,
    "X-Timestamp": timestamp,
  },
  init: { signal: controller.signal },
});
return result.response;
```

The `as never` on the path is needed because `signAndForward` accepts a dynamic `path` string that may not match the generated path literals. This is acceptable because the function is a generic HMAC transport layer — the callers (`submit-lead.ts`, `privacy.ts`) provide the correct paths.

**Why return `result.response` (raw Response)?** The callers check `response.ok`, call `response.text()`, and `response.json()`. Keeping the `Response` return type means zero changes to `submit-lead.ts` and `privacy.ts` error handling, including the Sentry `captureException` in `submit-lead.ts` and the error text capture (`response.text().catch(() => "")`).

**Observability preserved**:
- 15-second `AbortController` timeout via `init: { signal }`
- `X-Correlation-ID` auto-injected by `createApiClient`
- Sentry error capture in callers untouched
- Error response text capture in `submit-lead.ts` untouched (still gets `Response`)

- [ ] **Step 2: Run agent-site tests**

Run: `cd apps/agent-site && npx vitest run`
Expected: All tests pass

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/features/shared/hmac.ts
git commit -m "refactor: migrate signAndForward to typed API client with per-request HMAC headers"
```

### Task 12: Migrate requestExport (divergent HMAC signing)

**Files:**
- Modify: `apps/agent-site/features/privacy/privacy.ts:46-101`

- [ ] **Step 1: Replace raw fetch with typed client — preserve divergent signing**

Import the client:

```typescript
import { createApiClient } from "@real-estate-star/api-client";
```

Replace the `fetch` call in `requestExport` (line 74). Keep the HMAC signing (lines 50-67) exactly as-is — it uses raw `hmacSecret`, NOT `hmacSecret:agentId`.

```typescript
import { createApiClient } from "@real-estate-star/api-client";

// Replace the fetch call (line 74). Keep all HMAC signing code above unchanged.
const client = createApiClient(apiUrl);
const result = await client.GET(`/agents/${agentId}/leads/export` as never, {
  params: { query: { email: encodedEmail } },
  headers: {
    "X-API-Key": apiKey,
    "X-Signature": signature,
    "X-Timestamp": timestamp,
  },
  init: { signal: controller.signal },
});
response = result.response;
```

**CRITICAL**: Do NOT change the HMAC key derivation. `requestExport` signs with `hmacSecret` alone. `signAndForward` signs with `hmacSecret:agentId`. These are intentionally different.

- [ ] **Step 2: Run agent-site tests**

Run: `cd apps/agent-site && npx vitest run`
Expected: All tests pass

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/features/privacy/privacy.ts
git commit -m "refactor: migrate requestExport to typed client — preserve divergent HMAC signing"
```

### Task 12b: Verify all signAndForward callers work end-to-end

**Files:**
- Check: `apps/agent-site/features/lead-capture/submit-lead.ts`
- Check: `apps/agent-site/features/privacy/privacy.ts`
- Check: `apps/agent-site/features/shared/__tests__/` (if tests exist)

- [ ] **Step 1: Verify submit-lead.ts error handling still works**

The `submitLead` function calls `signAndForward` and checks `response.ok`, calls `response.text()`, and reports to Sentry. Since `signAndForward` still returns `Response`, no code changes are needed — but verify the test still passes:

Run: `cd apps/agent-site && npx vitest run --filter submit-lead`
Expected: All tests pass

- [ ] **Step 2: Verify all 4 privacy functions work**

The privacy functions (`requestOptOut`, `requestSubscribe`, `requestDeletion`) call `signAndForward` and check `response.ok`. `requestExport` was migrated in Task 12. Verify:

Run: `cd apps/agent-site && npx vitest run --filter privacy`
Expected: All tests pass

- [ ] **Step 3: Audit and update test mocks if needed**

If any tests mock `global.fetch` or `vi.mock("node-fetch")`, they may not intercept `openapi-fetch` calls (which uses its own internal fetch). Check:
- `apps/agent-site/__tests__/` and `apps/agent-site/features/*/__tests__/` for fetch mocks
- Update mocks to intercept the typed client if needed (mock `@real-estate-star/api-client` instead of `fetch`)

Run: `cd apps/agent-site && npx vitest run`
Expected: ALL agent-site tests pass

- [ ] **Step 4: Commit any mock updates**

```bash
git add -A
git commit -m "test: update mocks for typed API client migration"
```

---

## Phase 4: Documentation

### Task 13: Update architecture diagrams

**Files:**
- Modify: `docs/architecture/shared-lead-form-component-hierarchy.md`
- Modify: `docs/architecture/observability-flow.md`

- [ ] **Step 1: Fix hook name in component hierarchy diagram**

Replace `useGoogleMapsAutocomplete` with `useGooglePlacesAutocomplete` in `shared-lead-form-component-hierarchy.md`.

- [ ] **Step 2: Add correlation ID detail to observability diagram**

In `observability-flow.md`, add detail about how `createApiClient` auto-injects `X-Correlation-ID` on every request.

- [ ] **Step 3: Commit**

```bash
git add docs/architecture/
git commit -m "docs: fix hook name + add correlation ID detail in architecture diagrams"
```

### Task 14: Create api-client README

**Files:**
- Create: `packages/api-client/README.md`

- [ ] **Step 1: Write the README**

Cover: overview, usage (platform vs agent-site patterns), HMAC per-request headers, correlation ID auto-injection, how to regenerate types locally, CI auto-update flow.

- [ ] **Step 2: Commit**

```bash
git add packages/api-client/README.md
git commit -m "docs: add README for @real-estate-star/api-client package"
```

### Task 15: Update CLAUDE.md and onboarding docs

**Files:**
- Modify: `.claude/CLAUDE.md`
- Modify: `docs/onboarding.md`

- [ ] **Step 1: Add api-client section to CLAUDE.md**

Add under the existing infrastructure section: how to import the client, the two usage patterns (platform shared instance vs agent-site per-request), and the correlation ID behavior.

- [ ] **Step 2: Add "Using the Typed API Client" to onboarding.md**

Add a section with: `npm run generate --workspace=packages/api-client`, import examples, and the CI auto-update flow.

- [ ] **Step 3: Commit**

```bash
git add .claude/CLAUDE.md docs/onboarding.md
git commit -m "docs: add api-client usage to CLAUDE.md and onboarding guide"
```

---

## Phase 5: Verify & Ship

### Task 16: Full verification

- [ ] **Step 1: Run all platform tests**

Run: `cd apps/platform && npx vitest run`
Expected: All pass

- [ ] **Step 2: Run all agent-site tests**

Run: `cd apps/agent-site && npx vitest run`
Expected: All pass

- [ ] **Step 3: Run all forms package tests with coverage**

Run: `cd packages/forms && npx vitest run --coverage`
Expected: All pass, 100% coverage

- [ ] **Step 4: Build both apps**

Run: `cd apps/platform && npm run build && cd ../agent-site && npm run build`
Expected: Both build successfully

- [ ] **Step 5: TypeScript check**

Run: `npx tsc --noEmit -p packages/api-client/tsconfig.json`
Expected: No errors

- [ ] **Step 6: Push and create PR**

Use the `open-pr` skill.
