# Frontend Restructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure both Next.js apps (platform + agent-site) into isolated feature folders with shared packages, maximizing parallel AI agent development.

**Architecture:** Extract cross-app code into 5 npm workspace packages (`domain`, `api-client`, `forms`, `legal`, `analytics`). Organize app-specific code into `features/` folders with barrel exports and ESLint-enforced isolation. Add observability, CSS Modules, bundle optimization, and architecture enforcement.

**Tech Stack:** Next.js 16, React 19, Tailwind v4, Vitest, npm workspaces, Cloudflare Workers (3MB limit on agent-site)

**Spec:** `docs/superpowers/specs/2026-03-22-frontend-restructure-design.md`

**Worktree:** `C:/Users/Edward.Rosado/Real-Estate-Star/.worktrees/frontend-restructure`

**Branch:** `feat/frontend-restructure`

---

## Phase 1: Shared Packages

> One PR. No app changes yet — just package extraction and import rewrites.

### Task 1.0: Update root workspace configuration

**Files:**
- Modify: `package.json` (root)

- [ ] **Step 1: Verify current workspaces config**

```bash
grep -A 5 "workspaces" package.json
```

Current pattern is likely `["apps/*", "packages/*"]`. If it uses a glob, new packages under `packages/` are auto-discovered and no change is needed. If it lists packages explicitly, add the 4 new ones.

- [ ] **Step 2: If explicit listing, add new packages**

Add `packages/domain`, `packages/forms`, `packages/legal`, `packages/analytics` to the workspaces array.

- [ ] **Step 3: Commit (if changed)**

```bash
git add package.json
git commit -m "chore: add new packages to workspace configuration"
```

---

### Task 1.1: Create `packages/domain`

**Files:**
- Create: `packages/domain/src/index.ts`
- Create: `packages/domain/src/lead-form.ts`
- Create: `packages/domain/src/cma.ts`
- Create: `packages/domain/src/correlation.ts`
- Create: `packages/domain/package.json`
- Create: `packages/domain/tsconfig.json`
- Create: `packages/domain/vitest.config.ts`
- Create: `packages/domain/__tests__/correlation.test.ts`
- Source: `packages/shared-types/lead-form.ts` (move)
- Source: `packages/shared-types/cma.ts` (move)

- [ ] **Step 1: Create package scaffold**

```bash
mkdir -p packages/domain/src packages/domain/__tests__
```

- [ ] **Step 2: Create `packages/domain/package.json`**

```json
{
  "name": "@real-estate-star/domain",
  "version": "0.1.0",
  "private": true,
  "sideEffects": false,
  "main": "src/index.ts",
  "types": "src/index.ts",
  "scripts": {
    "test": "vitest run",
    "test:coverage": "vitest run --coverage"
  }
}
```

- [ ] **Step 3: Create `packages/domain/tsconfig.json`**

Extend from root or use strict standalone config. Must match existing `shared-types` config.

- [ ] **Step 4: Move types from `shared-types`**

Copy `packages/shared-types/lead-form.ts` → `packages/domain/src/lead-form.ts`
Copy `packages/shared-types/cma.ts` → `packages/domain/src/cma.ts`
Copy `packages/shared-types/index.ts` → base for `packages/domain/src/index.ts`

- [ ] **Step 5: Add `createCorrelationId()` to domain**

Create `packages/domain/src/correlation.ts`:
```ts
export function createCorrelationId(): string {
  return crypto.randomUUID();
}
```

- [ ] **Step 6: Write test for `createCorrelationId`**

Create `packages/domain/__tests__/correlation.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import { createCorrelationId } from '../src/correlation';

describe('createCorrelationId', () => {
  it('returns a valid UUID v4 string', () => {
    const id = createCorrelationId();
    expect(id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  });

  it('returns unique values', () => {
    const ids = new Set(Array.from({ length: 100 }, () => createCorrelationId()));
    expect(ids.size).toBe(100);
  });

  it('is compatible with backend CorrelationIdMiddleware validation', () => {
    // Backend accepts: 1-64 chars, [a-zA-Z0-9_-]
    // UUID v4 is 36 chars (8-4-4-4-12 with hyphens), all hex + hyphens
    const id = createCorrelationId();
    expect(id.length).toBe(36);
    expect(id.length).toBeLessThanOrEqual(64);
    // Backend regex allows alphanumeric + hyphen + underscore
    expect(id).toMatch(/^[a-zA-Z0-9_-]+$/);
  });
});
```

- [ ] **Step 7: Create barrel export `packages/domain/src/index.ts`**

```ts
export { createCorrelationId } from './correlation';
// Re-export all types from lead-form and cma
export type { LeadFormData, LeadType, PreApprovalStatus, Timeline, BuyerDetails, SellerDetails, MarketingConsentData } from './lead-form';
export type { CmaSubmitRequest, CmaSubmitResponse, CmaStatusUpdate } from './cma';
```

Adjust exact type names based on what `shared-types/index.ts` currently exports.

- [ ] **Step 8: Create vitest config**

Create `packages/domain/vitest.config.ts` — minimal config for pure TS package (no jsdom needed).

- [ ] **Step 9: Run tests**

```bash
npm run test --prefix packages/domain
```

Expected: PASS (correlation tests)

- [ ] **Step 10: Commit**

```bash
git add packages/domain/
git commit -m "feat: create @real-estate-star/domain package with types + correlation ID"
```

---

### Task 1.2: Create `packages/forms`

