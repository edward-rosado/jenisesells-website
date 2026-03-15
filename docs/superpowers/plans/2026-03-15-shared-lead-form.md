# Shared Lead Form — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the CMA form into a shared `<LeadForm>` component in `packages/ui/` with buyer/seller checkboxes, Google Maps autocomplete, and responsive design. Wire it into agent-site as a drop-in replacement.

**Architecture:** Single `<LeadForm>` component with `onSubmit` callback. Types in `packages/shared-types/`. npm workspaces for cross-package imports. Agent-site `CmaForm.tsx` becomes a thin wrapper.

**Tech Stack:** React 19, Next.js 16, TypeScript, vitest, testing-library, Google Maps Places API, npm workspaces, Cloudflare Workers (OpenNext)

**Spec:** `docs/superpowers/specs/2026-03-15-shared-lead-form-design.md`

---

## Chunk 1: Workspace Bootstrap & Shared Types

### Task 1: Create root package.json with npm workspaces

**Files:**
- Create: `package.json` (root)

- [ ] **Step 1: Create root package.json**

```json
{
  "name": "real-estate-star",
  "private": true,
  "workspaces": [
    "apps/*",
    "packages/*"
  ]
}
```

- [ ] **Step 2: Verify workspace resolution**

Run: `npm ls --workspaces 2>&1 | head -20`
Expected: Lists `agent-site`, `platform`, `shared-types`, `ui` as workspaces

---

### Task 2: Bootstrap packages/shared-types

**Files:**
- Create: `packages/shared-types/package.json`
- Create: `packages/shared-types/tsconfig.json`
- Create: `packages/shared-types/lead-form.ts`
- Create: `packages/shared-types/index.ts`
- Remove: `packages/shared-types/.gitkeep`

- [ ] **Step 1: Create package.json**

```json
{
  "name": "@real-estate-star/shared-types",
  "version": "0.1.0",
  "private": true,
  "main": "index.ts",
  "types": "index.ts"
}
```

- [ ] **Step 2: Create tsconfig.json**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "declaration": true,
    "composite": true,
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "outDir": "dist"
  },
  "include": ["*.ts"]
}
```

- [ ] **Step 3: Create lead-form.ts with types from spec**

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
  preApproved?: PreApprovalStatus;
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
  leadTypes: LeadType[];
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  buyer?: BuyerDetails;
  seller?: SellerDetails;
  timeline: Timeline;
  notes?: string;
}
```

- [ ] **Step 4: Create index.ts barrel**

```ts
export type {
  LeadType,
  PreApprovalStatus,
  Timeline,
  BuyerDetails,
  SellerDetails,
  LeadFormData,
} from "./lead-form";
```

- [ ] **Step 5: Remove .gitkeep**

```bash
rm packages/shared-types/.gitkeep
```

- [ ] **Step 6: Commit**

```bash
git add packages/shared-types/ package.json
git commit -m "feat: bootstrap shared-types package with LeadFormData types"
```

---

### Task 3: Bootstrap packages/ui

**Files:**
- Create: `packages/ui/package.json`
- Create: `packages/ui/tsconfig.json`
- Create: `packages/ui/index.ts`
- Create: `packages/ui/LeadForm/index.ts`
- Remove: `packages/ui/.gitkeep` (if exists)

- [ ] **Step 1: Create package.json**

```json
{
  "name": "@real-estate-star/ui",
  "version": "0.1.0",
  "private": true,
  "main": "index.ts",
  "types": "index.ts",
  "peerDependencies": {
    "react": ">=19",
    "react-dom": ">=19",
    "next": ">=16"
  },
  "dependencies": {
    "@real-estate-star/shared-types": "*"
  },
  "devDependencies": {
    "@testing-library/dom": "^10.4.1",
    "@testing-library/jest-dom": "^6.9.1",
    "@testing-library/react": "^16.3.2",
    "@types/react": "^19",
    "@types/react-dom": "^19",
    "@vitejs/plugin-react": "^5.1.4",
    "@vitest/coverage-v8": "^4.0.18",
    "jsdom": "^28.1.0",
    "react": "19.2.3",
    "react-dom": "19.2.3",
    "next": "16.1.6",
    "typescript": "^5",
    "vitest": "^4.0.18"
  }
}
```

