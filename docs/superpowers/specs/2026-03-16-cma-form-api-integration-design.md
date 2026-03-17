# CMA Form .NET API Integration Design

**Date:** 2026-03-16
**Status:** Implemented
**Author:** Eddie Rosado + Claude

## Summary

Consolidate CMA form submission into `packages/ui` so it always posts to the .NET backend. Remove Formspree entirely. Export a `useCmaSubmit` hook from the shared package so any consuming app can submit CMA requests. Keep progress tracking (SignalR) in `apps/portal` only — the agent-site shows a static thank-you page after submission.

## Goals

1. **Single submission path** — every CMA form submission goes through `POST /agents/{agentId}/cma`
2. **Shared logic in `packages/ui`** — `useCmaSubmit` hook + `submitCma` API client available to all apps
3. **No Formspree** — remove all Formspree code, config fields, and dependencies
4. **Portal-only progress tracking** — SignalR/`useCmaProgress` stays in `apps/portal`, not shared
5. **Agent-site simplicity** — submit, redirect to thank-you, done (no WebSocket connections)

## Non-Goals

- Changing the .NET API endpoints (they're already production-ready)
- Moving the `ProgressTracker` UI component into `packages/ui`
- Adding new form fields or validation rules
- Changing the CMA pipeline steps

## Current State (Post-Implementation)

> **Note:** This diagram reflects the implemented architecture. The original Formspree path has been fully removed.

```
┌────────────────────────┐     ┌──────────────────────┐
│  packages/ui           │     │  apps/agent-site     │
│                        │     │                      │
│  LeadForm.tsx          │     │  CmaSection.tsx      │
│  - Validates fields    │────►│  - Wraps LeadForm    │
│  - Calls onSubmit()    │     │  - Uses useCmaSubmit │
│  - No API knowledge    │     │    from packages/ui  │
│                        │     │  - Analytics +       │
│                        │     │    Sentry on error   │
│                        │     │  - Redirects to      │
│                        │     │    /thank-you        │
└────────────────────────┘     └──────────────────────┘
```

**Resolved issues (pre-implementation):**
- ~~Formspree path is dead weight~~ — Removed entirely
- ~~API client and hook locked in `apps/agent-site`~~ — Moved to `packages/ui`
- ~~Portal can't reuse submission logic~~ — Shared hook available
- ~~Two code paths to maintain~~ — Single path through .NET API

## Proposed Architecture

```
┌──────────────────────────────────────────────────┐
│                  packages/ui                      │
│                                                   │
│  ┌──────────────┐  ┌──────────────┐              │
│  │  LeadForm    │  │ useCmaSubmit │              │
│  │  (component) │  │   (hook)     │              │
│  │              │  │              │              │
│  │  Validates   │  │  POST /cma   │              │
│  │  Collects    │  │  Returns     │              │
│  │  Submits via │  │  { jobId }   │              │
│  │  onSubmit()  │  │              │              │
│  └──────────────┘  └──────────────┘              │
│                                                   │
│  ┌──────────────┐                                │
│  │  cma-api.ts  │  Types imported from           │
│  │  (client)    │  @real-estate-star/shared-types │
│  │              │                                │
│  │  submitCma() │  No type re-declarations       │
│  └──────────────┘                                │
│                                                   │
│  No SignalR dependency                            │
└────────────┬─────────────────────┬────────────────┘
             │                     │
             ▼                     ▼
┌──────────────────┐     ┌──────────────────────────┐
│ apps/agent-site  │     │ apps/portal              │
│                  │     │                           │
│ CmaSection.tsx   │     │ CmaProgressCard.tsx       │
│  └► <LeadForm>   │     │  └► useCmaProgress()     │
│  └► onSubmitted  │     │     (SignalR, local only) │
│     → analytics  │     │                           │
│     → thank-you  │     │ useCmaProgress.ts          │
│                  │     │  └► SignalR connection    │
│ No WebSocket     │     │  └► Polling fallback      │
│ No SignalR dep   │     │                           │
└──────────────────┘     └──────────────────────────┘
             │                     │
             └──────────┬──────────┘
                        ▼
             ┌──────────────────────┐
             │  .NET API            │
             │                      │
             │  POST /agents/{id}/  │
             │       cma            │
             │                      │
             │  GET /agents/{id}/   │
             │      cma/{jobId}/    │
             │      status          │
             │                      │
             │  SignalR hub:        │
             │  /cma-progress       │
             │                      │
             │  9-step pipeline     │
             └──────────────────────┘
```

## Submission Flow (Agent Site)

```
Seller visits {handle}.real-estate-star.com
  │
  ▼
Fills out LeadForm (packages/ui)
  │ firstName, lastName, email, phone
  │ address, city, state, zip
  │ beds, baths, sqft (optional)
  │ timeline, notes (optional)
  │ TCPA consent
  │
  ▼
LeadForm calls onSubmit(LeadFormData)
  │
  ▼
CmaSection calls useCmaSubmit().submit()
  │
  ├─► POST /agents/{agentId}/cma
  │   Headers: Content-Type: application/json
  │   Body: CmaSubmitRequest (mapped from LeadFormData)
  │
  ▼
API returns { jobId, status: "processing" }
  │
  ▼
CmaSection fires trackCmaConversion(tracking)
  │
  ▼
Redirect to /thank-you?agentId={agentId}
  │
  ▼
Thank-you page shows static confirmation:
  "Your personalized CMA has been submitted!
   Check your email for your report."
  │
  ▼
(Background) .NET pipeline runs 9 steps
  │
  ▼
Lead receives CMA PDF via email
```

## Submission Flow (Portal)

```
Agent triggers demo CMA in onboarding chat
  │
  ▼
Portal uses useCmaSubmit() from packages/ui
  │
  ├─► POST /agents/{agentId}/cma
  │
  ▼
API returns { jobId }
  │
  ▼
Portal calls useCmaProgress(jobId) — LOCAL hook
  │
  ├─► Connects SignalR to /cma-progress hub
  │   JoinJob(jobId)
  │
  ├─► Receives StatusUpdate messages
  │   { status, step, totalSteps, message }
  │
  ├─► Falls back to polling GET /cma/{jobId}/status
  │   if SignalR connection fails (every 3s)
  │
  ▼
CmaProgressCard renders step-by-step progress
  │
  ▼
On complete: "Your Report Is Ready!"
```

## Component & Module Changes

### packages/shared-types — Single Source of Truth for CMA Types

All CMA API types live in `packages/shared-types/cma.ts`. Both `packages/ui` and `apps/portal` import from here — **no type re-declarations anywhere else**.

```typescript
// packages/shared-types/cma.ts (new file)

/** Mirrors .NET SubmitCmaRequest. beds/baths/sqft are integers (whole numbers only). */
export interface CmaSubmitRequest {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  address: string;
  city: string;
  state: string;
  zip: string;
  timeline: string;
  beds?: number;
  baths?: number;
  sqft?: number;
  notes?: string;
}

/** Mirrors .NET SubmitCmaResponse */
export interface CmaSubmitResponse {
  jobId: string;
  status: string;
}

/** Mirrors .NET GetStatusResponse / SignalR StatusUpdate.
 *  Used only by apps/portal for progress tracking — included in shared-types
 *  because it mirrors a .NET contract and portal imports from here. */
export interface CmaStatusUpdate {
  status: string;
  step: number;
  totalSteps: number;
  message: string;
  errorMessage?: string | null;
}
```

Re-exported from `packages/shared-types/index.ts`.

### packages/ui — New Exports

#### `cma/cma-api.ts` (moved from apps/agent-site/lib/cma-api.ts)

Submission-only API client. No SignalR. Imports types from `@real-estate-star/shared-types` — does not re-declare them.

```typescript
import type { CmaSubmitRequest, CmaSubmitResponse } from "@real-estate-star/shared-types";

export async function submitCma(
  apiBaseUrl: string,
  agentId: string,
  request: CmaSubmitRequest,
): Promise<CmaSubmitResponse> {
  const url = `${apiBaseUrl}/agents/${encodeURIComponent(agentId)}/cma`;
  const response = await fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Accept: "application/json",
    },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const text = await response.text().catch(() => "");
    if (text) console.error("[CMA-001] API error body:", text);
    throw new Error(`CMA submission failed (${response.status})`);
  }

  return (await response.json()) as CmaSubmitResponse;
}
```

**Note:** `apiBaseUrl` is an explicit parameter, not read from `process.env`. This keeps the shared library runtime-agnostic — works in Next.js, Cloudflare Workers edge runtime, or any other environment. The consuming app passes its own env var:

```typescript
// In apps/agent-site
const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5135";
const { submit } = useCmaSubmit(apiUrl);
```

#### `cma/useCmaSubmit.ts` (moved from apps/agent-site/lib/useCmaSubmit.ts)

Simplified — no SignalR tracking. Just submit and return jobId.

```typescript
import type { LeadFormData } from "@real-estate-star/shared-types";

export type CmaSubmitPhase = "idle" | "submitting" | "submitted" | "error";

export interface CmaSubmitState {
  phase: CmaSubmitPhase;
  jobId: string | null;
  errorMessage: string | null;
}

export interface UseCmaSubmitReturn {
  state: CmaSubmitState;
  /** Returns true on success, false on error */
  submit: (agentId: string, data: LeadFormData) => Promise<boolean>;
  reset: () => void;
}

export function useCmaSubmit(
  apiBaseUrl: string,
  options?: { onError?: (error: Error) => void },
): UseCmaSubmitReturn {
  // Implementation: calls submitCma() internally,
  // maps LeadFormData → CmaSubmitRequest (see mapper below)
}
```

Key differences from current version:
- **`apiBaseUrl` parameter** — not read from `process.env` (edge-runtime safe)
- **No "tracking" phase** — the hook's job ends after the API responds
- **No SignalR dependency** — `@microsoft/signalr` stays out of `packages/ui`
- **Maps LeadFormData → CmaSubmitRequest** — the hook handles the DTO mapping internally so consumers pass the same `LeadFormData` the form produces
- **Exposes `jobId`** — so consuming apps can optionally connect to progress tracking
- **Optional `onError` callback** — see Error Reporting section below

#### LeadFormData → CmaSubmitRequest Mapper

The mapper lives inside `useCmaSubmit` and handles the shape transformation:

```typescript
function mapToCmaRequest(data: LeadFormData): CmaSubmitRequest {
  return {
    firstName: data.firstName,
    lastName: data.lastName,
    email: data.email,
    phone: data.phone,
    address: data.seller?.address ?? "",
    city: data.seller?.city ?? "",
    state: data.seller?.state ?? "",
    zip: data.seller?.zip ?? "",
    timeline: data.timeline,
    // Round to integers — .NET API expects int?, not float
    beds: data.seller?.beds != null ? Math.round(data.seller.beds) : undefined,
    baths: data.seller?.baths != null ? Math.round(data.seller.baths) : undefined,
    sqft: data.seller?.sqft != null ? Math.round(data.seller.sqft) : undefined,
    notes: data.notes,
  };
}
```

**Buyer-only edge case:** When `data.seller` is undefined (buyer-only lead), address fields default to empty strings. The .NET API validates required fields and will return a 400 for seller CMA requests missing an address. **Buyer-only leads should NOT hit the CMA endpoint** — the CMA pipeline requires a property address to search comps. The consuming app (CmaSection) should only call `submit()` when the form includes seller data. The `LeadForm` component already separates buyer vs. seller modes, so the consuming app can check `data.leadTypes.includes("selling")` before submitting.

#### LeadForm.tsx — No Changes

The `LeadForm` component itself doesn't change. It already accepts an `onSubmit` callback. The consuming app wires `useCmaSubmit` to `onSubmit` — the form doesn't know about the API.

### apps/agent-site — Simplification

#### CmaForm.tsx → CmaSection.tsx (rename for clarity)

```
BEFORE                              AFTER
─────────────────────────────       ─────────────────────────────
- Formspree submit path            - DELETED
- API submit path                  - Uses useCmaSubmit from ui
- useCmaSubmit (local hook)        - DELETED (moved to packages/ui)
- cma-api.ts (local client)        - DELETED (moved to packages/ui)
- SignalR connection                - DELETED
- ProgressTracker component         - DELETED
- formHandler config check          - DELETED
```

The simplified component:
1. Renders `<LeadForm>` with agent-specific props
2. On submit: calls `useCmaSubmit().submit()` (only if seller data present)
3. On success: fires `trackCmaConversion(tracking)` for analytics, then redirects to `/thank-you`
4. On error: reports to Sentry via `onError` callback, shows error message, user can retry

#### Files to delete from apps/agent-site:
- `lib/cma-api.ts`
- `lib/useCmaSubmit.ts`
- Tests for the above

### apps/portal — Minor Rewire

- Import `useCmaSubmit` from `@real-estate-star/ui` instead of local code
- Keep `useCmaProgress` as a local hook (already exists as part of `CmaProgressCard`)
- `CmaProgressCard` continues to use SignalR for real-time updates

### config — Schema Cleanup

#### agent.schema.json

Remove:
```json
"form_handler": { "enum": ["formspree", "custom"] }
"form_handler_id": { "type": "string" }
```

#### config/agents/jenise-buckalew/config.json

Remove:
```json
"form_handler": "formspree",
"form_handler_id": "xyzabc123"
```

### Template & Type Cleanup

The `emerald-classic.tsx` template and `lib/types.ts` in agent-site pass `formHandler`/`formHandlerId` props to `CmaForm`. These must be updated:
- `emerald-classic.tsx` — remove `formHandler` and `formHandlerId` props, pass only what `CmaSection` needs
- `lib/types.ts` — remove `form_handler` and `form_handler_id` from `AgentIntegrations` type
- Test fixtures — remove Formspree-related test data

**Full grep required at implementation time:** Search for `form_handler`, `formHandler`, `formHandlerId`, and `formspree` across the entire codebase. Known files beyond the main code paths:
- `apps/agent-site/templates/emerald-classic.tsx`
- `apps/agent-site/lib/types.ts`
- `apps/agent-site/__tests__/components/CmaForm.test.tsx`
- `apps/agent-site/__tests__/components/fixtures.ts`
- `docs/architecture/cma-pipeline.md`
- Any other docs referencing Formspree

## Type Flow Diagram

```
LeadFormData (shared-types)           CmaSubmitRequest (shared-types)
┌──────────────────────┐              ┌──────────────────────┐
│ leadTypes: LeadType[]│              │ firstName: string    │
│ firstName: string    │              │ lastName: string     │
│ lastName: string     │    map()     │ email: string        │
│ email: string        │ ──────────► │ phone: string        │
│ phone: string        │  Math.round  │ address: string      │
│ buyer?: BuyerDetails │  on beds/    │ city: string         │
│ seller?: SellerDetails  baths/sqft  │ state: string        │
│ timeline: Timeline   │              │ zip: string          │
│ notes?: string       │              │ timeline: string     │
└──────────────────────┘              │ beds?: number (int)  │
                                      │ baths?: number (int) │
   Produced by LeadForm               │ sqft?: number (int)  │
   component                          │ notes?: string       │
                                      └──────────────────────┘

                                         Consumed by .NET API
                                         POST /agents/{id}/cma

Note: beds, baths, sqft are integers on the .NET side (int?).
The mapper rounds these values defensively before sending.
Buyer-only leads (seller is undefined) should NOT submit to
the CMA endpoint — the pipeline requires a property address.
```

The mapping lives inside `useCmaSubmit` — it extracts `seller.*` fields into the flat `CmaSubmitRequest` shape the API expects.

## Dependency Changes

### packages/ui/package.json

**No new dependencies.** The submission uses `fetch` (built-in). No SignalR. Types come from `@real-estate-star/shared-types` (already a peer dep).

### apps/agent-site/package.json

**Remove:**
- `@microsoft/signalr` — no longer used in agent-site

### apps/portal/package.json

**Keep:**
- `@microsoft/signalr` — still used for `useCmaProgress` in portal

## Error Reporting

The current `CmaForm.tsx` calls `Sentry.captureException()` on submission failure. Since `packages/ui` should not depend on Sentry directly, error reporting is handled via a callback pattern:

```typescript
// useCmaSubmit accepts an optional onError callback
export function useCmaSubmit(
  apiBaseUrl: string,
  options?: { onError?: (error: Error) => void },
): UseCmaSubmitReturn;

// Consuming app provides Sentry reporting
const { submit } = useCmaSubmit(apiUrl, {
  onError: (err) => {
    Sentry.captureException(err, {
      tags: { agentId, feature: "cma-form" },
    });
  },
});
```

The hook calls `onError` when submission fails, then also sets `phase: "error"` with `errorMessage` for the UI. This way:
- `packages/ui` stays Sentry-free
- Agent-site keeps Sentry reporting (no regression)
- Portal can provide its own error handler
- The hook's error state still works for consumers that don't need Sentry

## CORS Note

The agent-site runs on `{handle}.real-estate-star.com` and the API runs on `api.real-estate-star.com`. The `fetch` call in `cma-api.ts` is a cross-origin request. The .NET API already has CORS configured for `*.real-estate-star.com` origins. No CORS changes needed for this migration — the same origins are making the same requests, just from shared code instead of app-local code.

## Error Handling

| Scenario | Behavior |
|----------|----------|
| API unreachable | `useCmaSubmit` returns `phase: "error"`, calls `onError`, form stays interactive, user can retry |
| API returns 400 (validation) | Error message displayed, form stays interactive |
| API returns 500 | Generic "Something went wrong" message, user can retry |
| Network timeout | Same as unreachable |
| Buyer-only submission | Consuming app should NOT call submit — CMA requires property address |
| Successful submit | Agent-site: fire analytics, redirect to thank-you. Portal: start progress tracking |

## Testing Strategy

### packages/ui tests

- `useCmaSubmit.test.ts` — hook behavior: idle → submitting → submitted, error cases, reset, `onError` callback invocation
- `cma-api.test.ts` — API client: success response, HTTP errors, network failures, `apiBaseUrl` parameter used correctly
- `mapToCmaRequest.test.ts` — mapper: seller data extraction, buyer-only defaults to empty strings, `Math.round` on beds/baths/sqft
- Existing `LeadForm.test.tsx` — unchanged (form doesn't know about API)

### apps/agent-site tests

- `CmaSection.test.tsx` — renders LeadForm, calls submit, fires analytics on success, redirects on success, shows error on failure, Sentry called on error
- Delete: Formspree-related tests, ProgressTracker tests, SignalR tests

### apps/portal tests

- Existing `CmaProgressCard.test.tsx` — unchanged
- Verify it imports `useCmaSubmit` from `@real-estate-star/ui`

## Migration Checklist

1. Add `CmaSubmitRequest`, `CmaSubmitResponse`, `CmaStatusUpdate` types to `packages/shared-types/cma.ts`
2. Re-export from `packages/shared-types/index.ts`
3. Create `packages/ui/cma/cma-api.ts` — submission client, imports types from shared-types, takes `apiBaseUrl` param
4. Create `packages/ui/cma/useCmaSubmit.ts` — hook with `apiBaseUrl` param, `onError` callback, LeadFormData mapper
5. Export new modules from `packages/ui/index.ts`
6. Rewrite agent-site `CmaForm.tsx` → `CmaSection.tsx` — remove Formspree, use shared hook, keep analytics + Sentry
7. Update `emerald-classic.tsx` template — remove `formHandler`/`formHandlerId` props
8. Update `lib/types.ts` — remove `form_handler`/`form_handler_id` from types
9. Delete agent-site `lib/cma-api.ts` and `lib/useCmaSubmit.ts` and their tests
10. Update portal imports to use `useCmaSubmit` from `@real-estate-star/ui`
11. Remove `form_handler` and `form_handler_id` from `config/agent.schema.json`
12. Update `config/agents/jenise-buckalew/config.json` — remove Formspree fields
13. Remove `@microsoft/signalr` from agent-site `package.json`
14. Grep codebase for `form_handler`, `formHandler`, `formHandlerId`, `formspree` — update/remove all remaining references
15. Update `docs/architecture/cma-pipeline.md` — remove Formspree flow from diagrams
16. Run full test suite across all packages