**Files:**
- Create: `packages/forms/` (scaffold)
- Source: `packages/ui/LeadForm/` (move)
- Source: `packages/ui/cma/` (move)

- [ ] **Step 1: Create package scaffold with `package.json`, `tsconfig.json`, `vitest.config.ts`**

`package.json` must declare `@real-estate-star/domain` as a dependency. Add `"sideEffects": false`.

- [ ] **Step 2: Move `packages/ui/LeadForm/` → `packages/forms/src/LeadForm/`**

Include `LeadForm.tsx`, `useGoogleMapsAutocomplete.ts`, `index.ts`, and their test files.

- [ ] **Step 3: Move `packages/ui/cma/` → `packages/forms/src/cma/`**

Include `cma-api.ts`, `mapToCmaRequest.ts`, `useCmaSubmit.ts`, `index.ts`, and their test files.

**Note on test file locations:** Current `packages/ui/` colocates tests next to source files (e.g., `LeadForm/LeadForm.test.tsx`). Move tests to `packages/forms/__tests__/` OR keep them colocated — pick one pattern and be consistent. Test files to move: `LeadForm.test.tsx`, `useGoogleMapsAutocomplete.test.ts`, `useCmaSubmit.test.ts`, `mapToCmaRequest.test.ts`, `cma-api.test.ts`.

- [ ] **Step 4: Create barrel export `packages/forms/src/index.ts`**

Named re-exports only — include BOTH components and types:
```ts
export { LeadForm } from './LeadForm';
export type { LeadFormProps } from './LeadForm';
export { useGoogleMapsAutocomplete } from './LeadForm/useGoogleMapsAutocomplete';
export { useCmaSubmit, submitCma, mapToCmaRequest } from './cma';
export type { CmaSubmitPhase, CmaSubmitState, UseCmaSubmitReturn, UseCmaSubmitOptions } from './cma';
```

**IMPORTANT:** Verify exact type names against current `packages/ui/index.ts` exports. Missing type re-exports will break consumers.

- [ ] **Step 5: Update imports — replace `@real-estate-star/shared-types` with `@real-estate-star/domain`**

In all moved files that import from `shared-types`.

- [ ] **Step 6: Run tests**

```bash
npm run test --prefix packages/forms
```

Expected: All LeadForm and CMA tests pass.

- [ ] **Step 7: Commit**

```bash
git add packages/forms/
git commit -m "feat: create @real-estate-star/forms package from ui/LeadForm + ui/cma"
```

---

### Task 1.3: Create `packages/legal`

**Files:**
- Create: `packages/legal/` (scaffold)
- Source: `packages/ui/EqualHousingNotice/` (move from ui)
- Source: `apps/agent-site/components/legal/LegalPageLayout.tsx` (move)
- Source: `apps/agent-site/components/legal/MarkdownContent.tsx` (move)
- Source: `apps/agent-site/components/legal/CookieConsent.tsx` (move)
- Source: `apps/agent-site/components/legal/CookieConsentBanner.tsx` (move)
- Source: `apps/agent-site/components/legal/constants.ts` (move)
- Source: `apps/platform/components/legal/` (verify same components, delete after)

- [ ] **Step 1: Create package scaffold**

`package.json` with `@real-estate-star/domain` dependency, `"sideEffects": false`.

- [ ] **Step 2: Move `EqualHousingNotice` from `packages/ui/`**

- [ ] **Step 3: Move legal components from `apps/agent-site/components/legal/`**

Move: `LegalPageLayout.tsx`, `MarkdownContent.tsx`, `CookieConsent.tsx`, `CookieConsentBanner.tsx`, `constants.ts`

- [ ] **Step 4: Verify platform has equivalent legal components — consolidate**

Check `apps/platform/components/legal/`. **Naming mismatch alert:** The platform has `EqualHousingOpportunity.tsx` while `packages/ui/` has `EqualHousingNotice.tsx`. Compare implementations — if functionally equivalent, pick one name (prefer `EqualHousingNotice` to match the package) and update all references. Delete platform copies after consolidating into the `legal` package.

- [ ] **Step 5: Create barrel export with named re-exports**

- [ ] **Step 6: Move/create tests in `packages/legal/__tests__/`**

- [ ] **Step 7: Run tests**

```bash
npm run test --prefix packages/legal
```

- [ ] **Step 8: Commit**

```bash
git add packages/legal/
git commit -m "feat: create @real-estate-star/legal package with shared legal components"
```

---

### Task 1.4: Create `packages/analytics`

**Files:**
- Create: `packages/analytics/src/index.ts`
- Create: `packages/analytics/src/event-types.ts`
- Create: `packages/analytics/src/track-event.ts`
- Create: `packages/analytics/src/track-conversion.ts`
- Create: `packages/analytics/src/report-error.ts`
- Create: `packages/analytics/src/web-vitals.ts`
- Create: `packages/analytics/src/problem-details.ts`
- Create: `packages/analytics/__tests__/`
- Source: `apps/agent-site/lib/telemetry.ts` (extract framework-agnostic parts)

- [ ] **Step 1: Create package scaffold**

`package.json` with `@real-estate-star/domain` dependency, `"sideEffects": false`.

- [ ] **Step 2: Create `event-types.ts` with PascalCase enum matching backend `FormEvent`**

```ts
export enum EventType {
  Viewed = 'Viewed',
  Started = 'Started',
  Submitted = 'Submitted',
  Succeeded = 'Succeeded',
  Failed = 'Failed',
}
```

