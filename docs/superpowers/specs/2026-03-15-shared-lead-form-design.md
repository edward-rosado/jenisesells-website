# Shared Lead Form Component — Design Spec

## Goal

Extract the CMA form from `apps/agent-site` into a shared, reusable `<LeadForm>` component in `packages/ui/` that supports both buyers and sellers, uses Google Maps autocomplete for address entry, and works across all agent sites (`{handle}.real-estate-star.com`, custom domains) and the platform (`platform.real-estate-star.com`).

## Background

The current `CmaForm` in `apps/agent-site/components/sections/CmaForm.tsx` is seller-only, tightly coupled to the agent-site (formspree, SignalR progress tracker, analytics), and not importable by other apps. This design extracts the form card into a shared component with a clean `onSubmit` callback, leaving submission logic and section wrappers to consumers.

---

## Architecture

### Component Approach

Single `<LeadForm>` component with mode props. The component manages its own checkbox state, conditional field visibility, and client-side validation. It emits a typed `LeadFormData` payload via `onSubmit` — consumers decide what to do with it (API call, formspree, chat flow, etc.).

No headless hook layer. No JSON-driven form renderer. If we later need headless access, we extract the hook from internals — YAGNI today.

### Data Flow

```
User fills form
       |
       v
<LeadForm onSubmit={handler}>
       |
       v
handler receives LeadFormData
       |
       +---> agent-site: calls useCmaSubmit() or formspree
       +---> platform: passes to chat/preview context
       +---> future: any consumer
```

---

## Types (`packages/shared-types/lead-form.ts`)

```ts
export type LeadType = "buying" | "selling";

export type PreApprovalStatus = "yes" | "no" | "in-progress";

export type Timeline =
  | "asap"
  | "1-3months"
  | "3-6months"
  | "6-12months"
  | "justcurious";

export interface BuyerDetails {
  desiredArea: string;
  minPrice?: number;
  maxPrice?: number;
  minBeds?: number;
  minBaths?: number;
  preApproved?: PreApprovalStatus;  // omitted when unselected
  preApprovalAmount?: number;
}

export interface SellerDetails {
  address: string;
  city: string;
  state: string;
  zip: string;
  beds?: number;
  baths?: number;
  sqft?: number;
}

export interface LeadFormData {
  leadTypes: LeadType[];          // at least one required
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  buyer?: BuyerDetails;           // present when "buying" checked
  seller?: SellerDetails;         // present when "selling" checked
  timeline: Timeline;
  notes?: string;
}
```

---

## Component API (`packages/ui/LeadForm`)

```ts
export interface LeadFormProps {
  defaultState: string;           // pre-fill state field for sellers (must be non-empty 2-letter code)
  googleMapsApiKey: string;
  onSubmit: (data: LeadFormData) => void | Promise<void>;
  initialMode?: LeadType[];       // optional pre-check, defaults to []. Pre-checks those pills on mount.
  submitLabel?: string;           // defaults to "Get Started"
  disabled?: boolean;             // external loading/disabled control
  error?: string;                 // external error message to display above submit button
}
```

### Key constraints

- **No submission logic inside the component.** No SignalR, no formspree, no redirect, no analytics. Just data out via `onSubmit`.
- **No section wrapper.** The component renders the form card only. Consumers provide their own section (title, subtitle, background, description text). Consumer owns `id="cma-form"` on their section wrapper for anchor navigation.
- **At least one checkbox must be checked.** Validation via `useState` guard in the submit handler — shows inline error text below the checkboxes. No HTML5 `required` on checkboxes (poor UX).
- **`onSubmit` is the only integration point.** If `onSubmit` throws, the component catches it, sets internal submitting state back to false, and relies on the consumer's `error` prop for display.
- **Double-submit prevention.** Component tracks its own `submitting` state internally. While `onSubmit` is in flight, the submit button is disabled regardless of the `disabled` prop.
- **`LeadForm` is a Client Component** (`"use client"`). The injected `<style>` tag is rendered inline in JSX (not dynamically appended), so it survives SSR hydration without flicker. The Google Maps hook must never run server-side — it is gated on `typeof window !== "undefined"`.
- **`agentId` removed** — not needed by the form component. Consumers have it in their own scope for submission logic.

