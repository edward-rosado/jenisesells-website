# Frontend Restructure Design

**Date:** 2026-03-22
**Status:** Approved
**Goal:** Restructure both Next.js apps (platform + agent-site) to maximize parallel agent development, mirroring the .NET API's multi-project isolation pattern.

## Approach

**Hybrid: Shared Packages + App Feature Folders + Architecture Enforcement**

- Extract cross-app concerns into focused npm workspace packages
- Organize app-specific code into isolated feature folders with barrel exports
- Enforce boundaries at both levels (ESLint + CI + tests)

## Current State

### Platform (~40 source files)
- Flat `components/` directory (chat/, landing/, legal/)
- No feature isolation — all components can import each other
- Tests in separate `__tests__/` tree

### Agent-Site (~160 source files, ~93 section components)
- Flat `components/sections/` with subsections (heroes/, about/, services/)
- Utilities in `lib/`, server actions in `actions/`
- 10 templates in `templates/`
- Config registry prebuild system

### Shared Packages (3)
- `shared-types` — lead-form.ts, cma.ts
- `ui` — LeadForm, EqualHousingNotice, CMA hooks (grab bag)
- `api-client` — OpenAPI client (exists but unused by apps)

## Target State

### Shared Packages (5)

| Package | Purpose | Depends On |
|---------|---------|------------|
| `@real-estate-star/domain` | Types, interfaces, enums, constants | Nothing |
| `@real-estate-star/api-client` | Generated OpenAPI client + typed fetch wrappers + auto-injected correlation IDs | `domain` |
| `@real-estate-star/forms` | LeadForm, validation, useGoogleMapsAutocomplete, CMA hooks | `domain` |
| `@real-estate-star/legal` | EqualHousingNotice, CookieConsentBanner, CookieConsent, LegalPageLayout, MarkdownContent, legal constants (LEGAL_EFFECTIVE_DATE, STATE_NAMES) | `domain` |
| `@real-estate-star/analytics` | Telemetry helpers, event types, conversion tracking utilities | `domain` |

**Analytics package note:** GA4 script injection and Sentry init use `next/script` and must remain in-app (Next.js-specific). The `analytics` package provides framework-agnostic utilities: event type definitions, telemetry helper functions, and conversion tracking logic. Each app wraps these with its own Next.js-specific components (e.g., `GA4Script.tsx` stays in `features/shared/`).

**Dependency rule:** Every package has at most 1 internal dependency (`domain`). No package-to-package imports except through `domain`.

**Absorbed packages:**
- `shared-types` → absorbed into `domain`, deleted
- `ui` → split into `forms` + `legal`, deleted

### API Client Contract Pipeline

The `@real-estate-star/api-client` package uses a checked-in OpenAPI spec as the single source of truth for backend/frontend contracts. A GitHub Actions workflow keeps the generated types in sync automatically.

**Flow:**

```
API code changes → API CI builds → exports openapi.json artifact
    → api-client workflow triggers → regenerates generated/types.ts
    → commits updated types to repo → frontend CI picks up changes
    → frontend build fails if contract mismatch (type errors)
```

**Workflow: `.github/workflows/api-client.yml`**

```yaml
name: api-client
on:
  workflow_run:
    workflows: [api]         # Triggers after API CI completes
    types: [completed]
    branches: [main]
  push:
    paths:
      - 'packages/api-client/**'

jobs:
  generate:
    if: github.event.workflow_run.conclusion == 'success' || github.event_name == 'push'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: 22 }
      - run: npm ci --workspace=packages/api-client
      - name: Download OpenAPI spec from API build
        if: github.event_name == 'workflow_run'
        uses: actions/download-artifact@v4
        with:
          name: openapi-spec
          path: packages/api-client/
      - run: npm run generate:ci --workspace=packages/api-client
      - name: Commit generated types
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add packages/api-client/generated/
          git diff --cached --quiet || git commit -m "chore: regenerate api-client types from OpenAPI spec"
          git push
```

**API CI addition:** The existing API workflow must export the OpenAPI spec as an artifact after build:

```yaml
# Added to .github/workflows/api.yml, after build step
- name: Export OpenAPI spec
  run: dotnet run --project apps/api/RealEstateStar.Api -- --export-openapi
- uses: actions/upload-artifact@v4
  with:
    name: openapi-spec
    path: apps/api/openapi.json
```

**Contract safety:** If the API adds/removes/changes an endpoint, the regenerated types will cause TypeScript compilation errors in any frontend code that uses the old contract. This catches contract mismatches at build time, not runtime.

**`generated/types.ts` is committed to the repo** — not gitignored. This means:
- Apps consume it via npm workspaces (no registry needed)
- Types are always visible in PRs (reviewable diffs)
- No build step required to use the api-client package locally
- The file is auto-generated — never hand-edit it

**Local dev flow:**

When developing locally, contract changes surface automatically through the frontend `prebuild` step:

```
You change an API endpoint
  → Run: npm run export-openapi        (curls spec from running API)
  → Run: npm run dev (platform or agent-site)
  → prebuild regenerates types from openapi.json
  → TypeScript red-squiggles any broken contracts immediately
```

**Scripts added to `packages/api-client/package.json`:**

```json
{
  "scripts": {
    "generate": "openapi-typescript openapi.json -o generated/types.ts",
    "generate:ci": "openapi-typescript openapi.json -o generated/types.ts",
    "export-openapi": "curl -s http://localhost:5135/openapi/v1.json -o openapi.json"
  }
}
```

**Frontend app `prebuild` addition (both apps):**

```json
{
  "scripts": {
    "prebuild": "npm run generate:ci --workspace=packages/api-client && <existing prebuild steps>"
  }
}
```

The `prebuild` script runs automatically before `npm run build` (npm lifecycle). For dev, a `predev` script is also needed:

```json
{
  "scripts": {
    "predev": "npm run generate:ci --workspace=packages/api-client",
    "prebuild": "npm run generate:ci --workspace=packages/api-client && <existing prebuild steps>"
  }
}
```

This means:
- `npm run dev` → predev regenerates types → dev server starts with current contracts
- `npm run build` → prebuild regenerates types → build fails if contracts don't match