Note: React/Next in devDeps for testing only. peerDeps ensures consumers provide their own copies.

- [ ] **Step 2: Create tsconfig.json**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "jsx": "react-jsx",
    "declaration": true,
    "composite": true,
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "outDir": "dist",
    "paths": {
      "@real-estate-star/shared-types": ["../shared-types/index.ts"]
    }
  },
  "include": ["**/*.ts", "**/*.tsx"],
  "references": [
    { "path": "../shared-types" }
  ]
}
```

- [ ] **Step 3: Create vitest.config.ts**

```ts
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    setupFiles: "./vitest.setup.ts",
    coverage: {
      provider: "v8",
      thresholds: {
        branches: 100,
        functions: 100,
        lines: 100,
        statements: 100,
      },
    },
  },
});
```

- [ ] **Step 4: Create vitest.setup.ts**

```ts
import "@testing-library/jest-dom/vitest";
```

- [ ] **Step 5: Create LeadForm/index.ts and package index.ts stubs**

LeadForm/index.ts:
```ts
export { LeadForm } from "./LeadForm";
export type { LeadFormProps } from "./LeadForm";
```

index.ts:
```ts
export { LeadForm } from "./LeadForm";
export type { LeadFormProps } from "./LeadForm/LeadForm";
```

- [ ] **Step 6: Remove .gitkeep if exists, run npm install at root**

```bash
rm -f packages/ui/.gitkeep
npm install
```

- [ ] **Step 7: Commit**

```bash
git add packages/ui/ package-lock.json
git commit -m "feat: bootstrap packages/ui with vitest config and LeadForm stub"
```

---

## Chunk 2: Visual Regression — Before Screenshots

### Task 4: Capture before-migration screenshots

**Files:**
- Create: `docs/superpowers/screenshots/lead-form-migration/before/desktop.png`
- Create: `docs/superpowers/screenshots/lead-form-migration/before/mobile.png`

- [ ] **Step 1: Start agent-site dev server locally**

```bash
cd apps/agent-site && npm run dev &
```

Wait for server ready on port 3000.

- [ ] **Step 2: Capture desktop screenshot (1440x900)**

Use Playwright or browser devtools to capture the CMA form section at `http://localhost:3000/jenise-buckalew#cma-form` at 1440x900 viewport.

Save to: `docs/superpowers/screenshots/lead-form-migration/before/desktop.png`

- [ ] **Step 3: Capture mobile screenshot (375x812)**

Same URL at 375x812 viewport.

Save to: `docs/superpowers/screenshots/lead-form-migration/before/mobile.png`

- [ ] **Step 4: Stop dev server, commit screenshots**

```bash
mkdir -p docs/superpowers/screenshots/lead-form-migration/before
git add docs/superpowers/screenshots/
git commit -m "docs: capture before-migration CMA form screenshots"
```

---

## Chunk 3: Google Maps Autocomplete Hook

### Task 5: Implement useGoogleMapsAutocomplete hook

**Files:**
- Create: `packages/ui/LeadForm/useGoogleMapsAutocomplete.ts`
- Create: `packages/ui/LeadForm/useGoogleMapsAutocomplete.test.ts`

- [ ] **Step 1: Write failing tests**

Test cases:
- Does not inject script tag when `enabled=false`
- Injects script tag when `enabled=true`
- Returns `{ loaded: false }` initially
- Returns `{ loaded: true }` after SDK loads
- Calls `onPlaceSelected` with parsed address components
- Returns empty string for missing address components (no zip)
- Deduplicates script loading (render two hooks, assert one script tag)
- Cleans up on unmount

Mock `window.google.maps.places.Autocomplete` as a class with `addListener` and `getPlace` methods.

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd packages/ui && npx vitest run --reporter=verbose
```

Expected: All tests FAIL

- [ ] **Step 3: Implement the hook**

```ts
"use client";

import { useEffect, useState, type RefObject } from "react";

interface PlaceResult {
  address: string;
  city: string;
  state: string;
  zip: string;
}