### Timeline label adaptation

- Selling only: "When are you looking to sell?"
- Buying only: "When are you looking to buy?"
- Both: "When are you looking to buy/sell?"

---

## Workspace Bootstrap (Prerequisite)

The monorepo currently has no workspace manager configured. `packages/shared-types/` and `packages/ui/` are empty (`.gitkeep` only). Before any cross-package imports work, the workspace infrastructure must be set up:

1. **Root `package.json`** — Add `"workspaces": ["apps/*", "packages/*"]` to enable npm workspaces.
2. **`packages/shared-types/package.json`** — `name: "@real-estate-star/shared-types"`, `main: "index.ts"`, no deps.
3. **`packages/ui/package.json`** — `name: "@real-estate-star/ui"`, `main: "index.ts"`, `peerDependencies: { "react": ">=19", "react-dom": ">=19", "next": ">=16" }`. React as a peer dep avoids the "two Reacts" hook error.
4. **`npm install`** at root to link workspace packages.
5. **`next.config.ts`** in both `apps/agent-site/` and `apps/platform/` — add `transpilePackages: ["@real-estate-star/ui", "@real-estate-star/shared-types"]` so Next.js compiles the source TypeScript from packages.

No bundler needed for the packages — consumers import source directly. TypeScript `composite: true` in package `tsconfig.json` for project references.

### Acceptance criteria

- `import { LeadForm } from "@real-estate-star/ui"` resolves in agent-site
- `import { LeadFormData } from "@real-estate-star/shared-types"` resolves in agent-site
- `npm run build` succeeds in agent-site with the new imports
- No "Invalid hook call" errors (React is not duplicated)

---

## File Structure

### New files

```
packages/shared-types/
  lead-form.ts                    # LeadType, BuyerDetails, SellerDetails, LeadFormData
  index.ts                        # barrel export
  package.json                    # @real-estate-star/shared-types

packages/ui/
  package.json                    # @real-estate-star/ui
  tsconfig.json
  LeadForm/
    LeadForm.tsx                  # the component
    useGoogleMapsAutocomplete.ts  # hook: lazy-loads Places API, attaches to input ref
    LeadForm.test.tsx             # vitest + testing-library tests
    index.ts                      # barrel export
  index.ts                        # package barrel
```

### Modified files

```
apps/agent-site/
  components/sections/CmaForm.tsx
    - Becomes a thin wrapper:
      - Renders the section (title, subtitle, gradient bg, description)
      - Imports <LeadForm> from @real-estate-star/ui
      - Passes onSubmit that bridges to useCmaSubmit() or formspree
      - Keeps ProgressTracker sub-component (agent-site only)
  lib/types.ts
    - CmaFormData (content config type) stays
    - LeadFormData imported from @real-estate-star/shared-types
  package.json
    - Add workspace deps: @real-estate-star/ui, @real-estate-star/shared-types
  next.config.ts
    - Add transpilePackages: ["@real-estate-star/ui", "@real-estate-star/shared-types"]

apps/platform/
  package.json
    - Add workspace deps (for future use — no platform changes in this scope)
```

### What stays in agent-site

- Section wrapper (title, subtitle, gradient background, description text)
- `ProgressTracker` sub-component (SignalR progress UI)
- `useCmaSubmit` hook and `cma-api.ts` (API mode submission)
- Formspree submission logic
- Analytics tracking (`trackCmaConversion`)
- Sentry error reporting
- `onSubmit` bridge that maps `LeadFormData` → existing submission flows

---

## Form Layout

### Lead type selection

Pill-style checkboxes centered at the top of the form card:
- **"I'm Buying"** — gold/accent border and tint (`--color-accent`)
- **"I'm Selling"** — green/primary border and tint (`--color-secondary`)
- Both can be checked simultaneously
- At least one must be checked (validation)

### Form field order

1. **Pill checkboxes** (buying / selling)
2. **Contact fields** (always visible): First Name, Last Name, Email, Phone
3. **Buyer card** (visible when "buying" checked): gold-tinted background card
   - Desired Area / City
   - Price Range (Min / Max side-by-side)
   - Min Beds / Min Baths (side-by-side)
   - Pre-approved? (select: Yes / No / In Progress) + Pre-approval Amount (optional)