**Typical local workflow:**
1. Change API endpoint (e.g., add a field to a response DTO)
2. Start/restart API (`dotnet run`)
3. Run `npm run export-openapi -w packages/api-client` (one command, pulls fresh spec)
4. Start frontend (`npm run dev -w apps/platform`) — prebuild auto-regenerates types
5. TypeScript shows errors in any component using the changed contract

### Package Internal Structure

Each package follows the same layout:

```
packages/{name}/
  src/
    index.ts          # Barrel export (public API)
    *.ts / *.tsx       # Source files
  __tests__/
    *.test.ts(x)      # Tests
  package.json        # Declares deps explicitly
  tsconfig.json       # Strict, extends shared base
  vitest.config.ts    # Own test config
```

### Observability Improvements

The restructure is the right time to fix observability gaps. Current state: platform is a complete black box, agent-site has partial coverage (Sentry with only 1 explicit call, fire-and-forget telemetry, no Web Vitals).

#### Analytics Ownership Model

**Platform and agent-site have different analytics ownership:**

| Concern | Platform | Agent-Site |
|---------|----------|------------|
| **Analytics owner** | Real Estate Star (us) | The agent (BYOK — bring your own keys) |
| **GA4/GTM keys** | Our keys, in `.env.production` | Agent's keys, from `account.json` config |
| **Sentry DSN** | Our DSN | Our DSN (we own error tracking for the platform we host) |
| **Telemetry endpoint** | `POST /telemetry` (our API) | `POST /telemetry` (our API, tagged with agentId) |
| **Web Vitals** | Sent to our telemetry endpoint | Sent to our telemetry endpoint (we own performance) |
| **Conversion tracking** | Our Stripe/payment events | Agent's GA4/GTM conversion labels (from config) |

**Why this split:** Agent sites are white-label — agents bring their own Google Analytics, GTM, Meta Pixel, and Google Ads keys so they can track their own marketing metrics. We still own error tracking (Sentry) and infrastructure telemetry (Web Vitals, form funnel events) because we're responsible for the platform working correctly.

**Implementation:**
- Agent-site `GA4Script.tsx` and `Analytics.tsx` read keys from agent config (`account.json` → `integrations.googleAnalytics`, `integrations.gtm`, etc.) — this already works today
- Platform gets its own `GA4Script.tsx` with our keys from `NEXT_PUBLIC_GA4_ID` env var
- Both apps send telemetry to `POST /telemetry` (our endpoint) — this is infrastructure telemetry, not marketing analytics
- `trackConversion()` in agent-site fires to the agent's GTM/GA4 — not ours

#### Backend Alignment

The frontend observability must wire up correctly with the backend's existing stack:

**Backend stack (already in place):**
- OpenTelemetry → OTLP gRPC (port 4317) → Grafana Cloud (prod) / Prometheus+Grafana (dev)
- Serilog structured logging with `CorrelationId` and `AgentId` enrichment
- Custom ActivitySources: Onboarding, Leads, CMA, HomeSearch
- Custom Meters: business event counters + duration histograms
- Health checks: `/health/live`, `/health/ready`, `/health/workers`
- Error codes: `[PREFIX-NNN]` format (e.g., `[LEAD-002]`, `[HMAC-001]`)

**Frontend→Backend integration points:**

| Frontend Action | Backend Integration | Header/Endpoint |
|----------------|---------------------|-----------------|
| Every API call | Correlation ID propagation | `X-Correlation-ID` header (1-64 chars, alphanumeric + `-` `_`) |
| Form funnel events | Telemetry endpoint | `POST /telemetry` (body: `{ event, agentId, errorType? }`) |
| Error display | RFC 7807 ProblemDetails | Parse `application/problem+json` responses |
| Health dashboard | Health check endpoints | `GET /health/ready` (returns per-service status + duration) |

**Correlation ID contract:**
- Frontend generates a UUID via `createCorrelationId()` and sends it as `X-Correlation-ID`
- Backend's `CorrelationIdMiddleware` accepts it (validated: 1-64 chars, `[a-zA-Z0-9_-]`)
- Backend pushes it to Serilog `LogContext` — all logs in that request scope include it
- Backend echoes it in the response `X-Correlation-ID` header
- Frontend stores the correlation ID with Sentry error reports for cross-referencing
- **Result:** A frontend error's correlation ID can be searched in Grafana to find the exact backend logs/spans

**Telemetry event alignment:**
The backend `POST /telemetry` endpoint accepts a `FormEvent` enum with **PascalCase** values: `Viewed`, `Started`, `Submitted`, `Succeeded`, `Failed`. These are deserialized via `JsonStringEnumConverter`. The backend then maps them to meter counter names (`form.viewed`, etc.) internally — but the API contract is PascalCase.

**Bug fix:** The existing frontend `telemetry.ts` sends lowercase dot-notation (`"form.viewed"`) which fails `JsonStringEnumConverter` deserialization — the error is silently swallowed by the fire-and-forget `catch(() => {})`. This means **form funnel telemetry is broken in production today**. The `EventType` enum in the analytics package must use PascalCase values to match the backend contract. This fix is part of Phase 1 (analytics package extraction).

#### What moves into `@real-estate-star/analytics` package

| Utility | Purpose | Used By |
|---------|---------|---------|
| `EventType` enum | PascalCase values matching backend `FormEvent` enum: `Viewed`, `Started`, `Submitted`, `Succeeded`, `Failed` | Both apps |
| `trackEvent(type, data)` | Fire-and-forget POST to `/telemetry` with correct PascalCase enum values | Both apps |
| `trackConversion(label)` | Fires to agent's GTM dataLayer (not our endpoint) | Agent-site |
| `createCorrelationId()` | **Lives in `domain` package** (not analytics). Generates hyphenated UUID v4 via `crypto.randomUUID()` (36 chars, passes backend validation: alphanumeric + `-` `_`, max 64 chars). In `domain` because `api-client` needs it and `api-client` should only depend on `domain`. | Both apps (via domain) |
| `reportError(error, context)` | Wraps `Sentry.captureException` with correlation ID + structured context | Both apps |
| `WebVitalsReporter` | Web Vitals collection (LCP, CLS, INP, FCP, TTFB) → sends via `trackEvent` | Both apps |
| `parseProblemDetails(response)` | Parses RFC 7807 error responses from API | Both apps |

