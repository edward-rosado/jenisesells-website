---
name: agent-site-component-creation
description: "Patterns for creating new agent-site section components with tests, exports, and template integration"
user-invocable: false
origin: auto-extracted
---

# Agent Site Component Creation

**Extracted:** 2026-03-19
**Context:** Creating new section components for the agent-site multi-template system (10 templates, 76+ section variants)

## Component Structure

### File Location
- Section components: `apps/agent-site/components/sections/{category}/{ComponentName}.tsx`
- Categories: `heroes/`, `stats/`, `services/`, `steps/`, `sold/`, `testimonials/`, `about/`, `marquee/`, `profiles/`, `shared/`

### Required Patterns
1. **`"use client"` directive** — required on ALL components that use hooks (useState, useEffect, useCallback, useRef, custom hooks)
2. **Inline styles only** — no CSS modules or Tailwind. Use `style={{}}` props. This is critical for the white-label system.
3. **CSS `@keyframes` via `<style>` JSX** — when animations are needed, embed `<style>` tags in JSX (same pattern as Nav.tsx)
4. **Brand color variables** — use `var(--color-primary, #fallback)` and `var(--font-family, inherit)` for theming
5. **Props interface** — import types from `components/sections/types.ts` or define inline. Match the `PageSections` data shape.

### Hooks Rules
- **Never call hooks inside `.map()`** — this violates Rules of Hooks
- **Stagger pattern** — use ONE `useScrollReveal` on parent, then `transitionDelay: ${i * 100}ms` per child item
- **`useSyncExternalStore`** for browser APIs (matchMedia) — SSR-safe with server snapshot returning `false`
- **`useState(initialValue)` over `useEffect` + `setState`** — React 19's `react-hooks/set-state-in-effect` lint rule flags synchronous setState in useEffect body. Initialize state directly instead.

## Registration

### 1. Export from barrel
Add to `apps/agent-site/components/sections/index.ts`:
```typescript
// {Category} variants
export { ComponentName } from "./{category}/ComponentName";
```

### 2. Add to template
Templates at `apps/agent-site/templates/{template-name}.tsx` import from `@/components/sections` and conditionally render based on `s.{sectionKey}?.enabled`.

### 3. Update types (if new section type)
If adding a new section category (like `marquee`), update:
- `apps/agent-site/lib/types.ts` — add data interface and `SectionConfig<DataType>` to `PageSections`
- `apps/agent-site/components/sections/types.ts` — add props interface if shared

## Testing

### File Location
Test files: `apps/agent-site/__tests__/components/{category}/{ComponentName}.test.tsx`

### Test File Template
```typescript
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ComponentName } from "@/components/sections/{category}/ComponentName";

// Mock hooks used by the component
vi.mock("@/hooks/useReducedMotion", () => ({ useReducedMotion: vi.fn(() => false) }));
vi.mock("@/hooks/useScrollReveal", () => ({ useScrollReveal: vi.fn(() => true) }));
vi.mock("@/hooks/useParallax", () => ({ useParallax: vi.fn() }));
```

### Critical Testing Lessons (learned the hard way)

1. **Mock ALL hooks the component uses** — even indirect ones. `ScrollRevealSection` uses `useScrollReveal` which uses `useReducedMotion` which calls `window.matchMedia`. If you miss one, you get `window.matchMedia is not a function` in jsdom.

2. **`vi.resetAllMocks()` not `vi.restoreAllMocks()`** — `restoreAllMocks` only reverts `vi.spyOn()` spies, NOT `mockReturnValue` overrides on `vi.fn()` mocks. If test A sets `mockReturnValue(true)`, test B still sees `true` with restoreAllMocks. Use `resetAllMocks()` in `afterEach`.

3. **Watch for text collisions with FTC disclaimer** — `FTC_DISCLAIMER` text contains words like "Zillow", "Trulia", etc. If your component renders these words AND includes the disclaimer, `getByText(/Zillow/)` matches multiple elements. Use `getAllByText` or more specific selectors.

4. **Check existing import patterns before writing tests** — template tests use relative imports `"../components/fixtures"`, NOT `"@/tests/components/fixtures"`. Always grep existing test files first.

5. **Cover ALL conditional branches for 100% coverage** — this project enforces 100% branch coverage. Common misses:
   - Ternary expressions with dark/light mode: `isDark ? "#fff" : "#000"` — test BOTH paths
   - Optional chaining: `item.category && (...)` — test with AND without category
   - Visibility states: `isVisible ? 1 : 0` — mock `useScrollReveal` returning both `true` and `false`
   - Guard clauses: `if (items.length === 0) return null` — test with empty array

6. **Template tests need hook mocks too** — when you add a `"use client"` wrapper like `ScrollRevealSection` to templates, ALL template test files need the hook mocks added. This isn't obvious because the template itself doesn't import hooks — the wrapper does.

7. **No `any` types in tests** — ESLint enforces `@typescript-eslint/no-explicit-any`. Use `Record<string, unknown>` or proper types instead.

## Template Integration Checklist

When adding a new component to templates:
- [ ] Component file with `"use client"` (if hooks used)
- [ ] Export added to `components/sections/index.ts`
- [ ] Types updated in `lib/types.ts` (if new section type)
- [ ] Component test with 100% branch coverage
- [ ] Template file updated to import and render component
- [ ] Template test updated with hook mocks (if component uses hooks)
- [ ] Test fixtures updated if new data shape needed (`__tests__/components/fixtures.ts`)
- [ ] Content JSON files updated for test accounts (if new section type)

## Common Gotchas

- **`getEnabledSections()`** dynamically iterates `Object.entries(sections)` — adding a new key to `PageSections` is sufficient for nav integration
- **Identity resolution cascade** — templates use `agent ?? account.agent ?? { id: account.handle, ... }` fallback. Test all 3 paths.
- **AboutParallax vs other About variants** — AboutParallax renders just `{name}`, NOT `"About {name}"`. Check heading text carefully in tests.
- **ServicesPremium is NOT wrapped in ScrollRevealSection** — it has its own internal scroll-reveal per child block