4. **Seller card** (visible when "selling" checked): green-tinted background card
   - Property Address (Google Maps autocomplete)
   - City / State / Zip
   - Beds / Baths / Sqft (all optional)
5. **Shared fields**: Timeline dropdown, Notes textarea
6. **Submit button**

### Card show/hide animation

Buyer/seller cards animate in/out with `max-height` (set to `800px` when visible, `0` when hidden) + `opacity` CSS transition (300ms ease). The `800px` ceiling accommodates all current fields with room to spare. `overflow: hidden` on the card container prevents content flash during animation.

---

## Google Maps Autocomplete

### Hook: `useGoogleMapsAutocomplete`

```ts
interface UseGoogleMapsAutocompleteOptions {
  apiKey: string;
  inputRef: RefObject<HTMLInputElement>;
  onPlaceSelected: (place: {
    address: string;
    city: string;
    state: string;
    zip: string;
  }) => void;
  enabled: boolean;  // only load when seller section is visible
}

interface UseGoogleMapsAutocompleteReturn {
  loaded: boolean;  // true once SDK is ready and autocomplete is attached
}
```

### Behavior

- Lazily loads Google Maps Places JS SDK via `<script>` tag when `enabled` becomes `true`
- Returns `{ loaded }` so the component can show a loading indicator on the address input
- Deduplicates script loading (module-level promise — multiple instances don't load twice). Test: render two instances, assert exactly one `<script>` tag appended.
- Attaches `Autocomplete` to the input ref, restricted to US addresses (`componentRestrictions: { country: "us" }`)
- On place selection, parses `address_components` into structured fields and calls `onPlaceSelected`. Returns empty string for missing components (e.g., addresses without zip code use `""` for zip).
- Auto-fills city, state, zip — user can still override manually
- Cleans up listener on unmount
- No external dependency (`@googlemaps/js-api-loader` not needed)
- **Client-only constraint:** Gated on `typeof window !== "undefined"`. Must never be called in SSR or Cloudflare Workers edge context. The module-level promise is safe because the component is `"use client"` and only runs in the browser.

### API key management

- **Phase 1 (this spec):** Platform-level API key. Real Estate Star owns one key, passed as prop. Key is restricted to `*.real-estate-star.com` + registered custom domains in Google Cloud Console.
- **Phase 2 (future):** Per-agent override via agent config. Component API already supports this since the key is a prop.

---

## Responsive Design

### Styling approach

Inline styles using CSS variables, matching the existing agent-site pattern. No Tailwind dependency in `packages/ui/` — keeps the package consumer-agnostic.

A single `<style>` tag injected by the component with `@media (max-width: 768px)` overrides. Scoped with `.lead-form-` class prefix to avoid collisions with host page styles.

### CSS variables consumed

| Variable | Purpose | Fallback |
|----------|---------|----------|
| `--color-primary` | Headings, submit button bg | `#1B5E20` |
| `--color-accent` | Buyer pill border, highlights | `#C8A951` |
| `--color-secondary` | Seller pill border | `#2E7D32` |

### Breakpoint behavior (mobile <= 768px)

| Element | Desktop | Mobile |
|---------|---------|--------|
| Pill checkboxes | Horizontal, centered | Stack vertically, full width |
| Name fields | Side-by-side | Side-by-side (short fields) |
| Price range | Side-by-side | Side-by-side |
| Beds/Baths (buyer) | Side-by-side | Side-by-side |
| Pre-approved / Amount | Side-by-side | Stack vertically |
| City / State / Zip | 2:1:1 flex row | City full width, State+Zip side-by-side |
| Beds / Baths / Sqft (seller) | 3-column grid | 3-column grid |
| Form card padding | clamp(20px, 5vw, 40px) | clamp(16px, 4vw, 28px) |

### Submit button

Hover effect: slight lift (`translateY(-2px)`) + box shadow, matching existing Hero CTA pattern.

---

## Cloudflare Pages Preview Deployments

### Purpose

Enable visual review of agent-site changes on PRs before merging. Required for the screenshot-based visual regression check during migration.

### Setup

Cloudflare Pages natively supports preview deployments. Every push to a PR branch that touches `apps/agent-site/` gets a preview URL like `{commit-hash}.agent-site.pages.dev`.

### CI workflow addition

Add a GitHub Actions job to `.github/workflows/agent-site.yml`:

1. **Build** — `npm run build` in `apps/agent-site/` (already exists)
2. **Deploy preview** — `wrangler pages deploy` with `--branch=${{ github.head_ref }}` flag. Cloudflare Pages auto-generates a preview URL for non-production branches.
3. **Comment on PR** — Post the preview URL as a PR comment so reviewers can click through

### DNS / domain

Preview deploys use the `*.agent-site.pages.dev` subdomain automatically. No custom domain configuration needed for previews.

### Implementation order

Preview deploy CI is a **precursor task** — implement it first (separate commit) before the form migration. This way the migration PR itself gets a preview URL for visual comparison.

---

## Visual Regression — Migration Quality Gate

### Before migration

1. Run the current agent site locally with the existing `CmaForm.tsx`
2. Capture screenshots at two viewport sizes:
   - Desktop: 1440x900
   - Mobile: 375x812
3. Store screenshots in `docs/superpowers/screenshots/lead-form-migration/before/`

### After migration

1. Wire up the new `<LeadForm>` component in the agent-site wrapper
2. Render with `initialMode={["selling"]}` to match the current seller-only view
3. Capture screenshots at the same viewport sizes
4. Store in `docs/superpowers/screenshots/lead-form-migration/after/`

### Comparison

- Manual side-by-side comparison during PR review
- The seller-only view of the new component should be visually identical to the current form card
- Screenshots committed to the repo for traceability

### What's compared

- Field labels, placeholders, and order
- Input styling (borders, border-radius, padding)
- Form card styling (background, shadow, border-radius)
- Submit button styling
- Responsive layout at 375px width

---

## Testing Strategy

### Unit tests (`packages/ui/LeadForm/LeadForm.test.tsx`)

- Renders all contact fields with labels
- Renders pill checkboxes for buying/selling
- Shows buyer card when "buying" checked, hides when unchecked
- Shows seller card when "selling" checked, hides when unchecked
- Shows both cards when both checked
- Prevents submission with neither checkbox checked
- Calls `onSubmit` with correct `LeadFormData` shape (buyer only)
- Calls `onSubmit` with correct `LeadFormData` shape (seller only)
- Calls `onSubmit` with correct `LeadFormData` shape (both)
- Pre-fills state field from `defaultState` prop
- Respects `initialMode` prop
- Respects `submitLabel` prop
- Respects `disabled` prop
- Timeline label adapts to checked modes
- All inputs have accessible labels
- Form is keyboard-navigable
- Does not call `onSubmit` a second time while first call is in flight (double-submit prevention)
- Does not crash if `onSubmit` throws — resets submitting state
- Shows `error` prop value when provided by consumer
- Clears internal submitting state when `onSubmit` rejects

### Google Maps autocomplete tests

- Mock `window.google.maps.places.Autocomplete`
- Script tag injected when `enabled=true`
- Script tag NOT injected when `enabled=false`
- `onPlaceSelected` fires with parsed address components
- Auto-fills city, state, zip on place selection
- Deduplicates script loading across multiple instances (render two LeadForms, assert one `<script>` tag)
- Returns empty string for missing address components (e.g., no zip code)
- Returns `{ loaded: false }` initially, `{ loaded: true }` after SDK ready

### Agent-site wrapper tests (update existing `CmaForm.test.tsx`)

- Renders `<LeadForm>` inside the section wrapper
- `onSubmit` bridge correctly calls `useCmaSubmit` for API mode
- `onSubmit` bridge correctly calls formspree for formspree mode
- ProgressTracker still works as before
- Legacy parity test: seller-only mode produces same fields/labels as before

### Coverage

100% branch coverage required per project rules.

---

## Out of Scope

- Platform integration (embedding the form in `apps/platform`) — future work
- Per-agent Google API key override — Phase 2
- SignalR progress tracking in the shared component — stays in agent-site wrapper
- Form analytics/tracking — stays in agent-site wrapper
- Headless hook extraction — YAGNI, extract later if needed
- PR preview deployment for the platform app — only agent-site in scope