#### What stays in-app (Next.js-specific)

| Component | Location | Purpose |
|-----------|----------|---------|
| `GA4Script.tsx` | agent-site `features/shared/` | Consent-gated GA4 with **agent's keys** from config |
| `GA4Script.tsx` | platform `features/shared/` | GA4 with **our keys** from env var |
| `Analytics.tsx` | agent-site `features/shared/` | GTM/Meta/Ads injection with **agent's keys** from config |
| Sentry configs | both apps (root level) | `sentry.client.config.ts` + `sentry.server.config.ts` with **our DSN** |

#### New observability added during restructure

1. **Platform gets Sentry** — currently has zero error tracking. Add `sentry.client.config.ts` and `sentry.server.config.ts` mirroring agent-site setup. All `console.error` in onboarding/payment flows replaced with `reportError()`.

2. **Platform gets GA4** — our own GA4 property to track platform usage (onboarding starts, completions, page views). Keys from `NEXT_PUBLIC_GA4_ID` env var, not from agent config.

3. **Web Vitals reporting** — `WebVitalsReporter` in the analytics package uses the `web-vitals` library to collect Core Web Vitals and sends them via `trackEvent` to our `/telemetry` endpoint. Both apps include it in their root layout. No-op on server, runs client-side only.

4. **Correlation IDs** — Every API call from either app includes an `X-Correlation-ID` header. Generated by `createCorrelationId()` in the analytics package, auto-injected by `createApiClient()` in the api-client package. Stored with Sentry error reports. Ties frontend errors to backend logs/spans in Grafana.

5. **Onboarding funnel tracking (platform)** — The onboarding chat flow becomes observable:
   - `onboarding.session_created` — session started
   - `onboarding.message_sent` — user message
   - `onboarding.card_displayed` — card type rendered
   - `onboarding.card_action` — user interacted with card
   - `onboarding.payment_initiated` — Stripe checkout opened
   - `onboarding.completed` — flow finished

   **Note:** These events are NOT sent to the existing `POST /telemetry` endpoint (which only accepts `FormEvent` values). They require a backend extension — either expanding the `/telemetry` endpoint to accept a broader event enum, or adding a new `POST /telemetry/onboarding` endpoint. This backend work is out of scope for this restructure spec but must be done before the platform onboarding telemetry is wired up. Until then, these events can be sent to Sentry as breadcrumbs (zero backend changes needed) and to our GA4 as custom events.

6. **Consistent error boundaries** — Both apps' `error.tsx` and `global-error.tsx` call `reportError()` instead of `console.error`. The `reportError()` helper wraps Sentry with structured context (correlation ID, agentId, component name).

7. **Privacy form tracking (agent-site)** — Delete/opt-out/subscribe forms get the same telemetry as the CMA form (viewed, started, submitted, succeeded, failed) via the same `POST /telemetry` endpoint.

8. **RFC 7807 error handling** — Both apps use `parseProblemDetails()` to extract structured error details from API responses, instead of displaying raw error text. Error details are logged to Sentry with correlation ID.

**Observability rule:** Every user-facing action (form submit, payment, OAuth, chat message) must have a corresponding telemetry event. This is enforced by code review, not lint rules — it's a convention, not a constraint.

### CSS Modules Migration (Agent-Site Only)

The agent-site has 93+ section components with Tailwind utility classes inline in JSX. This clutters the markup, makes components harder to read, and creates merge noise when agents work in parallel.

**Change:** Extract Tailwind utility classes into colocated CSS Modules (`.module.css` files). No visual changes — same classes, just moved.

**Before (inline):**
```tsx
// HeroAiry.tsx
export function HeroAiry({ title, subtitle }: HeroProps) {
  return (
    <section className="relative min-h-[80vh] flex items-center justify-center bg-gradient-to-b from-gray-50 to-white px-4 py-20 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-4xl text-center">
        <h1 className="text-4xl font-light tracking-tight text-gray-900 sm:text-5xl md:text-6xl">
          {title}
        </h1>
        <p className="mt-6 text-lg leading-8 text-gray-600 sm:text-xl">
          {subtitle}
        </p>
      </div>
    </section>
  );
}
```

**After (CSS Modules + `@apply`):**
```tsx
// HeroAiry.tsx
import styles from './HeroAiry.module.css';

export function HeroAiry({ title, subtitle }: HeroProps) {
  return (
    <section className={styles.section}>
      <div className={styles.container}>
        <h1 className={styles.title}>{title}</h1>
        <p className={styles.subtitle}>{subtitle}</p>
      </div>
    </section>
  );
}
```

```css
/* HeroAiry.module.css */
@reference "tailwindcss";

.section {
  @apply relative min-h-[80vh] flex items-center justify-center
         bg-gradient-to-b from-gray-50 to-white px-4 py-20 sm:px-6 lg:px-8;
}

.container {
  @apply mx-auto max-w-4xl text-center;
}

.title {
  @apply text-4xl font-light tracking-tight text-gray-900
         sm:text-5xl md:text-6xl;
}

.subtitle {
  @apply mt-6 text-lg leading-8 text-gray-600 sm:text-xl;
}
```

**Rules:**
1. Every component with more than 3 Tailwind classes gets a `.module.css` file.
2. CSS Module files live next to their component (e.g., `heroes/HeroAiry.module.css`).
3. Every `.module.css` file must include `@reference "tailwindcss";` at the top — required by Tailwind v4 for `@apply` to resolve utility classes in CSS Modules.
4. Use `@apply` to keep Tailwind utilities — no raw CSS values. This preserves the design system.
5. Dynamic/conditional classes (e.g., `className={isActive ? 'bg-blue' : 'bg-gray'}`) stay inline — CSS Modules are for static styles.
6. Config-driven branding CSS variables (`var(--color-primary)`) remain as inline `style={}` props — they're runtime values, not static styles.

**Scope:** Agent-site section components only (~93 files). Platform is too small and hasn't been designed yet — CSS Modules can be adopted there when it grows.

**Parallel agent benefit:** When an agent works on `HeroAiry`, it touches `HeroAiry.tsx` + `HeroAiry.module.css` — both in the same `features/sections/heroes/` directory. No conflicts with agents working on other sections.