**IMPORTANT:** The existing `telemetry.ts` sends lowercase `"form.viewed"` which is BROKEN in production. The backend `FormEvent` enum uses PascalCase. This fixes the bug.

- [ ] **Step 3: Create `track-event.ts`**

Framework-agnostic fire-and-forget POST to `/telemetry`:
```ts
import { EventType } from './event-types';

export async function trackEvent(
  apiUrl: string,
  event: EventType,
  agentId: string,
  errorType?: string
): Promise<void> {
  try {
    await fetch(`${apiUrl}/telemetry`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ event, agentId, errorType }),
    });
  } catch {
    // Fire-and-forget — don't break the app if telemetry fails
  }
}
```

- [ ] **Step 4: Create `track-conversion.ts`**

Fires to GTM dataLayer (agent's keys, not ours):
```ts
export function trackConversion(label: string): void {
  if (typeof window !== 'undefined' && (window as any).gtag) {
    (window as any).gtag('event', 'conversion', {
      send_to: label,
    });
  }
}
```

- [ ] **Step 5: Create `report-error.ts`**

The analytics package is framework-agnostic, so `reportError` accepts an error reporter function rather than importing `@sentry/nextjs` directly:
```ts
export type ErrorReporter = (error: unknown, context?: Record<string, string>) => void;

let reporter: ErrorReporter = (error) => console.error(error);

export function setErrorReporter(fn: ErrorReporter): void {
  reporter = fn;
}

export function reportError(error: unknown, context?: Record<string, string>): void {
  reporter(error, context);
}
```

Each app initializes the reporter in its root layout:
```ts
// In app layout.tsx
import { setErrorReporter } from '@real-estate-star/analytics';
import * as Sentry from '@sentry/nextjs';
setErrorReporter((error, context) => Sentry.captureException(error, { extra: context }));
```

- [ ] **Step 6: Install `web-vitals` dependency**

```bash
npm install web-vitals --workspace=packages/analytics
```

- [ ] **Step 7: Create `web-vitals.ts`**

Uses `web-vitals` library, sends metrics via `trackEvent`. Client-side only.

- [ ] **Step 7: Create `problem-details.ts`**

Parses RFC 7807 error responses:
```ts
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
}

export async function parseProblemDetails(response: Response): Promise<ProblemDetails | null> {
  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.includes('application/problem+json')) return null;
  return response.json();
}
```

- [ ] **Step 8: Create `problem-details.ts`**

(Same as before — parses RFC 7807 error responses.)

- [ ] **Step 9: Write tests for all utilities**

Tests for `EventType` values, `trackEvent` (mock fetch), `parseProblemDetails`, `reportError` (mock reporter), `setErrorReporter`.

- [ ] **Step 10: Create barrel export with named re-exports**

- [ ] **Step 11: Run tests**

```bash
npm run test --prefix packages/analytics
```

- [ ] **Step 12: Commit**

```bash
git add packages/analytics/
git commit -m "feat: create @real-estate-star/analytics package with telemetry, error reporting, web vitals"
```

---

### Task 1.5: Update `packages/api-client`

**Files:**
- Modify: `packages/api-client/package.json` (add domain dep)
- Modify: `packages/api-client/client.ts` (add correlation ID injection)
- Modify: `packages/api-client/.gitignore` (un-gitignore generated/types.ts)
- Create: `packages/api-client/generated/types.ts` (generate from openapi.json)

- [ ] **Step 1: Add `@real-estate-star/domain` to `package.json` dependencies**

- [ ] **Step 2: Update `client.ts` to auto-inject `X-Correlation-ID`**

```ts
import { createCorrelationId } from '@real-estate-star/domain';

export function createApiClient(baseUrl: string, apiKey?: string) {
  return createClient({
    baseUrl,
    headers: {
      'X-Correlation-ID': createCorrelationId(),
      ...(apiKey ? { 'X-API-Key': apiKey } : {}),
    },
  });
}
```

- [ ] **Step 3: Un-gitignore `generated/types.ts`**

Remove the gitignore entry so the generated file is committed to repo.

- [ ] **Step 4: Add `export-openapi` script to `package.json`**

```json
"export-openapi": "curl -s http://localhost:5135/openapi/v1.json -o openapi.json"
```

- [ ] **Step 5: Run `npm run generate:ci` to produce initial types**

**Note:** If `openapi.json` does not exist in `packages/api-client/`, skip this step. The file will be produced by the API CI pipeline added in Phase 4. For now, create a placeholder `generated/types.ts` with a comment: `// Auto-generated from OpenAPI spec — do not edit manually`.

- [ ] **Step 6: Add `"sideEffects": false` to package.json**

- [ ] **Step 7: Commit**

```bash
git add packages/api-client/
git commit -m "feat: add correlation ID injection and domain dependency to api-client"
```

---

### Task 1.6: Delete old packages + rewrite imports

**Files:**
- Delete: `packages/shared-types/`
- Delete: `packages/ui/`
- Modify: All files in both apps that import from `@real-estate-star/shared-types` or `@real-estate-star/ui`

- [ ] **Step 1: Find all imports of `@real-estate-star/shared-types`**

```bash
grep -r "@real-estate-star/shared-types" apps/ packages/ --include="*.ts" --include="*.tsx" -l
```

- [ ] **Step 2: Rewrite each import to `@real-estate-star/domain`**

Mechanical find/replace. Verify type names match.

- [ ] **Step 3: Find all imports of `@real-estate-star/ui`**

```bash
grep -r "@real-estate-star/ui" apps/ packages/ --include="*.ts" --include="*.tsx" -l
```

- [ ] **Step 4: Rewrite imports**

- `LeadForm` → `@real-estate-star/forms`
- `EqualHousingNotice` → `@real-estate-star/legal`
- `useCmaSubmit`, `submitCma`, `mapToCmaRequest` → `@real-estate-star/forms`

- [ ] **Step 5: Delete `packages/shared-types/` and `packages/ui/`**

- [ ] **Step 6: Run ALL tests across both apps**

```bash
npm run test --prefix apps/platform
npm run test --prefix apps/agent-site
npm run test --prefix packages/domain
npm run test --prefix packages/forms
npm run test --prefix packages/legal
npm run test --prefix packages/analytics
```

Expected: All pass.

- [ ] **Step 7: Add `predev`/`prebuild` scripts to both apps**

Add to `apps/platform/package.json`:
```json
"predev": "npm run generate:ci --workspace=packages/api-client",
"prebuild": "npm run generate:ci --workspace=packages/api-client"
```

Add to `apps/agent-site/package.json` (prepend to existing prebuild):
```json
"predev": "npm run generate:ci --workspace=packages/api-client",
"prebuild": "npm run generate:ci --workspace=packages/api-client && node scripts/generate-config-registry.mjs"
```

- [ ] **Step 8: Commit**

```bash
git add packages/shared-types packages/ui apps/platform apps/agent-site packages/forms packages/legal packages/analytics packages/domain packages/api-client
git commit -m "refactor: replace shared-types + ui packages with domain, forms, legal, analytics"
```

---

## Phase 2: Platform Restructure

> One PR. Can run in parallel with Phase 3 (different app).

### Task 2.1: Create feature folders + move components

**Files:**
- Create: `apps/platform/features/onboarding/index.ts`
- Create: `apps/platform/features/landing/index.ts`
- Create: `apps/platform/features/status/index.ts`
- Create: `apps/platform/features/shared/index.ts`
- Create: `apps/platform/features/billing/index.ts`
- Move: `apps/platform/components/chat/*` → `features/onboarding/`
- Move: `apps/platform/components/landing/*` → `features/landing/`
- Move: `apps/platform/app/status/StatusDashboard.tsx` → `features/status/`
- Move: `apps/platform/app/status/UptimeTracker.tsx` → `features/status/`
- Move: `apps/platform/app/status/useHealthCheck.ts` → `features/status/`
- Move: `apps/platform/components/GeometricStar.tsx` → `features/shared/`

- [ ] **Step 1: Create feature directory structure**

```bash
mkdir -p apps/platform/features/{onboarding,landing,status,shared,billing}/__tests__
```

- [ ] **Step 2: Move chat components to `features/onboarding/`**

Move all files from `apps/platform/components/chat/` to `apps/platform/features/onboarding/`.
Files: ChatWindow.tsx, MessageRenderer.tsx, MessageBubble.tsx, ProfileCard.tsx, ColorPalette.tsx, GoogleAuthCard.tsx, SitePreview.tsx, FeatureChecklist.tsx, PaymentCard.tsx, CmaProgressCard.tsx

- [ ] **Step 3: Move landing components to `features/landing/`**

Move: FeatureCards.tsx, ComparisonTable.tsx, TrustStrip.tsx, FinalCta.tsx

- [ ] **Step 4: Move status components to `features/status/`**

Move: StatusDashboard.tsx, UptimeTracker.tsx, useHealthCheck.ts from `apps/platform/app/status/`

- [ ] **Step 5: Move shared components to `features/shared/`**

Move: GeometricStar.tsx

- [ ] **Step 6: Create barrel exports for each feature**

Each `index.ts` uses named re-exports:
```ts
// features/onboarding/index.ts
export { ChatWindow } from './ChatWindow';
export { MessageRenderer } from './MessageRenderer';
// ... etc
```

- [ ] **Step 7: Update `app/` route files to import from `features/`**

Update: `app/page.tsx`, `app/onboard/page.tsx`, `app/status/page.tsx`, etc.
Each page file becomes thin: import from `@/features/*`, compose, render.

- [ ] **Step 8: Move tests into feature `__tests__/` directories**

Move relevant test files from `apps/platform/__tests__/components/` into their feature's `__tests__/`.

- [ ] **Step 9: Verify `"use client"` directives preserved**

```bash
grep -r "use client" apps/platform/features/ --include="*.tsx" -l
```

Compare against the original list of client components.

- [ ] **Step 10: Run tests**

```bash
npm run test:coverage --prefix apps/platform
```

Expected: 265 tests pass, 100% coverage.

- [ ] **Step 11: Commit**

```bash
git add apps/platform/
git commit -m "refactor: restructure platform into feature folders"
```

---

### Task 2.2: Add platform observability

**Files:**
- Create: `apps/platform/sentry.client.config.ts`
- Create: `apps/platform/sentry.server.config.ts`
- Create: `apps/platform/features/shared/GA4Script.tsx`
- Modify: `apps/platform/app/layout.tsx` (add WebVitalsReporter)
- Modify: `apps/platform/app/global-error.tsx` (add Sentry)
- Modify: `apps/platform/features/onboarding/ChatWindow.tsx` (add funnel events)

- [ ] **Step 1: Add `@sentry/nextjs` to platform dependencies**

```bash
npm install @sentry/nextjs --prefix apps/platform
```

- [ ] **Step 2: Create `sentry.client.config.ts` and `sentry.server.config.ts`**

Mirror agent-site's config. DSN from `NEXT_PUBLIC_SENTRY_DSN` env var.

- [ ] **Step 3: Create platform `GA4Script.tsx` in `features/shared/`**

Uses our keys from `NEXT_PUBLIC_GA4_ID` (not agent config).

- [ ] **Step 4: Add `WebVitalsReporter` to root layout**

Import from `@real-estate-star/analytics`, add to `app/layout.tsx`.

- [ ] **Step 5: Replace `console.error` with `reportError()` in error boundaries**

Update `global-error.tsx` and any catch blocks in onboarding.

- [ ] **Step 6: Add onboarding funnel events as Sentry breadcrumbs + GA4 custom events**

In ChatWindow.tsx: `onboarding.session_created`, `onboarding.message_sent`, `onboarding.card_displayed`, etc.
Note: These go to Sentry breadcrumbs and GA4, NOT `POST /telemetry` (backend doesn't support yet).

- [ ] **Step 7: Write tests for observability additions**

- [ ] **Step 8: Run tests**

```bash
npm run test:coverage --prefix apps/platform
```

Expected: All pass, 100% coverage maintained.

- [ ] **Step 9: Commit**

```bash
git add apps/platform/
git commit -m "feat: add Sentry, GA4, Web Vitals, and onboarding telemetry to platform"
```

---

### Task 2.3: Add platform ESLint cross-feature import rules

**Files:**
- Modify: `apps/platform/eslint.config.mjs`

- [ ] **Step 1: Add `no-restricted-imports` rules per feature**

Each feature folder blocks imports from other features. `shared/` is allowed everywhere.

- [ ] **Step 2: Run lint to verify no violations**

```bash
npm run lint --prefix apps/platform
```

- [ ] **Step 3: Commit**

```bash
git add apps/platform/eslint.config.mjs
git commit -m "feat: add ESLint cross-feature import restrictions for platform"
```

---

## Phase 3: Agent-Site Restructure

> One PR. Can run in parallel with Phase 2 (different app).

### Task 3.1: Create feature folders + move config

**Files:**
- Create: `apps/agent-site/features/config/`
- Move: `apps/agent-site/lib/config.ts` → `features/config/`
- Move: `apps/agent-site/lib/config-registry.ts` → `features/config/`
- Move: `apps/agent-site/lib/nav-registry.ts` → `features/config/`
- Move: `apps/agent-site/lib/types.ts` → `features/config/`
- Move: `apps/agent-site/lib/branding.ts` → `features/config/`
- Move: `apps/agent-site/lib/routing.ts` → `features/config/`
- Move: `apps/agent-site/lib/nav-config.ts` → `features/config/`

- [ ] **Step 1: Create all feature directories**

```bash
mkdir -p apps/agent-site/features/{config,templates,sections,lead-capture,privacy,shared}/__tests__
mkdir -p apps/agent-site/features/sections/{heroes,about,services,profiles,testimonials,stats,steps,sold,marquee,shared}
```

- [ ] **Step 2: Move `lib/` config utilities into `features/config/`**

Move: config.ts, config-registry.ts, nav-registry.ts, types.ts, branding.ts, routing.ts, nav-config.ts

- [ ] **Step 3: Create `features/config/index.ts` barrel**

Named re-exports for all config utilities.

- [ ] **Step 4: Update `generate-config-registry.mjs` output paths**

Change output from `lib/config-registry.ts` → `features/config/config-registry.ts` and `lib/nav-registry.ts` → `features/config/nav-registry.ts`.

- [ ] **Step 5: Update `.gitignore` for new auto-generated file paths**

- [ ] **Step 6: Move config tests into `features/config/__tests__/`**

- [ ] **Step 7: Run prebuild + tests to verify config still works**

```bash
node apps/agent-site/scripts/generate-config-registry.mjs
npm run test --prefix apps/agent-site
```

- [ ] **Step 8: Commit**

```bash
git add apps/agent-site/
git commit -m "refactor: move agent-site config utilities into features/config/"
```

---

### Task 3.2: Move sections into feature folders

**Files:**
- Move: `apps/agent-site/components/sections/heroes/` → `features/sections/heroes/`
- Move: `apps/agent-site/components/sections/about/` → `features/sections/about/`
- Move: (all other section subsections)
- Move: `apps/agent-site/components/sections/shared/` → `features/sections/shared/`

- [ ] **Step 1: Move all section subsection directories**

Move each subsection directory from `components/sections/` to `features/sections/`:
heroes, about, services, profiles, testimonials, stats, steps, sold, marquee, shared (Footer, CmaSection, ScrollRevealSection)

- [ ] **Step 2: Create subsection barrel exports (`index.ts` per subsection)**

Each `features/sections/heroes/index.ts`, `features/sections/about/index.ts`, etc. uses named re-exports.

- [ ] **Step 3: Create top-level `features/sections/index.ts`**

Re-exports from subsection barrels. Exists for tests/tooling but templates must NOT import from it.

- [ ] **Step 4: Move section tests into `features/sections/__tests__/`**

- [ ] **Step 5: Run tests**

```bash
npm run test --prefix apps/agent-site
```

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/
git commit -m "refactor: move agent-site sections into features/sections/"
```

---

### Task 3.3: Move templates into feature folder

**Files:**
- Move: `apps/agent-site/templates/` → `features/templates/`

- [ ] **Step 1: Move all template files**

Move: index.ts, types.ts, all 10 template .tsx files

- [ ] **Step 2: Update template imports to use subsection barrels**

Change:
```tsx
import { HeroGradient, StatsBar } from "@/components/sections";
```
To:
```tsx
import { HeroGradient } from "@/features/sections/heroes";
import { StatsBar } from "@/features/sections/stats";
```

Do this for ALL 10 templates.

- [ ] **Step 3: Move template tests**

- [ ] **Step 4: Run tests**

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/
git commit -m "refactor: move templates to features/ + convert to subsection imports"
```

---

### Task 3.4: Move remaining features (lead-capture, privacy, shared)

**Files:**
- Move: `apps/agent-site/actions/submit-lead.ts` → `features/lead-capture/`
- Move: `apps/agent-site/lib/hmac.ts` → `features/lead-capture/`
- Move: `apps/agent-site/lib/turnstile.ts` → `features/lead-capture/`
- Move: `apps/agent-site/lib/safe-contact.ts` → `features/lead-capture/`
- Move: `apps/agent-site/actions/privacy.ts` → `features/privacy/`
- Move: `apps/agent-site/components/privacy/*` → `features/privacy/`
- Move: `apps/agent-site/components/Nav.tsx` → `features/shared/`
- Move: `apps/agent-site/components/Analytics.tsx` → `features/shared/`
- Move: `apps/agent-site/components/GA4Script.tsx` → `features/shared/`
- Move: `apps/agent-site/lib/security-headers.ts` → `features/shared/`
- Move: `apps/agent-site/lib/telemetry.ts` → `features/shared/`
- Move: `apps/agent-site/lib/use-focus-trap.ts` → `features/shared/`
- Move: `apps/agent-site/hooks/*` → `features/shared/`

- [ ] **Step 1: Move lead-capture files**

Move server actions and utilities into `features/lead-capture/`. Create barrel export.

- [ ] **Step 2: Move privacy files**

Move privacy server action + form components into `features/privacy/`. Create barrel export.
Note: Legal layout components come from `@real-estate-star/legal` package, NOT duplicated here.

- [ ] **Step 3: Move shared files**

Move Nav, Analytics, GA4Script, security-headers, telemetry, use-focus-trap, and all hooks (useParallax, useReducedMotion, useScrollReveal) into `features/shared/`. Create barrel export.

- [ ] **Step 4: Update `middleware.ts` imports**

```tsx
// Before
import { resolveAgent } from '@/lib/routing';
import { getSecurityHeaders } from '@/lib/security-headers';

// After
import { resolveAgent } from '@/features/config/routing';
import { getSecurityHeaders } from '@/features/shared/security-headers';
```

- [ ] **Step 5: Thin out `app/` route files**

Update all page files to import from `@/features/*`.

- [ ] **Step 6: Verify `"use server"` and `"use client"` directives**

```bash
grep -rn "use server\|use client" apps/agent-site/features/ --include="*.ts" --include="*.tsx"
```

Compare against original files to ensure no directives were lost.

- [ ] **Step 7: Delete now-empty old directories**

Remove `components/`, `lib/`, `actions/`, `hooks/`, `templates/` (should be empty after all moves).

- [ ] **Step 8: Run prebuild + full test suite**

```bash
node apps/agent-site/scripts/generate-config-registry.mjs
npm run test:coverage --prefix apps/agent-site
```

Expected: 1626 tests pass, 100% coverage.

- [ ] **Step 9: Commit**

```bash
git add apps/agent-site/
git commit -m "refactor: complete agent-site restructure into feature folders"
```

---

### Task 3.5: Agent-site observability fixes

**Files:**
- Modify: `apps/agent-site/features/shared/telemetry.ts` (fix event names)
- Modify: `apps/agent-site/app/error.tsx` (add Sentry)
- Modify: `apps/agent-site/app/global-error.tsx` (add Sentry)
- Modify: `apps/agent-site/features/privacy/*.tsx` (add form telemetry)
- Modify: `apps/agent-site/app/layout.tsx` (add WebVitalsReporter)

- [ ] **Step 1: Fix telemetry event names to PascalCase**

Update `features/shared/telemetry.ts` to use `EventType` enum from `@real-estate-star/analytics` instead of hardcoded lowercase strings. This fixes the production bug.

- [ ] **Step 2: Update error boundaries to use `reportError()`**

- [ ] **Step 3: Add telemetry to privacy forms**

Add `Viewed`, `Started`, `Submitted`, `Succeeded`, `Failed` events to DeleteRequestForm, OptOutForm, MyDataForm, SubscribeForm.

- [ ] **Step 4: Add `WebVitalsReporter` to root layout**

- [ ] **Step 5: Write/update tests**

- [ ] **Step 6: Run tests**

- [ ] **Step 7: Commit**

```bash
git add apps/agent-site/
git commit -m "fix: fix telemetry event names (PascalCase), add Sentry to error boundaries, privacy form tracking"
```

---

### Task 3.6: Bundle optimization

**Files:**
- Modify: `apps/agent-site/app/page.tsx` (dynamic template loading)
- Modify: `apps/agent-site/features/sections/shared/CmaSection.tsx` (dynamic Turnstile)
- Modify: `apps/agent-site/features/lead-capture/submit-lead.ts` (lazy Sentry)

- [ ] **Step 1: Measure baseline bundle size**

```bash
npm run build --prefix apps/agent-site
npx opennextjs-cloudflare build
wc -c apps/agent-site/.open-next/worker.js
```

Record this number.

- [ ] **Step 2: Implement dynamic template loading**

In `app/page.tsx`, replace static template imports with `next/dynamic`:
```tsx
const templateLoaders: Record<string, () => Promise<{ default: React.ComponentType<TemplateProps> }>> = {
  'emerald-classic': () => import('@/features/templates/emerald-classic'),
  // ... one per template
};
const Template = dynamic(() => templateLoaders[config.template]());
```

- [ ] **Step 3: Dynamic import Turnstile**

In CmaSection.tsx:
```tsx
const Turnstile = dynamic(
  () => import('@marsidev/react-turnstile').then(m => m.Turnstile),
  { ssr: false }
);
```

- [ ] **Step 4: Lazy-load Sentry in submit-lead.ts**

Replace static import with dynamic import in catch block.

- [ ] **Step 5: Update/write tests**

- [ ] **Step 6: Measure new bundle size**

```bash
npm run build --prefix apps/agent-site
npx opennextjs-cloudflare build
wc -c apps/agent-site/.open-next/worker.js
```

Document before/after.

- [ ] **Step 7: Run full test suite**

- [ ] **Step 8: Commit**

```bash
git add apps/agent-site/
git commit -m "perf: dynamic template loading, lazy Sentry/Turnstile — bundle size before: X, after: Y"
```

---

### Task 3.7: Add agent-site ESLint cross-feature import rules

**Files:**
- Modify: `apps/agent-site/eslint.config.mjs`

- [ ] **Step 1: Add `no-restricted-imports` per feature**

Block cross-feature imports with these declared exceptions:
- `shared/` can be imported by any feature
- `templates/` can import from `sections/` subsection barrels (heroes/, about/, etc.) but NOT top-level `sections/index.ts`
- `shared/ → config/` is allowed (shared needs config for branding, routing)
- `sections/ → config/` is allowed (sections need config for types)
- `lead-capture/ → config/` is allowed (needs agentId, API keys)

All other cross-feature imports are blocked.

- [ ] **Step 2: Run lint**

```bash
npm run lint --prefix apps/agent-site
```

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/eslint.config.mjs
git commit -m "feat: add ESLint cross-feature import restrictions for agent-site"
```

---

## Phase 3b: CSS Modules Migration

> One PR. After Phase 3. Highly parallelizable — one agent per section subsection.

### Task 3b.1: CSS Modules migration (per subsection)

**This task repeats for each section subsection:** heroes (11 files), about (10), services (12), profiles (3), testimonials, stats, steps, sold, marquee, shared (3)

**Files per component:**
- Modify: `features/sections/{subsection}/{Component}.tsx` (remove inline classes)
- Create: `features/sections/{subsection}/{Component}.module.css` (colocated styles)

- [ ] **Step 1: For each component with >3 Tailwind classes, create `.module.css`**

Each `.module.css` file starts with:
```css
@reference "tailwindcss";
```

Extract static Tailwind classes into named CSS classes using `@apply`.

- [ ] **Step 2: Update component to use `styles.className`**

```tsx
import styles from './HeroAiry.module.css';
// Replace className="..." with className={styles.name}
```

- [ ] **Step 3: Keep dynamic/conditional classes inline**

Don't extract runtime-dependent styles. Don't extract `style={}` props (branding CSS vars).

- [ ] **Step 4: Run tests for the subsection**

- [ ] **Step 5: Visually verify no changes (optional — screenshot comparison)**

- [ ] **Step 6: Commit per subsection**

```bash
git add apps/agent-site/features/sections/heroes/
git commit -m "refactor: extract heroes section inline styles to CSS Modules"
```

Repeat for each subsection. Can run up to 10 agents in parallel (one per subsection).

---

## Phase 4: Architecture Enforcement + CI/CD + API Client Pipeline

> One PR. After Phases 2+3.

### Task 4.1: Create `validate-architecture.mjs`

**Files:**
- Create: `scripts/validate-architecture.mjs`

- [ ] **Step 1: Write the validation script**

Reads all `packages/*/package.json`, extracts `@real-estate-star/*` dependencies, compares against allowed map:
```
domain     → []
api-client → [domain]
forms      → [domain]
legal      → [domain]
analytics  → [domain]
```

Exits with error if any undeclared dependency found.

- [ ] **Step 2: Run it to verify it passes**

```bash
node scripts/validate-architecture.mjs
```

- [ ] **Step 3: Commit**

```bash
git add scripts/validate-architecture.mjs
git commit -m "feat: add frontend architecture dependency validation script"
```

---

### Task 4.2: Create `architecture.test.ts`

**Files:**
- Create: `packages/domain/__tests__/architecture.test.ts`

- [ ] **Step 1: Write the test**

Same logic as the script but as a Vitest test. Runs with `npm test`.

- [ ] **Step 2: Run test**

```bash
npm run test --prefix packages/domain
```

- [ ] **Step 3: Commit**

```bash
git add packages/domain/__tests__/architecture.test.ts
git commit -m "test: add frontend architecture dependency tests"
```

---

### Task 4.3: Update CI workflows

**Files:**
- Modify: `.github/workflows/platform.yml`
- Modify: `.github/workflows/agent-site.yml`
- Modify: `.github/workflows/deploy-platform.yml`
- Modify: `.github/workflows/deploy-agent-site.yml`
- Modify: `.github/workflows/api.yml`
- Modify: `.github/workflows/deploy-api.yml`
- Create: `.github/workflows/api-client.yml`

- [ ] **Step 1: Update path filters in all 4 frontend workflows**

Replace `packages/**` with explicit package paths:
```yaml
paths:
  - 'apps/platform/**'
  - 'packages/domain/**'
  - 'packages/forms/**'
  - 'packages/legal/**'
  - 'packages/analytics/**'
  - 'packages/api-client/**'
```

- [ ] **Step 2: Add architecture validation step to `platform.yml` and `agent-site.yml`**

```yaml
- name: Validate architecture
  run: node scripts/validate-architecture.mjs
```

- [ ] **Step 3: Add bundle size check to `agent-site.yml`**

After the opennextjs-cloudflare build step:
```yaml
- name: Check Worker bundle size
  run: |
    SIZE=$(wc -c < .open-next/worker.js)
    echo "Worker bundle size: $SIZE bytes"
    if [ "$SIZE" -gt 2621440 ]; then
      echo "::warning::Worker bundle is over 2.5MB ($SIZE bytes). 3MB limit approaching."
    fi
    if [ "$SIZE" -gt 3145728 ]; then
      echo "::error::Worker bundle exceeds 3MB limit ($SIZE bytes)."
      exit 1
    fi
```

- [ ] **Step 4: Add OpenAPI spec export to `api.yml` and `deploy-api.yml`**

- [ ] **Step 5: Create `.github/workflows/api-client.yml`**

Full workflow from the spec — triggers on API CI completion, regenerates types, commits.

- [ ] **Step 6: Add `PLATFORM_SENTRY_DSN` to deploy-platform.yml env**

- [ ] **Step 7: Commit**

```bash
git add .github/workflows/
git commit -m "ci: update CI/CD for restructured frontend — path filters, architecture validation, bundle checks, api-client pipeline"
```

---

## Phase 5: Documentation & Skills

> One PR. After Phase 4. Must reflect the final state.

### Task 5.1: Update CLAUDE.md

**Files:**
- Modify: `.claude/CLAUDE.md`

- [ ] **Step 1: Update monorepo structure section**

Replace the current directory tree with the new `features/` layout for both apps and the new packages.

- [ ] **Step 2: Add Frontend Package Dependency Rules section**

Mirror the API dependency rules format. Include the allowed dependency map.

- [ ] **Step 3: Add Feature Scope Map**

The full table from the spec — shows each feature's directory, allowed imports, and blocked imports.

- [ ] **Step 4: Add Frontend Conventions section**

CSS Modules, barrel exports, dynamic imports, bundle rules, observability requirements, analytics ownership, api-client contract.

- [ ] **Step 5: Commit**

```bash
git add .claude/CLAUDE.md
git commit -m "docs: update CLAUDE.md with restructured frontend architecture"
```

---

### Task 5.2: Create/update rules and skills

**Files:**
- Create: `.claude/rules/frontend-architecture.md`
- Modify: `.claude/rules/frontend.md`
- Modify: `.claude/rules/code-quality.md`
- Create: `.claude/skills/learned/frontend-feature-isolation.md`
- Create: `.claude/skills/learned/agent-site-bundle-optimization.md`
- Create: `.claude/skills/learned/css-modules-agent-site.md`
- Modify: `.claude/skills/ci-cd-pipeline/SKILL.md` (if exists)

- [ ] **Step 1: Create `frontend-architecture.md` rules**

Package dependency map, feature folder structure, import rules, enforcement layers.

- [ ] **Step 2: Update `frontend.md` rules**

Add feature isolation, CSS Modules, barrel import restrictions, dynamic import patterns.

- [ ] **Step 3: Update `code-quality.md` rules**

Add frontend observability mandate, bundle size verification.

- [ ] **Step 4: Create learned skills**

Three new skills: feature isolation, bundle optimization, CSS Modules.

- [ ] **Step 5: Update CI/CD skill if it exists**

- [ ] **Step 6: Update `docs/onboarding.md`**

New directory structure, how to add features, api-client pipeline, architecture verification.

- [ ] **Step 7: Commit**

```bash
git add .claude/ docs/onboarding.md
git commit -m "docs: create frontend architecture rules, skills, and update onboarding"
```

---

### Task 5.3: Validate documentation with test agent

- [ ] **Step 1: Dispatch a test agent with prompt: "Add a new feature called 'notifications' to the platform app"**

The agent should:
- Create `features/notifications/` with barrel export
- Wire into `app/notifications/page.tsx`
- Not cross-import from other features
- Follow CSS Modules convention (if applicable)
- Include tests

- [ ] **Step 2: Review the agent's output**

Did it follow the patterns from documentation alone? If not, identify gaps and update docs.

- [ ] **Step 3: Dispatch a second test agent: "Add a new hero variant called HeroMinimal to the agent-site"**

Should:
- Create in `features/sections/heroes/`
- Add `.module.css` with `@reference "tailwindcss"`
- Use `@apply` for static styles
- Update `features/sections/heroes/index.ts` barrel
- NOT import from top-level `sections/index.ts`

- [ ] **Step 4: Review and fix any documentation gaps**

- [ ] **Step 5: Final commit**

```bash
git add -A
git commit -m "docs: fix documentation gaps found by test agent validation"
```