interface UseGoogleMapsAutocompleteOptions {
  apiKey: string;
  inputRef: RefObject<HTMLInputElement>;
  onPlaceSelected: (place: PlaceResult) => void;
  enabled: boolean;
}

interface UseGoogleMapsAutocompleteReturn {
  loaded: boolean;
}

// Module-level promise for script deduplication (client-only)
let loadPromise: Promise<void> | null = null;

function loadGoogleMapsScript(apiKey: string): Promise<void> {
  if (loadPromise) return loadPromise;
  if (typeof window !== "undefined" && window.google?.maps?.places) {
    return Promise.resolve();
  }
  loadPromise = new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=places`;
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error("Failed to load Google Maps SDK"));
    document.head.appendChild(script);
  });
  return loadPromise;
}

function parseAddressComponents(components: google.maps.GeocoderAddressComponent[]): PlaceResult {
  let address = "";
  let city = "";
  let state = "";
  let zip = "";

  for (const comp of components) {
    const types = comp.types;
    if (types.includes("street_number")) {
      address = comp.long_name + " " + address;
    } else if (types.includes("route")) {
      address = address + comp.long_name;
    } else if (types.includes("locality")) {
      city = comp.long_name;
    } else if (types.includes("sublocality_level_1") && !city) {
      city = comp.long_name;
    } else if (types.includes("administrative_area_level_1")) {
      state = comp.short_name;
    } else if (types.includes("postal_code")) {
      zip = comp.long_name;
    }
  }

  return { address: address.trim(), city, state, zip };
}

export function useGoogleMapsAutocomplete({
  apiKey,
  inputRef,
  onPlaceSelected,
  enabled,
}: UseGoogleMapsAutocompleteOptions): UseGoogleMapsAutocompleteReturn {
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (!enabled || typeof window === "undefined") return;

    let autocomplete: google.maps.places.Autocomplete | null = null;
    let listener: google.maps.MapsEventListener | null = null;

    loadGoogleMapsScript(apiKey)
      .then(() => {
        if (!inputRef.current) return;
        autocomplete = new google.maps.places.Autocomplete(inputRef.current, {
          componentRestrictions: { country: "us" },
          fields: ["address_components"],
          types: ["address"],
        });
        listener = autocomplete.addListener("place_changed", () => {
          const place = autocomplete!.getPlace();
          if (place.address_components) {
            onPlaceSelected(parseAddressComponents(place.address_components));
          }
        });
        setLoaded(true);
      })
      .catch(() => {
        // SDK load failed — form still works, just no autocomplete
      });

    return () => {
      if (listener) google.maps.event.removeListener(listener);
    };
  }, [enabled, apiKey, inputRef, onPlaceSelected]);

  return { loaded };
}

// Export for testing
export { loadGoogleMapsScript, parseAddressComponents };
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd packages/ui && npx vitest run --reporter=verbose
```

Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add packages/ui/LeadForm/useGoogleMapsAutocomplete*
git commit -m "feat: implement useGoogleMapsAutocomplete hook with lazy loading"
```

---

## Chunk 4: LeadForm Component

### Task 6: Write LeadForm tests (TDD — red phase)

**Files:**
- Create: `packages/ui/LeadForm/LeadForm.test.tsx`

- [ ] **Step 1: Write all test cases from spec**

Test cases (see spec for full list):
1. Renders all contact fields with labels
2. Renders pill checkboxes for buying/selling
3. Shows buyer card when "buying" checked, hides when unchecked
4. Shows seller card when "selling" checked, hides when unchecked
5. Shows both cards when both checked
6. Prevents submission with neither checkbox checked
7. Calls `onSubmit` with correct `LeadFormData` shape (buyer only)
8. Calls `onSubmit` with correct `LeadFormData` shape (seller only)
9. Calls `onSubmit` with correct `LeadFormData` shape (both)
10. Pre-fills state field from `defaultState` prop
11. Respects `initialMode` prop
12. Respects `submitLabel` prop
13. Respects `disabled` prop
14. Timeline label adapts to checked modes
15. All inputs have accessible labels
16. Form is keyboard-navigable
17. Does not call `onSubmit` a second time while first call is in flight
18. Does not crash if `onSubmit` throws — resets submitting state
19. Shows `error` prop value when provided
20. Clears internal submitting state when `onSubmit` rejects

Mock `useGoogleMapsAutocomplete` to return `{ loaded: true }`.

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd packages/ui && npx vitest run --reporter=verbose
```

Expected: All tests FAIL (LeadForm component doesn't exist yet)

- [ ] **Step 3: Commit failing tests**

```bash
git add packages/ui/LeadForm/LeadForm.test.tsx
git commit -m "test: add LeadForm component tests (red phase)"
```

---

### Task 7: Implement LeadForm component (TDD — green phase)

**Files:**
- Create: `packages/ui/LeadForm/LeadForm.tsx`

- [ ] **Step 1: Implement the full component**

Key implementation details:
- `"use client"` directive
- Pill checkboxes with `--color-accent` (buyer) and `--color-secondary` (seller) borders
- Buyer card: gold-tinted background (`#FFFDE7`), border `#F0E68C`
- Seller card: green-tinted background (`#E8F5E9`), border `#A5D6A7`
- Card show/hide: `max-height: 800px` / `0` + `opacity` transition (300ms ease), `overflow: hidden`
- Inline styles with CSS variable fallbacks
- `<style>` tag for responsive `@media (max-width: 768px)` overrides, scoped with `.lead-form-` prefix
- Internal `submitting` state for double-submit prevention
- `onSubmit` wrapped in try/catch to reset submitting on error
- Timeline label: adapts based on checked modes
- Google Maps autocomplete via `useGoogleMapsAutocomplete` hook, `enabled` when selling is checked
- Form card padding: `clamp(20px, 5vw, 40px)` desktop, `clamp(16px, 4vw, 28px)` mobile
- Match existing input styles: `border: 2px solid #e0e0e0`, `borderRadius: 8px`, `padding: 12px 16px`, `fontSize: 15px`
- Submit button: `borderRadius: 30px`, hover with `translateY(-2px)` + box shadow

- [ ] **Step 2: Run tests**

```bash
cd packages/ui && npx vitest run --reporter=verbose
```

Expected: All tests PASS

- [ ] **Step 3: Run coverage**

```bash
cd packages/ui && npx vitest run --coverage
```

Expected: 100% branch coverage

- [ ] **Step 4: Commit**

```bash
git add packages/ui/LeadForm/LeadForm.tsx packages/ui/LeadForm/index.ts packages/ui/index.ts
git commit -m "feat: implement LeadForm component with buyer/seller modes"
```

---

## Chunk 5: Agent-Site Integration

### Task 8: Wire LeadForm into agent-site

**Files:**
- Modify: `apps/agent-site/package.json`
- Modify: `apps/agent-site/next.config.ts`
- Modify: `apps/agent-site/components/sections/CmaForm.tsx`

- [ ] **Step 1: Add workspace dependencies to agent-site**

Add to `apps/agent-site/package.json` dependencies:
```json
"@real-estate-star/ui": "*",
"@real-estate-star/shared-types": "*"
```

Run: `npm install` (from root)

- [ ] **Step 2: Add transpilePackages to next.config.ts**

Add `transpilePackages: ["@real-estate-star/ui", "@real-estate-star/shared-types"]` to the `nextConfig` object.

- [ ] **Step 3: Refactor CmaForm.tsx to thin wrapper**

The wrapper:
- Keeps the section outer div with `id="cma-form"`, gradient background, title/subtitle/description
- Imports `<LeadForm>` from `@real-estate-star/ui`
- Passes props: `defaultState`, `googleMapsApiKey` (from env var `NEXT_PUBLIC_GOOGLE_MAPS_API_KEY`), `onSubmit`, `initialMode={["selling"]}`, `error`
- The `onSubmit` handler bridges `LeadFormData` → existing submission flows:
  - Maps `seller` fields to the existing formspree/API request shape
  - Calls `useCmaSubmit().submit()` for API mode
  - Calls formspree fetch for formspree mode
- Keeps `ProgressTracker` sub-component
- Keeps analytics tracking, Sentry error reporting
- Keeps the `isApiMode` / `isProcessing` logic for showing progress vs form

- [ ] **Step 4: Run existing agent-site tests**

```bash
cd apps/agent-site && npm test
```

Fix any broken tests due to the refactored CmaForm structure.

- [ ] **Step 5: Update CmaForm.test.tsx for new wrapper structure**

Update tests to account for the new wrapper + `<LeadForm>` structure. Add:
- Legacy parity test: seller-only mode produces same fields/labels
- Wrapper passes correct props to LeadForm

- [ ] **Step 6: Run coverage**

```bash
cd apps/agent-site && npm run test:coverage
```

Expected: 100% branch coverage

- [ ] **Step 7: Verify build works**

```bash
cd apps/agent-site && npm run build
```

Expected: Build succeeds with no errors

- [ ] **Step 8: Commit**

```bash
git add apps/agent-site/package.json apps/agent-site/next.config.ts apps/agent-site/components/sections/CmaForm.tsx apps/agent-site/__tests__/components/CmaForm.test.tsx package-lock.json
git commit -m "refactor: wire shared LeadForm into agent-site CmaForm wrapper"
```

---

## Chunk 6: Visual Regression — After Screenshots & CI

### Task 9: Capture after-migration screenshots

**Files:**
- Create: `docs/superpowers/screenshots/lead-form-migration/after/desktop.png`
- Create: `docs/superpowers/screenshots/lead-form-migration/after/mobile.png`

- [ ] **Step 1: Start agent-site dev server**

```bash
cd apps/agent-site && npm run dev &
```

- [ ] **Step 2: Capture desktop screenshot (1440x900)**

Navigate to `http://localhost:3000/jenise-buckalew#cma-form` at 1440x900.

Save to: `docs/superpowers/screenshots/lead-form-migration/after/desktop.png`

- [ ] **Step 3: Capture mobile screenshot (375x812)**

Same URL at 375x812.

Save to: `docs/superpowers/screenshots/lead-form-migration/after/mobile.png`

- [ ] **Step 4: Compare with before screenshots**

Open before and after screenshots side by side. The seller-only form card (with "I'm Selling" pre-checked) should match the original:
- Same field labels, placeholders, order
- Same input styling
- Same form card shape and shadow
- Same responsive layout at mobile width

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/screenshots/lead-form-migration/after/
git commit -m "docs: capture after-migration screenshots for visual comparison"
```

---

### Task 10: Update CI workflow for workspace installs

**Files:**
- Modify: `.github/workflows/agent-site.yml`

- [ ] **Step 1: Update CI to use workspace-aware install**

The CI currently uses `npm ci --prefix apps/agent-site`. With npm workspaces, dependencies from `packages/*` are resolved via the root `package-lock.json`. Update the workflow:

- Change `npm ci --prefix apps/agent-site` → `npm ci` (root install, resolves all workspace links)
- Change `cache-dependency-path` → `package-lock.json` (root lockfile)
- Keep `--prefix apps/agent-site` for lint/test/build commands (they still run per-app)
- Add `packages/**` to the paths trigger so changes to shared packages trigger CI

Apply same changes to `deploy-agent-site.yml`.

- [ ] **Step 2: Verify CI config is valid**

Review the updated YAML for syntax issues.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/agent-site.yml .github/workflows/deploy-agent-site.yml
git commit -m "ci: update agent-site workflows for npm workspace installs"
```

---

### Task 11: Add platform workspace deps (future-proofing)

**Files:**
- Modify: `apps/platform/package.json`
- Modify: `.github/workflows/platform.yml`
- Modify: `.github/workflows/deploy-platform.yml`

- [ ] **Step 1: Add workspace deps to platform package.json**

Add to dependencies:
```json
"@real-estate-star/ui": "*",
"@real-estate-star/shared-types": "*"
```

- [ ] **Step 2: Update platform CI workflows for workspace install**

Same pattern as agent-site: `npm ci` at root, `--prefix apps/platform` for commands.

- [ ] **Step 3: Run npm install, verify platform build**

```bash
npm install
cd apps/platform && npm run build
```

- [ ] **Step 4: Commit**

```bash
git add apps/platform/package.json .github/workflows/platform.yml .github/workflows/deploy-platform.yml package-lock.json
git commit -m "chore: add shared package deps to platform, update CI for workspaces"
```