**File structure with CSS Modules:**
```
features/sections/heroes/
  index.ts
  HeroAiry.tsx
  HeroAiry.module.css
  HeroBold.tsx
  HeroBold.module.css
  ...
  hero-utils.tsx
```

### Bundle Optimization (Agent-Site)

The agent-site has a hard 3MB Cloudflare Worker limit. Current architecture bundles all 10 templates and all 88 section variants eagerly, even though a given agent site only uses 1 template with ~10-15 sections. Estimated dead code: 500KB-1MB.

#### 1. Dynamic template loading (biggest win: ~500KB-1MB)

The agent-site `page.tsx` already knows which template to render from config. Instead of importing all 10 templates statically, dynamically load only the one needed:

```tsx
// Before: all 10 templates in the bundle
import { templates } from '@/features/templates';
const Template = templates[config.template];

// After: only the needed template is loaded
import dynamic from 'next/dynamic';

const templateLoaders: Record<string, () => Promise<{ default: React.ComponentType<TemplateProps> }>> = {
  'emerald-classic': () => import('@/features/templates/emerald-classic'),
  'modern-minimal': () => import('@/features/templates/modern-minimal'),
  // ... one entry per template
};

const Template = dynamic(() => templateLoaders[config.template]());
```

Each template becomes its own chunk. Only the selected template's code is sent to the browser.

#### 2. Direct imports in templates (savings: ~200-300KB)

Templates must import sections directly from subsection modules, not from the top-level `sections/index.ts` barrel:

```tsx
// Bad — barrel import pulls in all 88 section variants
import { HeroGradient, StatsBar, ServicesGrid } from '@/features/sections';

// Good — direct imports, only these 3 are bundled
import { HeroGradient } from '@/features/sections/heroes';
import { StatsBar } from '@/features/sections/stats';
import { ServicesGrid } from '@/features/sections/services';
```

The top-level `sections/index.ts` barrel still exists for convenience in tests and tooling, but **templates must never import from it**. This is enforced by ESLint:

```js
// eslint.config.mjs
{
  files: ["features/templates/**"],
  rules: {
    "no-restricted-imports": ["error", {
      patterns: [{
        group: ["../sections/index", "../sections"],
        message: "Templates must import from subsection barrels (e.g., ../sections/heroes), not the top-level barrel."
      }]
    }]
  }
}
```

#### 3. Lazy-load Sentry (savings: ~100-150KB)

Sentry is currently imported unconditionally but only used in 1 location (`submit-lead.ts`). Lazy-load it on error:

```tsx
// Before: Sentry bundled even if no errors occur
import * as Sentry from '@sentry/nextjs';
Sentry.captureException(error);

// After: only loaded when an error actually happens
try {
  await submitLead(data);
} catch (error) {
  const Sentry = await import('@sentry/nextjs');
  Sentry.captureException(error);
}
```

Note: `sentry.client.config.ts` and `sentry.server.config.ts` at the app root still initialize Sentry for automatic error capture (uncaught exceptions, unhandled rejections). The lazy import is only for explicit `captureException` calls in application code.

#### 4. Dynamic import Turnstile (savings: ~40-50KB)

Turnstile is only used when `NEXT_PUBLIC_TURNSTILE_SITE_KEY` is set. Dynamic import defers loading:

```tsx
// Before: always bundled
import { Turnstile } from '@marsidev/react-turnstile';

// After: only loaded when CAPTCHA is enabled
const Turnstile = dynamic(
  () => import('@marsidev/react-turnstile').then(m => m.Turnstile),
  { ssr: false }
);
```

#### 5. Package-level tree-shaking (enables all of the above)

Add `"sideEffects": false` to all workspace packages' `package.json`:

```json
// packages/domain/package.json, packages/forms/package.json, etc.
{
  "sideEffects": false
}
```

This tells the bundler it's safe to eliminate unused exports from these packages.

#### Total estimated savings: ~1MB-1.5MB

| Optimization | Savings | Phase |
|-------------|---------|-------|
| Dynamic template loading | 500KB-1MB | Phase 3 |
| Direct subsection imports | 200-300KB | Phase 3 |
| Lazy Sentry | 100-150KB | Phase 3 |
| Dynamic Turnstile | 40-50KB | Phase 3 |
| `sideEffects: false` | Enables tree-shaking | Phase 1 |

All optimizations are part of the restructure — no separate phase needed. Phase 1 adds `sideEffects: false` to new packages. Phase 3 implements the dynamic imports and direct subsection imports as files are moved.

#### Verification

After Phase 3, measure the Worker bundle size:

```bash
npm run build -w apps/agent-site && wc -c .open-next/worker.js
```

Document the before/after size in the PR. If the Worker exceeds 2.5MB (leaving headroom below the 3MB limit), investigate further splits.

### Platform App Feature Structure

```
apps/platform/
  app/                          # Next.js App Router (thin routing layer)
    layout.tsx                  # Root layout — imports from features
    page.tsx                    # Landing page — renders landing feature
    globals.css
    sitemap.ts
    global-error.tsx
    onboard/
      layout.tsx
      page.tsx                  # Wires onboarding feature
    billing/
      page.tsx                  # Wires billing feature (NEW)
    status/
      page.tsx                  # Wires status feature
    privacy/page.tsx
    terms/page.tsx
    dmca/page.tsx
    accessibility/page.tsx

  features/
    onboarding/                 # Chat-based agent setup
      index.ts                  # Barrel export
      ChatWindow.tsx
      MessageRenderer.tsx
      MessageBubble.tsx
      ProfileCard.tsx
      ColorPalette.tsx
      GoogleAuthCard.tsx
      SitePreview.tsx
      FeatureChecklist.tsx
      PaymentCard.tsx
      CmaProgressCard.tsx
      __tests__/
        *.test.tsx

    billing/                    # Stripe integration (NEW)
      index.ts
      # Components, hooks, server actions TBD
      __tests__/

    landing/                    # Marketing pages
      index.ts
      FeatureCards.tsx
      ComparisonTable.tsx
      TrustStrip.tsx
      FinalCta.tsx
      __tests__/
        *.test.tsx

    status/                     # Health dashboard
      index.ts
      StatusDashboard.tsx
      UptimeTracker.tsx
      useHealthCheck.ts
      __tests__/
        *.test.tsx

    shared/                     # App-level shared (header, footer, logo)
      index.ts
      GeometricStar.tsx
      __tests__/
```

