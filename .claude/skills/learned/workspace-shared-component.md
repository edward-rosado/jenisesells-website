---
name: workspace-shared-component
description: "Extract inline components into packages/ui with npm workspaces, transpilePackages, and test migration"
user-invocable: false
origin: auto-extracted
---

# Workspace Shared Component Extraction

**Extracted:** 2026-03-15
**Context:** Extracting an inline React component from an app into `packages/ui/` for reuse across multiple Next.js apps in the monorepo.

## Problem
Inline components in `apps/agent-site/` or `apps/platform/` can't be shared. Extracting to a shared package requires workspace wiring, build config, and test migration — each with non-obvious pitfalls.

## Solution

### 1. Package Setup (`packages/ui/`)
- React/Next as **peerDependencies** (avoid two-React errors) AND **devDependencies** (for testing)
- `"main": "index.ts"` — point at TypeScript source, not compiled output
- Depend on `@real-estate-star/shared-types: "*"` for workspace linking

### 2. Consumer App Config
Add to each consuming app's `next.config.ts`:
```ts
transpilePackages: ["@real-estate-star/ui", "@real-estate-star/shared-types"]
```
Without this, Next.js won't compile the TypeScript source from workspace packages.

Add to `package.json` dependencies:
```json
"@real-estate-star/ui": "*",
"@real-estate-star/shared-types": "*"
```

### 3. CI Workflow Updates
When switching to workspaces, ALL CI workflows must change:
- `npm ci --prefix apps/foo` -> `npm ci` (root install)
- `cache-dependency-path: apps/foo/package-lock.json` -> `package-lock.json`
- Add `'packages/**'` to path triggers
- Keep `--prefix apps/foo` for lint/test/build commands

### 4. Test Migration Pitfalls
- Tests using `getByPlaceholderText("John")` break when the shared component uses different placeholders — switch to `getByLabelText("First Name")` which is more stable
- Mock the shared component's internal hooks in consumer tests:
  ```ts
  vi.mock("@real-estate-star/ui/LeadForm/useGoogleMapsAutocomplete", () => ({
    useGoogleMapsAutocomplete: () => ({ loaded: false }),
  }));
  ```
- Vitest 4 requires `afterEach(cleanup)` in setup file — without it, component state leaks between tests

### 5. Type Widening for Shared Components
When `transpilePackages` type-checks shared source against consumer usage, you may need to widen ref types:
```ts
// Breaks when <select> passes ref typed for HTMLInputElement
ref: React.Ref<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>
```

## When to Use
- Extracting any component from `apps/*` into `packages/ui/`
- Adding a new shared package to the workspace
- Updating CI after adding workspace packages