**Rules:**
1. `app/` pages are thin — import from `features/` and compose. No business logic in route files.
2. Features cannot cross-import — `onboarding/` cannot import from `billing/`, etc.
3. `features/shared/` is the exception — any feature can import from `shared/`, but `shared/` cannot import from any feature. (Same role as `Workers.Shared` in the backend.)
4. Package imports are unrestricted — any feature can use `@real-estate-star/domain`, `forms`, `legal`, etc.
5. Tests live inside the feature — each feature is self-contained for parallel agent work.

**Parallel agent surface (4 simultaneous agents):**

| Agent | Scope | Files Touched |
|-------|-------|---------------|
| Agent 1 | Onboarding chat flow | `features/onboarding/*` |
| Agent 2 | Stripe billing | `features/billing/*` |
| Agent 3 | Landing page updates | `features/landing/*` |
| Agent 4 | Status dashboard | `features/status/*` |

### Agent-Site Feature Structure

```
apps/agent-site/
  app/                              # Next.js App Router (thin routing)
    layout.tsx                      # Root layout — schema.org, branding CSS vars
    page.tsx                        # Loads config → selects template
    not-found.tsx
    error.tsx
    global-error.tsx
    globals.css
    robots.ts
    sitemap.ts
    thank-you/page.tsx
    privacy/page.tsx
    terms/page.tsx
    accessibility/page.tsx

  features/
    config/                         # Config system (registry, branding, routing)
      index.ts
      config.ts                     # Config loader
      config-registry.ts            # AUTO-GENERATED (prebuild)
      nav-registry.ts               # AUTO-GENERATED (prebuild)
      types.ts                      # AccountConfig, ContentConfig, AgentConfig
      branding.ts                   # CSS variable builder
      routing.ts                    # Hostname → agentId mapping
      nav-config.ts                 # Navigation builder
      __tests__/

    templates/                      # Page templates (10 designs)
      index.ts                      # Template registry
      types.ts                      # TemplateProps interface
      emerald-classic.tsx
      modern-minimal.tsx
      warm-community.tsx
      luxury-estate.tsx
      urban-loft.tsx
      new-beginnings.tsx
      light-luxury.tsx
      country-estate.tsx
      coastal-living.tsx
      commercial.tsx
      __tests__/

    sections/                       # Composable content blocks
      index.ts
      heroes/                       # 11 hero variants
        index.ts
        HeroAiry.tsx
        HeroBold.tsx
        ...
        hero-utils.tsx
      about/                        # 10 about variants
        index.ts
        ...
      services/                     # 12 service variants
        index.ts
        ...
      profiles/
        index.ts
        ...
      testimonials/
      stats/
      steps/
      sold/
      marquee/
      shared/                       # Footer, ScrollRevealSection
        index.ts
        Footer.tsx
        CmaSection.tsx
        ScrollRevealSection.tsx
      __tests__/                    # Mirrors section structure

    lead-capture/                   # Form wiring + server actions
      index.ts
      submit-lead.ts                # Server action (Turnstile + HMAC)
      hmac.ts                       # HMAC signing
      turnstile.ts                  # Turnstile validation
      safe-contact.ts               # Contact sanitization
      __tests__/

    privacy/                        # Privacy request forms (agent-site-specific)
      index.ts
      privacy.ts                    # Server action
      DeleteRequestForm.tsx
      MyDataForm.tsx
      OptOutForm.tsx
      SubscribeForm.tsx
      __tests__/
      # Note: LegalPageLayout, CookieConsentBanner, CookieConsent, MarkdownContent,
      # and legal constants are imported from @real-estate-star/legal (shared package),
      # NOT duplicated here.

    shared/                         # App-level shared
      index.ts
      Nav.tsx
      GA4Script.tsx                 # Next.js-specific analytics wrapper
      Analytics.tsx                 # Next.js-specific Sentry/GA init
      security-headers.ts
      telemetry.ts                  # App-level telemetry (wraps @real-estate-star/analytics)
      use-focus-trap.ts
      useParallax.ts                # Consolidated from hooks/
      useReducedMotion.ts           # Consolidated from hooks/
      useScrollReveal.ts            # Consolidated from hooks/
      __tests__/

  middleware.ts                     # Multi-tenant routing + CSP headers
                                    # Imports from features/config/ (routing) and
                                    # features/shared/ (security-headers)

  scripts/
    generate-config-registry.mjs    # Prebuild script
```

**Rules:**
1. `app/` pages are thin — load config, select template, render.
2. Features cannot cross-import — except through `features/shared/`.
3. One declared exception: `templates/` → `sections/` — templates compose sections by design. One-way dependency only (template imports section, never the reverse). Templates must import from **subsection barrels** (`sections/heroes`, `sections/about`), never from the top-level `sections/index.ts` barrel.
4. Package imports unrestricted from any feature.
5. Tests live inside the feature.
6. `middleware.ts` lives at app root (Next.js requirement) and may import from `features/config/` and `features/shared/` only.
7. `"use server"` and `"use client"` directives must be preserved on every file during the move — a missing directive silently changes component behavior.

**Agent-site feature dependency graph:**

```
config           → nothing (app-level domain)
sections         → config (reads branding/content types)
sections/shared/ → config (internal to sections, NOT the same as features/shared/)
templates        → config + sections
lead-capture     → config (agentId, API keys)
privacy          → nothing
shared           → config
```

Note: `sections/shared/` (Footer, CmaSection, ScrollRevealSection) is internal to the sections feature — it is shared among section subsections, not across features. It is distinct from `features/shared/` which is cross-feature.

**Parallel agent surface (6 simultaneous agents):**

| Agent | Scope | Files Touched |
|-------|-------|---------------|
| Agent 1 | New template | `features/templates/` |
| Agent 2 | Hero variants | `features/sections/heroes/` |
| Agent 3 | About variants | `features/sections/about/` |
| Agent 4 | Lead capture changes | `features/lead-capture/` |
| Agent 5 | Privacy forms | `features/privacy/` |
| Agent 6 | Config system | `features/config/` |

## Architecture Enforcement

Three layers, mirroring the backend's reflection tests + NetArchTest.

### Layer 1: ESLint Import Restrictions

Each feature folder gets `no-restricted-imports` rules preventing cross-feature imports.

```js
// eslint.config.mjs (both apps)
{
  files: ["features/onboarding/**"],
  rules: {
    "no-restricted-imports": ["error", {
      patterns: [
        { group: ["../billing/*"], message: "Features cannot cross-import. Use shared/ or a package." },
        { group: ["../landing/*"], message: "Features cannot cross-import." },
        { group: ["../status/*"], message: "Features cannot cross-import." },
      ]
    }]
  }
}
```

For agent-site, the `templates/ → sections/` exception is explicitly allowed — all other cross-feature imports are blocked.

**What this catches:** An agent accidentally importing a component from another feature folder. Fails at lint time, before CI even runs tests.

### Layer 2: Package Dependency Validation (CI)

A script that reads each package's `package.json` and validates it only declares allowed internal dependencies.

```
# Allowed dependency map (checked in CI)
packages/domain          → []
packages/api-client      → [domain]
packages/forms           → [domain]
packages/legal           → [domain]
packages/analytics       → [domain]
```

Every package has at most 1 internal dependency (`domain`). `api-client` uses `createCorrelationId()` from `domain` to auto-inject `X-Correlation-ID` headers on every API call.

The script (`scripts/validate-architecture.mjs`):
1. Reads each `packages/*/package.json`
2. Extracts `@real-estate-star/*` dependencies
3. Compares against the allowed map
4. Fails CI if any undeclared internal dependency is found

Runs as a GitHub Actions step before build.

### Layer 3: Architecture Test File

A dedicated test file — `packages/domain/__tests__/architecture.test.ts` — that programmatically validates the rules, similar to `DependencyTests.cs` in the backend.

Reads all `packages/*/package.json` at test time, asserts dependency rules hold, runs with every `npm test`.

**Enforcement summary:**

| Enforcement | What It Catches | When It Runs |
|-------------|----------------|--------------|
| ESLint `no-restricted-imports` | Cross-feature imports within apps | Editor + lint + CI |
| `validate-architecture.mjs` | Undeclared package-to-package deps | CI (GitHub Actions) |
| `architecture.test.ts` | Same as above, in test runner | `npm test` (local + CI) |

## CI/CD Pipeline Changes

The restructure changes file paths, adds new packages, and introduces new validation steps. All 4 app-facing workflows need updates.

### Existing Workflows

| Workflow | Triggers On | Changes Needed |
|----------|------------|----------------|
| `platform.yml` | `apps/platform/**`, `packages/**` | Update path filters, add architecture validation step |
| `agent-site.yml` | `apps/agent-site/**`, `config/accounts/**`, `packages/**` | Update path filters, add architecture validation step, add bundle size check |
| `deploy-platform.yml` | `apps/platform/**`, `packages/**` | Update path filters, add `predev`/`prebuild` api-client generation |
| `deploy-agent-site.yml` | `apps/agent-site/**`, `config/accounts/**`, `packages/**` | Update path filters, add `predev`/`prebuild` api-client generation |
| `api.yml` | `apps/api/**` | Add OpenAPI spec export as artifact |
| `deploy-api.yml` | `apps/api/**` | Add OpenAPI spec export as artifact |
| `drive-monitor.yml` | Scheduled (5min) | No changes |
| `lead-pipeline-smoke.yml` | Scheduled (daily) | No changes |

### New Workflow: `api-client.yml`

Triggers after API CI completes. Regenerates `packages/api-client/generated/types.ts` from the exported OpenAPI spec and commits the updated types. See "API Client Contract Pipeline" section for full workflow definition.

### Path Filter Updates

Current path filters use `packages/**` which is too broad — triggers all app builds when any package changes. After the restructure, use explicit package paths so builds only trigger when relevant packages change:

```yaml
# platform.yml — before
paths: ['apps/platform/**', 'packages/**']

# platform.yml — after
paths:
  - 'apps/platform/**'
  - 'packages/domain/**'
  - 'packages/forms/**'
  - 'packages/legal/**'
  - 'packages/analytics/**'
  - 'packages/api-client/**'
  - '.github/workflows/platform.yml'

# agent-site.yml — after
paths:
  - 'apps/agent-site/**'
  - 'config/accounts/**'
  - 'packages/domain/**'
  - 'packages/forms/**'
  - 'packages/legal/**'
  - 'packages/analytics/**'
  - 'packages/api-client/**'
  - '.github/workflows/agent-site.yml'
```

### New CI Steps

**Architecture validation** (added to both `platform.yml` and `agent-site.yml`):
```yaml
- name: Validate architecture
  run: node scripts/validate-architecture.mjs
```

**Bundle size check** (added to `agent-site.yml` only):
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

**API OpenAPI spec export** (added to `api.yml` and `deploy-api.yml`):
```yaml
- name: Export OpenAPI spec
  run: dotnet run --project apps/api/RealEstateStar.Api -- --export-openapi
- uses: actions/upload-artifact@v4
  with:
    name: openapi-spec
    path: apps/api/openapi.json
```

### Sentry Configuration

**New secret needed for platform:** `SENTRY_DSN` (platform currently has no Sentry). Add to GitHub repo secrets and wire into `deploy-platform.yml`:
```yaml
env:
  NEXT_PUBLIC_SENTRY_DSN: ${{ secrets.PLATFORM_SENTRY_DSN }}
```

Agent-site already has Sentry configured but the DSN may need verification — confirm `NEXT_PUBLIC_SENTRY_DSN` is set in the Cloudflare Worker environment or injected at build time.

## Documentation & Skills Updates

AI agent coders need the right context to write code correctly the first time. The restructure changes file paths, import patterns, and architectural rules — all documentation and skills must be updated to reflect the new structure.

### CLAUDE.md Updates

The project-level `.claude/CLAUDE.md` must be updated to reflect:

1. **New monorepo structure** — Update the directory tree to show `features/` layout in both apps and the new packages (`domain`, `forms`, `legal`, `analytics`)
2. **Package dependency rules** — Add the allowed dependency map (mirrors the API dependency rules section already there)
3. **Feature isolation rules** — Document that features cannot cross-import, with the `templates/ → sections/` exception
4. **Import patterns** — Templates use subsection barrel imports, never top-level `sections/index.ts`
5. **CSS Modules convention** — Agent-site components use colocated `.module.css` files with `@apply`
6. **Bundle optimization rules** — Dynamic template loading, lazy Sentry, no barrel imports in templates
7. **Observability requirements** — Every user-facing action needs a telemetry event, correlation IDs on all API calls
8. **Analytics ownership** — Platform = our keys, agent-site = BYOK from config
9. **API client contract** — `generated/types.ts` is auto-generated, never hand-edit, `predev`/`prebuild` regenerates

### Skills Updates

Skills teach agents HOW to do things. These need updating or creating:

| Skill | Action | What Changes |
|-------|--------|-------------|
| `.claude/skills/ci-cd-pipeline/SKILL.md` | **Update** | Add `api-client.yml` workflow, architecture validation step, bundle size check, updated path filters |
| `.claude/rules/frontend.md` | **Update** | Add feature isolation rules, CSS Modules convention, barrel import restrictions, dynamic import patterns |
| `.claude/rules/code-quality.md` | **Update** | Add frontend observability mandate (telemetry events, correlation IDs, Sentry), bundle size verification step |
| New: `.claude/rules/frontend-architecture.md` | **Create** | Package dependency map, feature folder structure, import rules, enforcement layers — the frontend equivalent of the API architecture rules |
| New: `.claude/skills/learned/frontend-feature-isolation.md` | **Create** | Step-by-step: how to add a new feature folder, what barrel exports look like, how to wire into `app/` routes |
| New: `.claude/skills/learned/agent-site-bundle-optimization.md` | **Create** | Dynamic imports, subsection barrel imports, lazy loading patterns, bundle size verification |
| New: `.claude/skills/learned/css-modules-agent-site.md` | **Create** | When to use CSS Modules, `@apply` patterns, dynamic vs static styles, file naming |

### Onboarding Doc Updates

`docs/onboarding.md` must be updated with:
- New directory structure for both apps
- How to add a new feature to platform or agent-site
- How to add a new section variant to agent-site
- How the api-client contract pipeline works (local dev flow)
- How to verify architecture rules pass locally

### Agent Config for Parallel Work

When spinning up parallel agents, each agent needs context about its scope. The documentation should include a **"Feature Scope Map"** that agents can reference:

```markdown
## Feature Scope Map

### Platform
| Feature | Directory | Can Import From | Cannot Import From |
|---------|-----------|----------------|-------------------|
| onboarding | features/onboarding/ | features/shared/, all packages | billing, landing, status |
| billing | features/billing/ | features/shared/, all packages | onboarding, landing, status |
| landing | features/landing/ | features/shared/, all packages | onboarding, billing, status |
| status | features/status/ | features/shared/, all packages | onboarding, billing, landing |

### Agent-Site
| Feature | Directory | Can Import From | Cannot Import From |
|---------|-----------|----------------|-------------------|
| config | features/config/ | all packages | templates, sections, lead-capture, privacy |
| templates | features/templates/ | features/sections/*, features/config/, all packages | lead-capture, privacy, shared |
| sections | features/sections/ | features/config/, all packages | templates, lead-capture, privacy |
| lead-capture | features/lead-capture/ | features/config/, all packages | templates, sections, privacy |
| privacy | features/privacy/ | all packages | templates, sections, config, lead-capture |
| shared | features/shared/ | features/config/, all packages | templates, sections, lead-capture, privacy |
```

This map goes into both CLAUDE.md and the onboarding doc. When an agent is dispatched to work on "billing", it can immediately see what it can and cannot touch.

## Migration Strategy

### Phased Approach

**Phase 1: Shared packages** (no app changes yet)
1. Create `packages/domain` — move types from `shared-types`, add any new shared interfaces
2. Create `packages/forms` — move `LeadForm/` and `cma/` from `packages/ui`
3. Create `packages/legal` — move `EqualHousingNotice/` from `packages/ui`
4. Create `packages/analytics` — extract framework-agnostic telemetry helpers, event types, conversion tracking logic from agent-site (Next.js-specific wrappers like `GA4Script.tsx` stay in-app)
5. Update `packages/api-client` to depend on `domain`
6. Un-gitignore `packages/api-client/generated/types.ts` — commit generated types to repo
7. Run `npm run generate:ci` to produce initial `generated/types.ts` from checked-in `openapi.json`
8. Update root `package.json` workspaces to include new packages
9. Add `"sideEffects": false` to all new packages' `package.json` (enables tree-shaking)
10. Delete `packages/shared-types` and `packages/ui` — update all imports in both apps
11. All tests pass, CI green

**Phase 2: Platform restructure** (parallel with Phase 3)
1. Create `features/` folders with barrel exports
2. Move components into their feature folders
3. Thin out `app/` route files to just import + render
4. Move tests into feature folders
5. Add Sentry integration (`sentry.client.config.ts`, `sentry.server.config.ts`)
6. Replace `console.error` calls in onboarding/payment flows with `Sentry.captureException()`
7. Add `WebVitalsReporter` to root layout (from `@real-estate-star/analytics`)
8. Add onboarding funnel events (session_created, message_sent, card_displayed, card_action, payment_initiated, completed)
9. Add ESLint cross-feature import rules
10. All tests pass, 100% coverage maintained

**Phase 3: Agent-site restructure** (parallel with Phase 2)
1. Create `features/` folders
2. Move `lib/` utilities into `features/config/`
3. Move `actions/` into `features/lead-capture/` and `features/privacy/`
4. Move `components/sections/` into `features/sections/`
5. Move `templates/` into `features/templates/`
6. Move `components/legal/` and `components/privacy/` into `features/privacy/`
7. Move `components/` shared pieces + `hooks/` into `features/shared/`
8. Move Next.js-specific analytics wrappers (`GA4Script.tsx`, `Analytics.tsx`) into `features/shared/`
9. Move `telemetry.ts` into `features/shared/`
10. Update `middleware.ts` imports: `@/lib/routing` → `@/features/config/routing`, `@/lib/security-headers` → `@/features/shared/security-headers`
11. Update `generate-config-registry.mjs` output paths: `lib/config-registry.ts` → `features/config/config-registry.ts`, `lib/nav-registry.ts` → `features/config/nav-registry.ts`
12. Update `.gitignore` entries for auto-generated registry files
13. Thin out `app/` route files
14. Update tsconfig `paths` — `@/*` alias continues to resolve to app root (no change needed, features/ is under root)
15. Verify `"use server"` and `"use client"` directives preserved on all moved files
16. Replace `console.error` calls with `Sentry.captureException()` where only console logging today
17. Add privacy form telemetry (viewed, started, submitted, succeeded, failed)
18. Add `WebVitalsReporter` to root layout (from `@real-estate-star/analytics`)
19. Implement dynamic template loading (`next/dynamic` per template)
20. Convert template imports to direct subsection barrel imports (not top-level `sections/index.ts`)
21. Add ESLint rule blocking templates from importing top-level sections barrel
22. Dynamic import Turnstile (only load when `NEXT_PUBLIC_TURNSTILE_SITE_KEY` is set)
23. Lazy-load Sentry `captureException` in application code (keep root config for automatic capture)
24. Add ESLint cross-feature import rules
25. Measure Worker bundle size (`wc -c .open-next/worker.js`) — document before/after in PR
26. All tests pass, 100% coverage maintained

**Phase 3b: CSS Modules migration (agent-site, after Phase 3)**
1. For each section component (~93 files), extract inline Tailwind classes into colocated `.module.css` files
2. Use `@apply` in CSS Modules to preserve Tailwind utilities — no raw CSS values
3. Keep dynamic/conditional classes inline (runtime values)
4. Keep config-driven branding `style={}` props inline (CSS variables from config)
5. Highly parallelizable — each section subsection (heroes/, about/, services/) can be migrated by a separate agent
6. No visual changes — verify screenshots match before/after (optional)
7. All tests pass, 100% coverage maintained

**Phase 4: Architecture enforcement + API client pipeline** (after Phases 2+3)
1. Add `validate-architecture.mjs` script
2. Add `architecture.test.ts`
3. Add architecture validation step to `platform.yml` and `agent-site.yml`
4. Add bundle size check step to `agent-site.yml` (warn at 2.5MB, fail at 3MB)
5. Update path filters in `platform.yml`, `agent-site.yml`, `deploy-platform.yml`, `deploy-agent-site.yml` — replace `packages/**` with explicit package paths
6. Add `.github/workflows/api-client.yml` — triggers on API CI completion, regenerates types, commits to repo
7. Update `api.yml` and `deploy-api.yml` to export `openapi.json` as a build artifact
8. Add `PLATFORM_SENTRY_DSN` secret to GitHub repo, wire into `deploy-platform.yml`
9. Verify all rules pass, end-to-end contract pipeline works

**Phase 5: Documentation & skills** (after Phase 4)
1. Update `.claude/CLAUDE.md` — new directory structure, package dependency map, feature isolation rules, import patterns, CSS Modules convention, bundle rules, observability requirements, analytics ownership, api-client contract, Feature Scope Map
2. Update `.claude/rules/frontend.md` — add feature isolation, CSS Modules, barrel import restrictions, dynamic import patterns
3. Update `.claude/rules/code-quality.md` — add frontend observability mandate (telemetry events, correlation IDs, Sentry), bundle size verification
4. Create `.claude/rules/frontend-architecture.md` — package dependency map, feature folder structure, import rules, enforcement layers (frontend equivalent of API architecture rules)
5. Create `.claude/skills/learned/frontend-feature-isolation.md` — how to add a new feature folder, barrel exports, wiring into `app/` routes
6. Create `.claude/skills/learned/agent-site-bundle-optimization.md` — dynamic imports, subsection barrel imports, lazy loading, bundle size verification
7. Create `.claude/skills/learned/css-modules-agent-site.md` — when to use CSS Modules, `@apply` patterns, dynamic vs static, file naming
8. Update `.claude/skills/ci-cd-pipeline/SKILL.md` — add `api-client.yml` workflow, architecture validation, bundle size check, updated path filters
9. Update `docs/onboarding.md` — new directory structure, how to add features, how api-client pipeline works, how to verify architecture locally
10. Verify: dispatch a test agent to "add a new feature to platform" and confirm it follows the new patterns from documentation alone

### Phase Dependencies

```
Phase 1 (packages)
    ├── Phase 2 (platform + observability)     ← parallel
    └── Phase 3 (agent-site + observability)   ← parallel
         └── Phase 3b (CSS Modules migration)
              └── Phase 4 (CI/CD + enforcement + API client pipeline)
                   └── Phase 5 (documentation & skills)
```

Phase 5 is last because documentation must reflect the final state, not an intermediate one. Step 10 (test agent dispatch) validates that the documentation is sufficient for agents to write correct code on the first try.

### Risk Mitigation

- **No behavior changes** — pure restructure. Every file moves, no logic changes.
- **Import rewrites are mechanical** — find/replace `@real-estate-star/shared-types` → `@real-estate-star/domain`, etc.
- **Tests validate the move** — 100% coverage already exists. If tests pass after the move, nothing broke.
- **One PR per phase** — keeps reviews manageable and rollback clean.
- **Bundle size verification** — agent-site has a hard 3MB Cloudflare Worker limit. Measure bundle size before Phase 3 and after. Barrel exports must not defeat tree-shaking. Use named re-exports only (`export { Foo } from './Foo'`), never `export * from`.
- **Sentry source maps** — moving files changes stack trace paths. Source maps regenerate on next build/deploy, so this resolves automatically. No action needed beyond deploying after merge.
- **Directive preservation** — every moved file must retain its `"use server"` or `"use client"` directive. A missing directive silently changes component rendering behavior (server vs client). Verify with grep after each move.

### Barrel Export Convention

All barrel exports (`index.ts`) use **named re-exports only** for tree-shaking safety:

```ts
// Good — tree-shakeable
export { LeadForm } from './LeadForm';
export { useGoogleMapsAutocomplete } from './useGoogleMapsAutocomplete';

// Bad — defeats tree-shaking, bloats bundle
export * from './LeadForm';
```

Each section subsection (`heroes/index.ts`, `about/index.ts`) has its own barrel. The top-level `sections/index.ts` re-exports from subsection barrels. This means agents adding variants to different subsections only touch their subsection's `index.ts`, not the shared top-level barrel.
