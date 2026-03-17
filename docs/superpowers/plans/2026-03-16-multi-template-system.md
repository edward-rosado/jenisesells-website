# Multi-Template System Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform the agent-site from a single-template app into a 3-template system (Emerald Classic, Modern Minimal, Warm Community) using section variants.

**Architecture:** Each section type (Hero, Stats, Services, etc.) gets a variant folder with one component per template. Templates compose their chosen variants. Shared components (Nav, CmaForm, Footer) stay unchanged. A shared `types.ts` defines props interfaces imported by all variants.

**Tech Stack:** Next.js 16, React, TypeScript, Vitest, @testing-library/react

**Spec:** `docs/superpowers/specs/2026-03-16-multi-template-system-design.md`

**Responsive & ADA Requirements:** All new variant components MUST match the Emerald Classic template's responsive and accessibility patterns:

- **Responsive (mobile / iPad / desktop):** Use `flexWrap: "wrap"`, `gridTemplateColumns: "repeat(auto-fit, minmax(Xpx, 1fr))"`, and flexible padding. No media queries needed — the existing codebase uses inline styles with CSS flex/grid natural breakpoints. All layouts must degrade gracefully from desktop → tablet → mobile.
- **ADA Accessibility:** Every section must use semantic HTML (`<section>`, `<article>`, `<nav>`, `<h2>`, `<dl>`). Include `aria-label` on interactive/landmark elements, `alt` text on all images, `role="img"` with `aria-label` on star ratings, and proper heading hierarchy (h1 for hero, h2 for section titles, h3 for cards). Links must have visible focus states.
- **Legal Compliance:** Footer, CmaForm, and Nav are shared (identical across templates) so legal items (Equal Housing, license, disclaimers, legal links) are automatic. For variant sections: every Testimonials variant MUST include the FTC disclaimer ("No compensation was provided..."), every Stats variant MUST render `sourceDisclaimer` when provided, every Sold variant MUST show SOLD badge/label.

---

## Chunk 1: Restructure Existing Sections

Phase 1 moves existing section components into variant folders. Zero behavior change — Emerald Classic renders identically before and after.

### Task 1: Create shared section props types file

**Files:**
- Create: `apps/agent-site/components/sections/types.ts`

- [ ] **Step 1: Create the shared props types file**

```typescript
// apps/agent-site/components/sections/types.ts
import type {
  HeroData,
  StatItem,
  ServiceItem,
  StepItem,
  SoldHomeItem,
  TestimonialItem,
  AgentConfig,
  AboutData,
} from "@/lib/types";

export interface HeroProps {
  data: HeroData;
  agentPhotoUrl?: string;
  agentName?: string;
}

export interface StatsProps {
  items: StatItem[];
  sourceDisclaimer?: string;
}

export interface ServicesProps {
  items: ServiceItem[];
  title?: string;
  subtitle?: string;
}

export interface StepsProps {
  steps: StepItem[];
  title?: string;
  subtitle?: string;
}

export interface SoldHomesProps {
  items: SoldHomeItem[];
  title?: string;
  subtitle?: string;
}

export interface TestimonialsProps {
  items: TestimonialItem[];
  title?: string;
}

export interface AboutProps {
  agent: AgentConfig;
  data: AboutData;
}
```

- [ ] **Step 2: Verify TypeScript compiles**

Run: `cd apps/agent-site && npx tsc --noEmit`
Expected: No new errors (existing errors are fine)

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/components/sections/types.ts
git commit -m "refactor: add shared section props types file"
```

---

### Task 2: Move shared sections (CmaForm, Footer)

**Files:**
- Move: `apps/agent-site/components/sections/CmaForm.tsx` → `apps/agent-site/components/sections/shared/CmaForm.tsx`
- Move: `apps/agent-site/components/sections/Footer.tsx` → `apps/agent-site/components/sections/shared/Footer.tsx`
- Create: `apps/agent-site/components/sections/shared/index.ts`
- Move: `apps/agent-site/__tests__/components/CmaForm.test.tsx` → `apps/agent-site/__tests__/components/shared/CmaForm.test.tsx`
- Move: `apps/agent-site/__tests__/components/Footer.test.tsx` → `apps/agent-site/__tests__/components/shared/Footer.test.tsx`

- [ ] **Step 1: Create shared directory and move files**

```bash
cd apps/agent-site
mkdir -p components/sections/shared
git mv components/sections/CmaForm.tsx components/sections/shared/CmaForm.tsx
git mv components/sections/Footer.tsx components/sections/shared/Footer.tsx
```

- [ ] **Step 2: Create shared barrel export**

```typescript
// apps/agent-site/components/sections/shared/index.ts
export { CmaForm } from "./CmaForm";
export { Footer } from "./Footer";
```

- [ ] **Step 3: Move test files**

```bash
cd apps/agent-site
mkdir -p __tests__/components/shared
git mv __tests__/components/CmaForm.test.tsx __tests__/components/shared/CmaForm.test.tsx
git mv __tests__/components/Footer.test.tsx __tests__/components/shared/Footer.test.tsx
```

- [ ] **Step 4: Update test imports**

In `__tests__/components/shared/CmaForm.test.tsx`, change:
```typescript
// Old
import { CmaForm } from "@/components/sections/CmaForm";
// New
import { CmaForm } from "@/components/sections/shared/CmaForm";
```

In `__tests__/components/shared/Footer.test.tsx`, change:
```typescript
// Old
import { Footer } from "@/components/sections/Footer";
// New
import { Footer } from "@/components/sections/shared/Footer";
```

- [ ] **Step 5: Run tests to verify nothing broke**

Run: `cd apps/agent-site && npx vitest run __tests__/components/shared/`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add -A apps/agent-site/components/sections/shared/ apps/agent-site/__tests__/components/shared/
git add apps/agent-site/components/sections/CmaForm.tsx apps/agent-site/components/sections/Footer.tsx
git add apps/agent-site/__tests__/components/CmaForm.test.tsx apps/agent-site/__tests__/components/Footer.test.tsx
git commit -m "refactor: move CmaForm and Footer to sections/shared/"
```

---

### Task 3: Move Hero to heroes/ folder

**Files:**
- Move: `apps/agent-site/components/sections/Hero.tsx` → `apps/agent-site/components/sections/heroes/HeroGradient.tsx`
- Create: `apps/agent-site/components/sections/heroes/index.ts`
- Move: `apps/agent-site/__tests__/components/Hero.test.tsx` → `apps/agent-site/__tests__/components/heroes/HeroGradient.test.tsx`

- [ ] **Step 1: Create heroes directory and move file**

```bash
cd apps/agent-site
mkdir -p components/sections/heroes
git mv components/sections/Hero.tsx components/sections/heroes/HeroGradient.tsx
```

- [ ] **Step 2: Rename the exported component**

In `components/sections/heroes/HeroGradient.tsx`, change:
```typescript
// Old
export function Hero({ data, agentPhotoUrl, agentName }: HeroProps) {
// New (import from shared types)
import type { HeroProps } from "@/components/sections/types";
export function HeroGradient({ data, agentPhotoUrl, agentName }: HeroProps) {
```

Also remove the local `HeroProps` interface definition since it now comes from the shared types file. Keep the `HeroData` import from `@/lib/types` for the `renderHeadline` helper (it doesn't use it directly, but ensure the import of `HeroProps` from types.ts brings what's needed).

The file should:
- Remove `import type { HeroData } from "@/lib/types";` (no longer needed directly — HeroProps references HeroData)
- Remove the local `interface HeroProps { ... }`
- Add `import type { HeroProps } from "@/components/sections/types";`
- Rename `export function Hero(` to `export function HeroGradient(`

- [ ] **Step 3: Create heroes barrel export**

```typescript
// apps/agent-site/components/sections/heroes/index.ts
export { HeroGradient } from "./HeroGradient";
```

- [ ] **Step 4: Move and update test file**

```bash
cd apps/agent-site
mkdir -p __tests__/components/heroes
git mv __tests__/components/Hero.test.tsx __tests__/components/heroes/HeroGradient.test.tsx
```

In `__tests__/components/heroes/HeroGradient.test.tsx`, change:
```typescript
// Old
import { Hero } from "@/components/sections/Hero";
// New
import { HeroGradient } from "@/components/sections/heroes/HeroGradient";
```

Replace all `<Hero` with `<HeroGradient` and update the describe block name:
```typescript
// Old
describe("Hero", () => {
// New
describe("HeroGradient", () => {
```

- [ ] **Step 5: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run __tests__/components/heroes/`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add -A apps/agent-site/components/sections/heroes/ apps/agent-site/__tests__/components/heroes/
git add apps/agent-site/components/sections/Hero.tsx apps/agent-site/__tests__/components/Hero.test.tsx
git commit -m "refactor: move Hero to heroes/HeroGradient"
```

---

### Task 4: Move StatsBar to stats/ folder

**Files:**
- Move: `apps/agent-site/components/sections/StatsBar.tsx` → `apps/agent-site/components/sections/stats/StatsBar.tsx`
- Create: `apps/agent-site/components/sections/stats/index.ts`
- Move: `apps/agent-site/__tests__/components/StatsBar.test.tsx` → `apps/agent-site/__tests__/components/stats/StatsBar.test.tsx`

- [ ] **Step 1: Create stats directory and move file**

```bash
cd apps/agent-site
mkdir -p components/sections/stats
git mv components/sections/StatsBar.tsx components/sections/stats/StatsBar.tsx
```

- [ ] **Step 2: Update to use shared types**

In `components/sections/stats/StatsBar.tsx`, change:
```typescript
// Old
import type { StatItem } from "@/lib/types";
interface StatsBarProps {
  items: StatItem[];
  sourceDisclaimer?: string;
}
export function StatsBar({ items, sourceDisclaimer }: StatsBarProps) {
// New
import type { StatsProps } from "@/components/sections/types";
export function StatsBar({ items, sourceDisclaimer }: StatsProps) {
```

Remove the local `StatsBarProps` interface and the `StatItem` import.

- [ ] **Step 3: Create stats barrel export**

```typescript
// apps/agent-site/components/sections/stats/index.ts
export { StatsBar } from "./StatsBar";
```

- [ ] **Step 4: Move and update test file**

```bash
cd apps/agent-site
mkdir -p __tests__/components/stats
git mv __tests__/components/StatsBar.test.tsx __tests__/components/stats/StatsBar.test.tsx
```

In `__tests__/components/stats/StatsBar.test.tsx`, change the import:
```typescript
// Old
import { StatsBar } from "@/components/sections/StatsBar";
// New
import { StatsBar } from "@/components/sections/stats/StatsBar";
```

- [ ] **Step 5: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run __tests__/components/stats/`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add -A apps/agent-site/components/sections/stats/ apps/agent-site/__tests__/components/stats/
git add apps/agent-site/components/sections/StatsBar.tsx apps/agent-site/__tests__/components/StatsBar.test.tsx
git commit -m "refactor: move StatsBar to stats/"
```

---

### Task 5: Move Services to services/ folder

**Files:**
- Move: `apps/agent-site/components/sections/Services.tsx` → `apps/agent-site/components/sections/services/ServicesGrid.tsx`
- Create: `apps/agent-site/components/sections/services/index.ts`
- Move: `apps/agent-site/__tests__/components/Services.test.tsx` → `apps/agent-site/__tests__/components/services/ServicesGrid.test.tsx`

- [ ] **Step 1: Create services directory and move file**

```bash
cd apps/agent-site
mkdir -p components/sections/services
git mv components/sections/Services.tsx components/sections/services/ServicesGrid.tsx
```

- [ ] **Step 2: Update to use shared types and rename**

In `components/sections/services/ServicesGrid.tsx`, change:
```typescript
// Old
import type { ServiceItem } from "@/lib/types";
interface ServicesProps { ... }
export function Services({ items, title, subtitle }: ServicesProps) {
// New
import type { ServicesProps } from "@/components/sections/types";
export function ServicesGrid({ items, title, subtitle }: ServicesProps) {
```

Remove the local `ServicesProps` interface and the `ServiceItem` import.

- [ ] **Step 3: Create services barrel export**

```typescript
// apps/agent-site/components/sections/services/index.ts
export { ServicesGrid } from "./ServicesGrid";
```

- [ ] **Step 4: Move and update test file**

```bash
cd apps/agent-site
mkdir -p __tests__/components/services
git mv __tests__/components/Services.test.tsx __tests__/components/services/ServicesGrid.test.tsx
```

Update imports and component references:
```typescript
// Old
import { Services } from "@/components/sections/Services";
describe("Services", () => {
// New
import { ServicesGrid } from "@/components/sections/services/ServicesGrid";
describe("ServicesGrid", () => {
```

Replace all `<Services` with `<ServicesGrid` in the test file.

- [ ] **Step 5: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run __tests__/components/services/`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add -A apps/agent-site/components/sections/services/ apps/agent-site/__tests__/components/services/
git add apps/agent-site/components/sections/Services.tsx apps/agent-site/__tests__/components/Services.test.tsx
git commit -m "refactor: move Services to services/ServicesGrid"
```

---

### Task 6: Move HowItWorks to steps/ folder

**Files:**
- Move: `apps/agent-site/components/sections/HowItWorks.tsx` → `apps/agent-site/components/sections/steps/StepsNumbered.tsx`
- Create: `apps/agent-site/components/sections/steps/index.ts`
- Move: `apps/agent-site/__tests__/components/HowItWorks.test.tsx` → `apps/agent-site/__tests__/components/steps/StepsNumbered.test.tsx`

- [ ] **Step 1: Create steps directory and move file**

```bash
cd apps/agent-site
mkdir -p components/sections/steps
git mv components/sections/HowItWorks.tsx components/sections/steps/StepsNumbered.tsx
```

- [ ] **Step 2: Update to use shared types and rename**

In `components/sections/steps/StepsNumbered.tsx`, change:
```typescript
// Old
import type { StepItem } from "@/lib/types";
interface HowItWorksProps { ... }
export function HowItWorks({ steps, title, subtitle }: HowItWorksProps) {
// New
import type { StepsProps } from "@/components/sections/types";
export function StepsNumbered({ steps, title, subtitle }: StepsProps) {
```

Remove the local `HowItWorksProps` interface and the `StepItem` import.

- [ ] **Step 3: Create steps barrel export**

```typescript
// apps/agent-site/components/sections/steps/index.ts
export { StepsNumbered } from "./StepsNumbered";
```

- [ ] **Step 4: Move and update test file**

```bash
cd apps/agent-site
mkdir -p __tests__/components/steps
git mv __tests__/components/HowItWorks.test.tsx __tests__/components/steps/StepsNumbered.test.tsx
```

Update imports and component references:
```typescript
// Old
import { HowItWorks } from "@/components/sections/HowItWorks";
describe("HowItWorks", () => {
// New
import { StepsNumbered } from "@/components/sections/steps/StepsNumbered";
describe("StepsNumbered", () => {
```

Replace all `<HowItWorks` with `<StepsNumbered` in the test file.

- [ ] **Step 5: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run __tests__/components/steps/`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add -A apps/agent-site/components/sections/steps/ apps/agent-site/__tests__/components/steps/
git add apps/agent-site/components/sections/HowItWorks.tsx apps/agent-site/__tests__/components/HowItWorks.test.tsx
git commit -m "refactor: move HowItWorks to steps/StepsNumbered"
```

---

### Task 7: Move SoldHomes to sold/ folder

**Files:**
- Move: `apps/agent-site/components/sections/SoldHomes.tsx` → `apps/agent-site/components/sections/sold/SoldGrid.tsx`
- Create: `apps/agent-site/components/sections/sold/index.ts`
- Move: `apps/agent-site/__tests__/components/SoldHomes.test.tsx` → `apps/agent-site/__tests__/components/sold/SoldGrid.test.tsx`

- [ ] **Step 1: Create sold directory and move file**

```bash
cd apps/agent-site
mkdir -p components/sections/sold
git mv components/sections/SoldHomes.tsx components/sections/sold/SoldGrid.tsx
```

- [ ] **Step 2: Update to use shared types and rename**

In `components/sections/sold/SoldGrid.tsx`, change:
```typescript
// Old
import type { SoldHomeItem } from "@/lib/types";
interface SoldHomesProps { ... }
export function SoldHomes({ items, title, subtitle }: SoldHomesProps) {
// New
import type { SoldHomesProps } from "@/components/sections/types";
export function SoldGrid({ items, title, subtitle }: SoldHomesProps) {
```

Remove the local `SoldHomesProps` interface and the `SoldHomeItem` import.

- [ ] **Step 3: Create sold barrel export**

```typescript
// apps/agent-site/components/sections/sold/index.ts
export { SoldGrid } from "./SoldGrid";
```

- [ ] **Step 4: Move and update test file**

```bash
cd apps/agent-site
mkdir -p __tests__/components/sold
git mv __tests__/components/SoldHomes.test.tsx __tests__/components/sold/SoldGrid.test.tsx
```

Update imports and component references:
```typescript
// Old
import { SoldHomes } from "@/components/sections/SoldHomes";
describe("SoldHomes", () => {
// New
import { SoldGrid } from "@/components/sections/sold/SoldGrid";
describe("SoldGrid", () => {
```

Replace all `<SoldHomes` with `<SoldGrid` in the test file.

- [ ] **Step 5: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run __tests__/components/sold/`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add -A apps/agent-site/components/sections/sold/ apps/agent-site/__tests__/components/sold/
git add apps/agent-site/components/sections/SoldHomes.tsx apps/agent-site/__tests__/components/SoldHomes.test.tsx
git commit -m "refactor: move SoldHomes to sold/SoldGrid"
```

---

### Task 8: Move Testimonials to testimonials/ folder

**Files:**
- Move: `apps/agent-site/components/sections/Testimonials.tsx` → `apps/agent-site/components/sections/testimonials/TestimonialsGrid.tsx`
- Create: `apps/agent-site/components/sections/testimonials/index.ts`
- Move: `apps/agent-site/__tests__/components/Testimonials.test.tsx` → `apps/agent-site/__tests__/components/testimonials/TestimonialsGrid.test.tsx`

- [ ] **Step 1: Create testimonials directory and move file**

```bash
cd apps/agent-site
mkdir -p components/sections/testimonials
git mv components/sections/Testimonials.tsx components/sections/testimonials/TestimonialsGrid.tsx
```

- [ ] **Step 2: Update to use shared types and rename**

In `components/sections/testimonials/TestimonialsGrid.tsx`, change:
```typescript
// Old
import type { TestimonialItem } from "@/lib/types";
interface TestimonialsProps { ... }
export function Testimonials({ items, title }: TestimonialsProps) {
// New
import type { TestimonialsProps } from "@/components/sections/types";
export function TestimonialsGrid({ items, title }: TestimonialsProps) {
```

Remove the local `TestimonialsProps` interface and the `TestimonialItem` import.

- [ ] **Step 3: Create testimonials barrel export**

```typescript
// apps/agent-site/components/sections/testimonials/index.ts
export { TestimonialsGrid } from "./TestimonialsGrid";
```

- [ ] **Step 4: Move and update test file**

```bash
cd apps/agent-site
mkdir -p __tests__/components/testimonials
git mv __tests__/components/Testimonials.test.tsx __tests__/components/testimonials/TestimonialsGrid.test.tsx
```

Update imports and component references:
```typescript
// Old
import { Testimonials } from "@/components/sections/Testimonials";
describe("Testimonials", () => {
// New
import { TestimonialsGrid } from "@/components/sections/testimonials/TestimonialsGrid";
describe("TestimonialsGrid", () => {
```

Replace all `<Testimonials` with `<TestimonialsGrid` in the test file.

- [ ] **Step 5: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run __tests__/components/testimonials/`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add -A apps/agent-site/components/sections/testimonials/ apps/agent-site/__tests__/components/testimonials/
git add apps/agent-site/components/sections/Testimonials.tsx apps/agent-site/__tests__/components/Testimonials.test.tsx
git commit -m "refactor: move Testimonials to testimonials/TestimonialsGrid"
```

---

### Task 9: Move About to about/ folder

**Files:**
- Move: `apps/agent-site/components/sections/About.tsx` → `apps/agent-site/components/sections/about/AboutSplit.tsx`
- Create: `apps/agent-site/components/sections/about/index.ts`
- Move: `apps/agent-site/__tests__/components/About.test.tsx` → `apps/agent-site/__tests__/components/about/AboutSplit.test.tsx`

- [ ] **Step 1: Create about directory and move file**

```bash
cd apps/agent-site
mkdir -p components/sections/about
git mv components/sections/About.tsx components/sections/about/AboutSplit.tsx
```

- [ ] **Step 2: Update to use shared types and rename**

In `components/sections/about/AboutSplit.tsx`, change:
```typescript
// Old
import type { AgentConfig, AboutData } from "@/lib/types";
interface AboutProps { ... }
export function About({ agent, data }: AboutProps) {
// New
import type { AboutProps } from "@/components/sections/types";
export function AboutSplit({ agent, data }: AboutProps) {
```

Remove the local `AboutProps` interface and the `AgentConfig, AboutData` import.

- [ ] **Step 3: Create about barrel export**

```typescript
// apps/agent-site/components/sections/about/index.ts
export { AboutSplit } from "./AboutSplit";
```

- [ ] **Step 4: Move and update test file**

```bash
cd apps/agent-site
mkdir -p __tests__/components/about
git mv __tests__/components/About.test.tsx __tests__/components/about/AboutSplit.test.tsx
```

Update imports and component references:
```typescript
// Old
import { About } from "@/components/sections/About";
describe("About", () => {
// New
import { AboutSplit } from "@/components/sections/about/AboutSplit";
describe("AboutSplit", () => {
```

Replace all `<About` with `<AboutSplit` in the test file.

- [ ] **Step 5: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run __tests__/components/about/`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add -A apps/agent-site/components/sections/about/ apps/agent-site/__tests__/components/about/
git add apps/agent-site/components/sections/About.tsx apps/agent-site/__tests__/components/About.test.tsx
git commit -m "refactor: move About to about/AboutSplit"
```

---

### Task 10: Update barrel exports and EmeraldClassic imports

**Files:**
- Modify: `apps/agent-site/components/sections/index.ts`
- Modify: `apps/agent-site/templates/emerald-classic.tsx`

- [ ] **Step 1: Replace sections barrel export**

Replace the entire content of `apps/agent-site/components/sections/index.ts`:

```typescript
// Shared sections (same across all templates)
export { CmaForm } from "./shared/CmaForm";
export { Footer } from "./shared/Footer";

// Section variants — re-export by type
export { HeroGradient } from "./heroes/HeroGradient";
export { StatsBar } from "./stats/StatsBar";
export { ServicesGrid } from "./services/ServicesGrid";
export { StepsNumbered } from "./steps/StepsNumbered";
export { SoldGrid } from "./sold/SoldGrid";
export { TestimonialsGrid } from "./testimonials/TestimonialsGrid";
export { AboutSplit } from "./about/AboutSplit";

// Types
export type {
  HeroProps,
  StatsProps,
  ServicesProps,
  StepsProps,
  SoldHomesProps,
  TestimonialsProps,
  AboutProps,
} from "./types";
```

- [ ] **Step 2: Update EmeraldClassic template imports**

In `apps/agent-site/templates/emerald-classic.tsx`, change imports:

```typescript
// Old
import { Hero, StatsBar, Services, HowItWorks, SoldHomes, Testimonials, CmaForm, About, Footer } from "@/components/sections";
// New
import { HeroGradient, StatsBar, ServicesGrid, StepsNumbered, SoldGrid, TestimonialsGrid, CmaForm, AboutSplit, Footer } from "@/components/sections";
```

And update component usage:
- `<Hero` → `<HeroGradient`
- `<Services` → `<ServicesGrid`
- `<HowItWorks` → `<StepsNumbered`
- `<SoldHomes` → `<SoldGrid`
- `<Testimonials` → `<TestimonialsGrid`
- `<About` → `<AboutSplit`

(`StatsBar`, `CmaForm`, and `Footer` keep their names.)

- [ ] **Step 3: Run full test suite**

Run: `cd apps/agent-site && npx vitest run`
Expected: ALL tests pass (337 tests, same count as before)

- [ ] **Step 4: Commit**

```bash
git add apps/agent-site/components/sections/index.ts apps/agent-site/templates/emerald-classic.tsx
git commit -m "refactor: update barrel exports and EmeraldClassic imports for variant structure"
```

---

### Task 11: Update EmeraldClassic template test imports

**Files:**
- Modify: `apps/agent-site/__tests__/templates/emerald-classic.test.tsx`

- [ ] **Step 1: Update template test**

The test file at `__tests__/templates/emerald-classic.test.tsx` does NOT import section components directly — it imports `EmeraldClassic` and renders it. So no import changes needed there. But verify by running:

Run: `cd apps/agent-site && npx vitest run __tests__/templates/emerald-classic.test.tsx`
Expected: All 20 tests pass

If any test fails due to changed component names in internal assertions, fix accordingly.

- [ ] **Step 2: Run full test suite one final time**

Run: `cd apps/agent-site && npx vitest run`
Expected: ALL tests pass

- [ ] **Step 3: Commit if any changes were needed**

```bash
git add apps/agent-site/__tests__/templates/emerald-classic.test.tsx
git commit -m "refactor: update template test imports for variant structure"
```

---

## Chunk 2: Modern Minimal Template

Phase 2 creates all Modern Minimal section variants and the template composition file.

### Task 12: Extract shared hero utilities

**Files:**
- Create: `apps/agent-site/components/sections/heroes/hero-utils.ts`
- Create: `apps/agent-site/__tests__/components/heroes/hero-utils.test.ts`

The `safeHref` and `renderHeadline` functions are needed by all 3 hero variants. Extract them once into a shared utility file rather than duplicating across HeroGradient, HeroSplit, and HeroCentered.

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/heroes/hero-utils.test.ts
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render } from "@testing-library/react";
import { createElement } from "react";
import { safeHref, renderHeadline } from "@/components/sections/heroes/hero-utils";

describe("safeHref", () => {
  it("allows hash links", () => {
    expect(safeHref("#cma-form")).toBe("#cma-form");
  });

  it("allows relative paths", () => {
    expect(safeHref("/about")).toBe("/about");
  });

  it("allows https URLs", () => {
    expect(safeHref("https://example.com")).toBe("https://example.com");
  });

  it("allows http URLs", () => {
    expect(safeHref("http://example.com")).toBe("http://example.com");
  });

  it("sanitizes javascript: links to #", () => {
    expect(safeHref("javascript:alert(1)")).toBe("#");
  });

  it("sanitizes data: URLs to #", () => {
    expect(safeHref("data:text/html,<h1>hack</h1>")).toBe("#");
  });

  it("sanitizes invalid URLs to #", () => {
    expect(safeHref("not a url at all")).toBe("#");
  });
});

describe("renderHeadline", () => {
  it("returns plain string when no highlightWord", () => {
    expect(renderHeadline("Hello World")).toBe("Hello World");
  });

  it("returns plain string when highlightWord not found", () => {
    expect(renderHeadline("Hello World", "Missing")).toBe("Hello World");
  });

  it("wraps the highlight word in a styled span", () => {
    const result = renderHeadline("Find Your Dream Home", "Dream");
    // result is a React element — render it to check
    const { container } = render(createElement("h1", null, result));
    const span = container.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("Dream");
  });

  it("highlights the last occurrence of the word", () => {
    const result = renderHeadline("Dream big, live the Dream", "Dream");
    const { container } = render(createElement("h1", null, result));
    const spans = container.querySelectorAll("span");
    // Only one span — the last "Dream"
    expect(spans).toHaveLength(1);
    expect(container.textContent).toBe("Dream big, live the Dream");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/heroes/hero-utils.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Implement hero-utils**

```typescript
// apps/agent-site/components/sections/heroes/hero-utils.ts
import type { ReactNode } from "react";

/**
 * Sanitize href values to prevent XSS via javascript: or data: URLs.
 * Only allows #anchors, /relative paths, and http(s) URLs.
 */
export function safeHref(href: string): string {
  if (href.startsWith("#") || href.startsWith("/")) return href;
  try {
    const url = new URL(href);
    if (url.protocol === "https:" || url.protocol === "http:") return href;
  } catch { /* invalid URL */ }
  return "#";
}

/**
 * Render a headline with an optional highlighted word wrapped in a colored span.
 * Highlights the last occurrence of highlightWord.
 */
export function renderHeadline(headline: string, highlightWord?: string): ReactNode {
  if (!highlightWord) return headline;
  const idx = headline.lastIndexOf(highlightWord);
  if (idx === -1) return headline;
  return (
    <>
      {headline.slice(0, idx)}
      <span style={{ color: "var(--color-accent)" }}>{highlightWord}</span>
      {headline.slice(idx + highlightWord.length)}
    </>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/components/heroes/hero-utils.test.ts`
Expected: PASS

- [ ] **Step 5: Update HeroGradient to import from hero-utils**

In `apps/agent-site/components/sections/heroes/HeroGradient.tsx`, remove the local `safeHref` and `renderHeadline` functions and add:

```typescript
import { safeHref, renderHeadline } from "./hero-utils";
```

- [ ] **Step 6: Run all hero tests to verify no regression**

Run: `cd apps/agent-site && npx vitest run __tests__/components/heroes/`
Expected: All PASS

- [ ] **Step 7: Update heroes barrel export**

Add to `apps/agent-site/components/sections/heroes/index.ts`:
```typescript
export { safeHref, renderHeadline } from "./hero-utils";
```

- [ ] **Step 8: Commit**

```bash
git add apps/agent-site/components/sections/heroes/hero-utils.ts apps/agent-site/__tests__/components/heroes/hero-utils.test.ts apps/agent-site/components/sections/heroes/HeroGradient.tsx apps/agent-site/components/sections/heroes/index.ts
git commit -m "refactor: extract safeHref and renderHeadline to shared hero-utils"
```

---

### Task 13: Create HeroSplit variant

**Files:**
- Create: `apps/agent-site/components/sections/heroes/HeroSplit.tsx`
- Create: `apps/agent-site/__tests__/components/heroes/HeroSplit.test.tsx`
- Modify: `apps/agent-site/components/sections/heroes/index.ts`

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/heroes/HeroSplit.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { HeroSplit } from "@/components/sections/heroes/HeroSplit";
import type { HeroData } from "@/lib/types";

const BASE_DATA: HeroData = {
  headline: "Find Your Dream Home",
  tagline: "Modern approach to real estate",
  cta_text: "Get Started",
  cta_link: "#cma-form",
};

describe("HeroSplit", () => {
  it("renders the headline", () => {
    render(<HeroSplit data={BASE_DATA} />);
    expect(screen.getByRole("heading", { level: 1, name: "Find Your Dream Home" })).toBeInTheDocument();
  });

  it("renders the tagline", () => {
    render(<HeroSplit data={BASE_DATA} />);
    expect(screen.getByText("Modern approach to real estate")).toBeInTheDocument();
  });

  it("renders the CTA link", () => {
    render(<HeroSplit data={BASE_DATA} />);
    const link = screen.getByRole("link");
    expect(link.textContent).toContain("Get Started");
    expect(link).toHaveAttribute("href", "#cma-form");
  });

  it("renders a section element", () => {
    const { container } = render(<HeroSplit data={BASE_DATA} />);
    expect(container.querySelector("section")).toBeInTheDocument();
  });

  it("does not render agent photo when not provided", () => {
    render(<HeroSplit data={BASE_DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders agent photo when provided", () => {
    render(<HeroSplit data={BASE_DATA} agentPhotoUrl="/photos/agent.jpg" agentName="Jane Smith" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Photo of Jane Smith");
  });

  it("uses split layout — white/light background, not gradient", () => {
    const { container } = render(<HeroSplit data={BASE_DATA} />);
    const section = container.querySelector("section");
    // Modern Minimal uses light background, not gradient
    expect(section?.style.background).not.toContain("gradient");
  });

  it("sanitizes javascript: links to #", () => {
    const data = { ...BASE_DATA, cta_link: "javascript:alert(1)" };
    render(<HeroSplit data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("renders body text when provided", () => {
    const data = { ...BASE_DATA, body: "A modern real estate experience." };
    render(<HeroSplit data={data} />);
    expect(screen.getByText("A modern real estate experience.")).toBeInTheDocument();
  });

  it("highlights the highlight_word in the headline", () => {
    const data = { ...BASE_DATA, headline: "Find Your Dream Home", highlight_word: "Dream" };
    render(<HeroSplit data={data} />);
    const heading = screen.getByRole("heading", { level: 1 });
    const span = heading.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("Dream");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/heroes/HeroSplit.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement HeroSplit**

```typescript
// apps/agent-site/components/sections/heroes/HeroSplit.tsx
"use client";

import { useState } from "react";
import Image from "next/image";
import type { HeroProps } from "@/components/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroSplit({ data, agentPhotoUrl, agentName }: HeroProps) {
  const [ctaHover, setCtaHover] = useState(false);

  return (
    <section
      style={{
        background: "#fafafa",
        color: "#1a1a1a",
        paddingTop: "100px",
        paddingBottom: "80px",
        paddingLeft: "60px",
        paddingRight: "60px",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "60px",
        flexWrap: "wrap",
        minHeight: "400px",
      }}
    >
      <div style={{ maxWidth: "480px", flex: 1 }}>
        <h1 style={{
          fontSize: "48px",
          fontWeight: 700,
          lineHeight: 1.1,
          letterSpacing: "-0.5px",
          marginBottom: "16px",
          color: "#1a1a1a",
        }}>
          {renderHeadline(data.headline, data.highlight_word)}
        </h1>
        <p style={{
          fontSize: "18px",
          color: "#666",
          marginBottom: "20px",
          lineHeight: 1.6,
        }}>
          {data.tagline}
        </p>
        {data.body && (
          <p style={{
            fontSize: "16px",
            color: "#888",
            marginBottom: "28px",
            lineHeight: 1.6,
          }}>
            {data.body}
          </p>
        )}
        <a
          href={safeHref(data.cta_link)}
          onMouseEnter={() => setCtaHover(true)}
          onMouseLeave={() => setCtaHover(false)}
          onFocus={() => setCtaHover(true)}
          onBlur={() => setCtaHover(false)}
          style={{
            display: "inline-block",
            background: ctaHover ? "var(--color-primary)" : "#1a1a1a",
            color: "white",
            padding: "14px 32px",
            borderRadius: "30px",
            fontSize: "15px",
            fontWeight: 600,
            textDecoration: "none",
            transition: "all 0.3s",
            transform: ctaHover ? "translateY(-2px)" : "none",
          }}
        >
          {data.cta_text} &rarr;
        </a>
      </div>
      {agentPhotoUrl && (
        <div style={{
          width: "320px",
          height: "380px",
          borderRadius: "16px",
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <Image
            src={agentPhotoUrl}
            alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
            width={320}
            height={380}
            style={{ width: "100%", height: "100%", objectFit: "cover" }}
            priority
          />
        </div>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Update heroes barrel export**

Add the `HeroSplit` export to `apps/agent-site/components/sections/heroes/index.ts` (keep existing exports for HeroGradient and hero-utils):

```typescript
export { HeroSplit } from "./HeroSplit";
```

The full file should now contain:
```typescript
export { HeroGradient } from "./HeroGradient";
export { safeHref, renderHeadline } from "./hero-utils";
export { HeroSplit } from "./HeroSplit";
```

- [ ] **Step 5: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run __tests__/components/heroes/`
Expected: All tests pass (HeroGradient + HeroSplit + hero-utils)

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/components/sections/heroes/ apps/agent-site/__tests__/components/heroes/HeroSplit.test.tsx
git commit -m "feat: add HeroSplit variant for Modern Minimal template"
```

---

### Task 14: Create StatsCards variant

**Files:**
- Create: `apps/agent-site/components/sections/stats/StatsCards.tsx`
- Create: `apps/agent-site/__tests__/components/stats/StatsCards.test.tsx`
- Modify: `apps/agent-site/components/sections/stats/index.ts`

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/stats/StatsCards.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatsCards } from "@/components/sections/stats/StatsCards";

const ITEMS = [
  { value: "150+", label: "Homes Sold" },
  { value: "$2.5M", label: "Total Volume" },
  { value: "5.0", label: "Rating" },
];

describe("StatsCards", () => {
  it("renders all stat values", () => {
    render(<StatsCards items={ITEMS} />);
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("$2.5M")).toBeInTheDocument();
    expect(screen.getByText("5.0")).toBeInTheDocument();
  });

  it("renders all stat labels", () => {
    render(<StatsCards items={ITEMS} />);
    expect(screen.getByText("Homes Sold")).toBeInTheDocument();
    expect(screen.getByText("Total Volume")).toBeInTheDocument();
    expect(screen.getByText("Rating")).toBeInTheDocument();
  });

  it("has aria-label for accessibility", () => {
    render(<StatsCards items={ITEMS} />);
    expect(screen.getByLabelText("Agent statistics")).toBeInTheDocument();
  });

  it("renders sourceDisclaimer when provided", () => {
    render(<StatsCards items={ITEMS} sourceDisclaimer="Data from Zillow" />);
    expect(screen.getByText("Data from Zillow")).toBeInTheDocument();
  });

  it("does not render disclaimer when not provided", () => {
    render(<StatsCards items={ITEMS} />);
    expect(screen.queryByText("Data from Zillow")).not.toBeInTheDocument();
  });

  it("uses bordered card style (not solid background bar)", () => {
    const { container } = render(<StatsCards items={ITEMS} />);
    const section = container.querySelector("section");
    // Modern Minimal uses white/light background, not dark green
    expect(section?.style.background).not.toBe("#1B5E20");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/stats/StatsCards.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement StatsCards**

```typescript
// apps/agent-site/components/sections/stats/StatsCards.tsx
import type { StatsProps } from "@/components/sections/types";

export function StatsCards({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      aria-label="Agent statistics"
      style={{
        background: "white",
        padding: "40px 40px",
      }}
    >
      <dl style={{
        display: "flex",
        justifyContent: "center",
        gap: "20px",
        flexWrap: "wrap",
        margin: 0,
        maxWidth: "800px",
        marginLeft: "auto",
        marginRight: "auto",
      }}>
        {items.map((item) => (
          <div
            key={item.label}
            style={{
              textAlign: "center",
              padding: "20px 30px",
              border: "1px solid #e0e0e0",
              borderRadius: "12px",
              minWidth: "140px",
            }}
          >
            <dd style={{
              fontSize: "28px",
              fontWeight: 700,
              color: "#1a1a1a",
              margin: 0,
            }}>
              {item.value}
            </dd>
            <dt style={{
              fontSize: "12px",
              textTransform: "uppercase",
              letterSpacing: "1px",
              marginTop: "4px",
              color: "#888",
            }}>
              {item.label}
            </dt>
          </div>
        ))}
      </dl>
      {sourceDisclaimer && (
        <p style={{
          textAlign: "center",
          color: "#aaa",
          fontSize: "11px",
          marginTop: "16px",
        }}>
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Update stats barrel export**

```typescript
// apps/agent-site/components/sections/stats/index.ts
export { StatsBar } from "./StatsBar";
export { StatsCards } from "./StatsCards";
```

- [ ] **Step 5: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run __tests__/components/stats/`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/components/sections/stats/ apps/agent-site/__tests__/components/stats/StatsCards.test.tsx
git commit -m "feat: add StatsCards variant for Modern Minimal template"
```

---

### Task 15: Create ServicesClean variant

**Files:**
- Create: `apps/agent-site/components/sections/services/ServicesClean.tsx`
- Create: `apps/agent-site/__tests__/components/services/ServicesClean.test.tsx`
- Modify: `apps/agent-site/components/sections/services/index.ts`

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/services/ServicesClean.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ServicesClean } from "@/components/sections/services/ServicesClean";
import type { ServiceItem } from "@/lib/types";

const ITEMS: ServiceItem[] = [
  { title: "Market Analysis", description: "Deep market insights" },
  { title: "Photography", description: "Professional photos" },
  { title: "Negotiation", description: "Expert negotiation" },
];

describe("ServicesClean", () => {
  it("renders the section heading with default title", () => {
    render(<ServicesClean items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<ServicesClean items={ITEMS} title="My Services" />);
    expect(screen.getByRole("heading", { level: 2, name: "My Services" })).toBeInTheDocument();
  });

  it("renders all service card titles", () => {
    render(<ServicesClean items={ITEMS} />);
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Photography")).toBeInTheDocument();
    expect(screen.getByText("Negotiation")).toBeInTheDocument();
  });

  it("renders all service descriptions", () => {
    render(<ServicesClean items={ITEMS} />);
    expect(screen.getByText("Deep market insights")).toBeInTheDocument();
    expect(screen.getByText("Professional photos")).toBeInTheDocument();
    expect(screen.getByText("Expert negotiation")).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<ServicesClean items={ITEMS} subtitle="Full-service representation" />);
    expect(screen.getByText("Full-service representation")).toBeInTheDocument();
  });

  it("has services section id for anchor linking", () => {
    const { container } = render(<ServicesClean items={ITEMS} />);
    expect(container.querySelector("#services")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/services/ServicesClean.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement ServicesClean**

```typescript
// apps/agent-site/components/sections/services/ServicesClean.tsx
import type { ServicesProps } from "@/components/sections/types";

export function ServicesClean({ items, title, subtitle }: ServicesProps) {
  return (
    <section
      id="services"
      style={{
        padding: "80px 40px",
        maxWidth: "1000px",
        margin: "0 auto",
      }}
    >
      <h2 style={{
        textAlign: "center",
        fontSize: "32px",
        fontWeight: 600,
        color: "#1a1a1a",
        marginBottom: "8px",
        letterSpacing: "-0.3px",
      }}>
        {title ?? "What I Do for You"}
      </h2>
      {subtitle && (
        <p style={{
          textAlign: "center",
          color: "#888",
          fontSize: "16px",
          marginBottom: "50px",
        }}>
          {subtitle}
        </p>
      )}
      <div style={{
        display: "grid",
        gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
        gap: "30px",
      }}>
        {items.map((item) => (
          <div
            key={item.title}
            style={{
              padding: "28px",
              borderRadius: "8px",
              border: "1px solid #eee",
              transition: "box-shadow 0.3s",
            }}
          >
            <h3 style={{
              color: "#1a1a1a",
              fontSize: "18px",
              fontWeight: 600,
              marginBottom: "8px",
            }}>
              {item.title}
            </h3>
            <p style={{ color: "#666", fontSize: "15px", lineHeight: 1.6 }}>
              {item.description}
            </p>
          </div>
        ))}
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Update services barrel export**

```typescript
// apps/agent-site/components/sections/services/index.ts
export { ServicesGrid } from "./ServicesGrid";
export { ServicesClean } from "./ServicesClean";
```

- [ ] **Step 5: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run __tests__/components/services/`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/components/sections/services/ apps/agent-site/__tests__/components/services/ServicesClean.test.tsx
git commit -m "feat: add ServicesClean variant for Modern Minimal template"
```

---

### Task 16: Create StepsTimeline variant

**Files:**
- Create: `apps/agent-site/components/sections/steps/StepsTimeline.tsx`
- Create: `apps/agent-site/__tests__/components/steps/StepsTimeline.test.tsx`
- Modify: `apps/agent-site/components/sections/steps/index.ts`

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/steps/StepsTimeline.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StepsTimeline } from "@/components/sections/steps/StepsTimeline";

const STEPS = [
  { number: 1, title: "Submit Info", description: "Fill out the form" },
  { number: 2, title: "Get Report", description: "Receive your CMA" },
  { number: 3, title: "Meet Agent", description: "Schedule walkthrough" },
];

describe("StepsTimeline", () => {
  it("renders the section heading with default title", () => {
    render(<StepsTimeline steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<StepsTimeline steps={STEPS} title="The Process" />);
    expect(screen.getByRole("heading", { level: 2, name: "The Process" })).toBeInTheDocument();
  });

  it("renders all step titles", () => {
    render(<StepsTimeline steps={STEPS} />);
    expect(screen.getByText("Submit Info")).toBeInTheDocument();
    expect(screen.getByText("Get Report")).toBeInTheDocument();
    expect(screen.getByText("Meet Agent")).toBeInTheDocument();
  });

  it("renders all step descriptions", () => {
    render(<StepsTimeline steps={STEPS} />);
    expect(screen.getByText("Fill out the form")).toBeInTheDocument();
    expect(screen.getByText("Receive your CMA")).toBeInTheDocument();
    expect(screen.getByText("Schedule walkthrough")).toBeInTheDocument();
  });

  it("has how-it-works section id for anchor linking", () => {
    const { container } = render(<StepsTimeline steps={STEPS} />);
    expect(container.querySelector("#how-it-works")).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<StepsTimeline steps={STEPS} subtitle="Simple 3-step process" />);
    expect(screen.getByText("Simple 3-step process")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/steps/StepsTimeline.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement StepsTimeline**

```typescript
// apps/agent-site/components/sections/steps/StepsTimeline.tsx
import type { StepsProps } from "@/components/sections/types";

export function StepsTimeline({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="how-it-works"
      style={{
        background: "white",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "700px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 600,
          color: "#1a1a1a",
          marginBottom: "8px",
          letterSpacing: "-0.3px",
        }}>
          {title ?? "How It Works"}
        </h2>
        {subtitle && (
          <p style={{
            textAlign: "center",
            color: "#888",
            fontSize: "16px",
            marginBottom: "50px",
          }}>
            {subtitle}
          </p>
        )}
        <ol style={{ listStyle: "none", padding: 0, margin: 0 }}>
          {steps.map((step, i) => (
            <li
              key={step.number}
              style={{
                display: "flex",
                gap: "24px",
                alignItems: "flex-start",
                paddingBottom: i < steps.length - 1 ? "40px" : "0",
                position: "relative",
              }}
            >
              {/* Timeline line */}
              {i < steps.length - 1 && (
                <div
                  aria-hidden="true"
                  style={{
                    position: "absolute",
                    left: "19px",
                    top: "40px",
                    width: "2px",
                    height: "calc(100% - 20px)",
                    background: "#e0e0e0",
                  }}
                />
              )}
              {/* Step number */}
              <div
                aria-hidden="true"
                style={{
                  width: "40px",
                  height: "40px",
                  borderRadius: "50%",
                  border: "2px solid var(--color-primary)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: "16px",
                  fontWeight: 600,
                  color: "var(--color-primary)",
                  flexShrink: 0,
                  background: "white",
                  position: "relative",
                  zIndex: 1,
                }}
              >
                {step.number}
              </div>
              {/* Content */}
              <div style={{ paddingTop: "6px" }}>
                <h3 style={{
                  color: "#1a1a1a",
                  fontSize: "18px",
                  fontWeight: 600,
                  marginBottom: "4px",
                }}>
                  {step.title}
                </h3>
                <p style={{ color: "#888", fontSize: "14px", lineHeight: 1.6 }}>
                  {step.description}
                </p>
              </div>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Update steps barrel export**

```typescript
// apps/agent-site/components/sections/steps/index.ts
export { StepsNumbered } from "./StepsNumbered";
export { StepsTimeline } from "./StepsTimeline";
```

- [ ] **Step 5: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run __tests__/components/steps/`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/components/sections/steps/ apps/agent-site/__tests__/components/steps/StepsTimeline.test.tsx
git commit -m "feat: add StepsTimeline variant for Modern Minimal template"
```

---

### Task 17: Create SoldMinimal variant

**Files:**
- Create: `apps/agent-site/components/sections/sold/SoldMinimal.tsx`
- Create: `apps/agent-site/__tests__/components/sold/SoldMinimal.test.tsx`
- Modify: `apps/agent-site/components/sections/sold/index.ts`

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/sold/SoldMinimal.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { SoldMinimal } from "@/components/sections/sold/SoldMinimal";

const ITEMS = [
  { address: "123 Main St", city: "Hoboken", state: "NJ", price: "$750,000" },
  { address: "456 Elm Ave", city: "Jersey City", state: "NJ", price: "$620,000" },
];

describe("SoldMinimal", () => {
  it("renders the section heading with default title", () => {
    render(<SoldMinimal items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders all sold home prices", () => {
    render(<SoldMinimal items={ITEMS} />);
    expect(screen.getByText("$750,000")).toBeInTheDocument();
    expect(screen.getByText("$620,000")).toBeInTheDocument();
  });

  it("renders addresses", () => {
    render(<SoldMinimal items={ITEMS} />);
    expect(screen.getByText(/123 Main St/)).toBeInTheDocument();
    expect(screen.getByText(/456 Elm Ave/)).toBeInTheDocument();
  });

  it("has sold section id for anchor linking", () => {
    const { container } = render(<SoldMinimal items={ITEMS} />);
    expect(container.querySelector("#sold")).toBeInTheDocument();
  });

  it("renders custom title", () => {
    render(<SoldMinimal items={ITEMS} title="Recent Closings" />);
    expect(screen.getByRole("heading", { level: 2, name: "Recent Closings" })).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<SoldMinimal items={ITEMS} subtitle="Proven track record" />);
    expect(screen.getByText("Proven track record")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails, then implement**

- [ ] **Step 3: Implement SoldMinimal**

```typescript
// apps/agent-site/components/sections/sold/SoldMinimal.tsx
import Image from "next/image";
import type { SoldHomesProps } from "@/components/sections/types";

export function SoldMinimal({ items, title, subtitle }: SoldHomesProps) {
  return (
    <section
      id="sold"
      style={{
        padding: "80px 40px",
        maxWidth: "1000px",
        margin: "0 auto",
      }}
    >
      <h2 style={{
        textAlign: "center",
        fontSize: "32px",
        fontWeight: 600,
        color: "#1a1a1a",
        marginBottom: "8px",
        letterSpacing: "-0.3px",
      }}>
        {title ?? "Recently Sold"}
      </h2>
      {subtitle && (
        <p style={{
          textAlign: "center",
          color: "#888",
          fontSize: "16px",
          marginBottom: "50px",
        }}>
          {subtitle}
        </p>
      )}
      <div style={{
        display: "grid",
        gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
        gap: "24px",
      }}>
        {items.map((item) => (
          <article
            key={`${item.address}-${item.city}`}
            style={{
              borderRadius: "8px",
              overflow: "hidden",
              border: "1px solid #eee",
            }}
          >
            {item.image_url && (
              <div style={{
                width: "100%",
                height: "160px",
                position: "relative",
              }}>
                <Image
                  src={item.image_url}
                  alt={`${item.address}, ${item.city}`}
                  fill
                  style={{ objectFit: "cover" }}
                  sizes="(max-width: 768px) 50vw, 220px"
                />
              </div>
            )}
            <div style={{ padding: "16px" }}>
              <div style={{
                fontSize: "20px",
                fontWeight: 700,
                color: "#1a1a1a",
                marginBottom: "4px",
              }}>
                {item.price}
              </div>
              <div style={{
                fontSize: "13px",
                color: "#888",
              }}>
                {item.address}, {item.city}, {item.state}
              </div>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Update sold barrel export and run tests**

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/sold/ apps/agent-site/__tests__/components/sold/SoldMinimal.test.tsx
git commit -m "feat: add SoldMinimal variant for Modern Minimal template"
```

---

### Task 18: Create TestimonialsClean variant

**Files:**
- Create: `apps/agent-site/components/sections/testimonials/TestimonialsClean.tsx`
- Create: `apps/agent-site/__tests__/components/testimonials/TestimonialsClean.test.tsx`
- Modify: `apps/agent-site/components/sections/testimonials/index.ts`

- [ ] **Step 1: Write test file**

```typescript
// apps/agent-site/__tests__/components/testimonials/TestimonialsClean.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { TestimonialsClean } from "@/components/sections/testimonials/TestimonialsClean";
import type { TestimonialItem } from "@/lib/types";

const ITEMS: TestimonialItem[] = [
  { reviewer: "Alice M.", text: "Wonderful experience!", rating: 5, source: "Zillow" },
  { reviewer: "Bob K.", text: "Very professional.", rating: 4, source: "Google" },
];

describe("TestimonialsClean", () => {
  it("renders the default title", () => {
    render(<TestimonialsClean items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "What My Clients Say" })).toBeInTheDocument();
  });

  it("renders a custom title", () => {
    render(<TestimonialsClean items={ITEMS} title="Client Stories" />);
    expect(screen.getByRole("heading", { level: 2, name: "Client Stories" })).toBeInTheDocument();
  });

  it("uses id=testimonials for anchor linking", () => {
    const { container } = render(<TestimonialsClean items={ITEMS} />);
    expect(container.querySelector("#testimonials")).toBeInTheDocument();
  });

  it("renders all testimonial items", () => {
    render(<TestimonialsClean items={ITEMS} />);
    expect(screen.getByText("Wonderful experience!")).toBeInTheDocument();
    expect(screen.getByText("Very professional.")).toBeInTheDocument();
  });

  it("renders reviewer names", () => {
    render(<TestimonialsClean items={ITEMS} />);
    expect(screen.getByText(/Alice M\./)).toBeInTheDocument();
    expect(screen.getByText(/Bob K\./)).toBeInTheDocument();
  });

  it("renders star ratings with aria labels", () => {
    render(<TestimonialsClean items={ITEMS} />);
    expect(screen.getByLabelText("5 out of 5 stars")).toBeInTheDocument();
    expect(screen.getByLabelText("4 out of 5 stars")).toBeInTheDocument();
  });

  it("renders source attribution", () => {
    render(<TestimonialsClean items={ITEMS} />);
    expect(screen.getByText(/via Zillow/)).toBeInTheDocument();
    expect(screen.getByText(/via Google/)).toBeInTheDocument();
  });

  it("includes FTC disclaimer text", () => {
    render(<TestimonialsClean items={ITEMS} />);
    expect(screen.getByText(/No compensation was provided/)).toBeInTheDocument();
  });

  it("uses clean minimal styling — white background cards with thin borders", () => {
    const { container } = render(<TestimonialsClean items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(2);
    expect(articles[0].style.border).toContain("1px solid");
    expect(articles[0].style.background).toBe("white");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/testimonials/TestimonialsClean.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement TestimonialsClean**

```typescript
// apps/agent-site/components/sections/testimonials/TestimonialsClean.tsx
import type { TestimonialsProps } from "@/components/sections/types";

export function TestimonialsClean({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "#fafafa",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 600,
          color: "#1a1a1a",
          marginBottom: "8px",
          letterSpacing: "-0.3px",
        }}>
          {title ?? "What My Clients Say"}
        </h2>
        <p style={{
          textAlign: "center",
          color: "#aaa",
          fontSize: "12px",
          marginBottom: "50px",
        }}>
          Real reviews from real clients. Unedited excerpts from verified reviews on Zillow.
          No compensation was provided. Individual results may vary.
        </p>
        <div style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
          gap: "24px",
        }}>
          {items.map((item) => (
            <article
              key={item.reviewer}
              style={{
                background: "white",
                borderRadius: "8px",
                padding: "24px",
                border: "1px solid #eee",
              }}
            >
              <span
                role="img"
                aria-label={`${item.rating} out of 5 stars`}
                style={{
                  display: "block",
                  color: "var(--color-accent)",
                  fontSize: "16px",
                  marginBottom: "12px",
                }}
              >
                {"★".repeat(item.rating)}{"☆".repeat(5 - item.rating)}
              </span>
              <p style={{
                color: "#555",
                fontSize: "14px",
                lineHeight: 1.7,
                fontStyle: "italic",
              }}>
                {item.text}
              </p>
              <div style={{
                marginTop: "16px",
                fontWeight: 600,
                color: "#1a1a1a",
                fontSize: "14px",
              }}>
                — {item.reviewer}
                {item.source && (
                  <span style={{ fontWeight: "normal", color: "#aaa" }}>
                    {" "}via {item.source}
                  </span>
                )}
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Update barrel export, run tests, commit**

```bash
git add apps/agent-site/components/sections/testimonials/ apps/agent-site/__tests__/components/testimonials/TestimonialsClean.test.tsx
git commit -m "feat: add TestimonialsClean variant for Modern Minimal template"
```

---

### Task 19: Create AboutMinimal variant

**Files:**
- Create: `apps/agent-site/components/sections/about/AboutMinimal.tsx`
- Create: `apps/agent-site/__tests__/components/about/AboutMinimal.test.tsx`
- Modify: `apps/agent-site/components/sections/about/index.ts`

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/about/AboutMinimal.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutMinimal } from "@/components/sections/about/AboutMinimal";
import { AGENT } from "../fixtures";
import type { AboutData } from "@/lib/types";

const ABOUT_DATA: AboutData = {
  bio: "A modern approach to real estate.",
  credentials: ["Licensed REALTOR", "Certified Negotiation Expert"],
};

describe("AboutMinimal", () => {
  it("renders the agent name in heading", () => {
    render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByRole("heading", { level: 2, name: /About/ })).toBeInTheDocument();
  });

  it("renders the bio text", () => {
    render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByText("A modern approach to real estate.")).toBeInTheDocument();
  });

  it("handles array bio", () => {
    const arrayBio: AboutData = { bio: ["First paragraph.", "Second paragraph."], credentials: [] };
    render(<AboutMinimal agent={AGENT} data={arrayBio} />);
    expect(screen.getByText("First paragraph.")).toBeInTheDocument();
    expect(screen.getByText("Second paragraph.")).toBeInTheDocument();
  });

  it("uses id=about for anchor linking", () => {
    const { container } = render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    expect(container.querySelector("#about")).toBeInTheDocument();
  });

  it("renders agent photo when available", () => {
    render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    if (AGENT.identity.headshot_url) {
      expect(screen.getByRole("img")).toBeInTheDocument();
    }
  });

  it("renders credentials as inline text", () => {
    render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByText(/Licensed REALTOR/)).toBeInTheDocument();
    expect(screen.getByText(/Certified Negotiation Expert/)).toBeInTheDocument();
  });

  it("uses flexWrap for responsive layout", () => {
    const { container } = render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    const flexContainer = container.querySelector("[style*='flex-wrap: wrap']");
    expect(flexContainer).toBeInTheDocument();
  });

  it("uses clean minimal style — no gradient background", () => {
    const { container } = render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    const section = container.querySelector("#about");
    expect(section?.style.background).not.toContain("gradient");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/about/AboutMinimal.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement AboutMinimal**

```typescript
// apps/agent-site/components/sections/about/AboutMinimal.tsx
import Image from "next/image";
import type { AboutProps } from "@/components/sections/types";

export function AboutMinimal({ agent, data }: AboutProps) {
  return (
    <section
      id="about"
      style={{
        padding: "80px 40px",
        maxWidth: "900px",
        margin: "0 auto",
      }}
    >
      <div style={{
        display: "flex",
        alignItems: "flex-start",
        gap: "40px",
        flexWrap: "wrap",
        justifyContent: "center",
      }}>
        {agent.identity.headshot_url && (
          <div style={{
            width: "220px",
            height: "280px",
            borderRadius: "12px",
            overflow: "hidden",
            flexShrink: 0,
            position: "relative",
          }}>
            <Image
              src={agent.identity.headshot_url}
              alt={agent.identity.name}
              fill
              style={{ objectFit: "cover" }}
              sizes="220px"
            />
          </div>
        )}
        <div style={{ maxWidth: "500px", flex: 1 }}>
          <h2 style={{
            color: "#1a1a1a",
            fontSize: "28px",
            fontWeight: 600,
            marginBottom: "16px",
            letterSpacing: "-0.3px",
          }}>
            {data.title || `About ${agent.identity.name}`}
          </h2>
          {Array.isArray(data.bio) ? (
            data.bio.map((paragraph, i) => (
              <p key={i} style={{ color: "#666", fontSize: "15px", marginBottom: "12px", lineHeight: 1.7 }}>
                {paragraph}
              </p>
            ))
          ) : (
            <p style={{ color: "#666", fontSize: "15px", marginBottom: "12px", lineHeight: 1.7 }}>
              {data.bio}
            </p>
          )}
          {data.credentials && data.credentials.length > 0 && (
            <p style={{
              marginTop: "16px",
              fontSize: "13px",
              color: "#888",
            }}>
              {data.credentials.join(" · ")}
            </p>
          )}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Update barrel export, run tests, commit**

```bash
git add apps/agent-site/components/sections/about/ apps/agent-site/__tests__/components/about/AboutMinimal.test.tsx
git commit -m "feat: add AboutMinimal variant for Modern Minimal template"
```

---

### Task 20: Create ModernMinimal template and register it

**Files:**
- Create: `apps/agent-site/templates/modern-minimal.tsx`
- Create: `apps/agent-site/__tests__/templates/modern-minimal.test.tsx`
- Modify: `apps/agent-site/templates/index.ts`
- Modify: `apps/agent-site/__tests__/templates/index.test.ts`
- Modify: `apps/agent-site/components/sections/index.ts`

- [ ] **Step 1: Update sections barrel to export new variants**

Add to `apps/agent-site/components/sections/index.ts`:
```typescript
export { HeroSplit } from "./heroes/HeroSplit";
export { StatsCards } from "./stats/StatsCards";
export { ServicesClean } from "./services/ServicesClean";
export { StepsTimeline } from "./steps/StepsTimeline";
export { SoldMinimal } from "./sold/SoldMinimal";
export { TestimonialsClean } from "./testimonials/TestimonialsClean";
export { AboutMinimal } from "./about/AboutMinimal";
```

- [ ] **Step 2: Write ModernMinimal template test**

```typescript
// apps/agent-site/__tests__/templates/modern-minimal.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ModernMinimal } from "@/templates/modern-minimal";
import { AGENT, CONTENT, CONTENT_ALL_DISABLED } from "../components/fixtures";

vi.mock("next/script", () => ({
  __esModule: true,
  default: ({ id, src }: { id?: string; src: string }) => (
    <script data-testid={id} data-src={src} />
  ),
}));

describe("ModernMinimal template", () => {
  it("always renders the Nav", () => {
    render(<ModernMinimal agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("navigation", { name: "Main navigation" })).toBeInTheDocument();
  });

  it("always renders the Footer", () => {
    render(<ModernMinimal agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("contentinfo")).toBeInTheDocument();
  });

  it("renders Hero section when enabled", () => {
    render(<ModernMinimal agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });

  it("does not render Hero when disabled", () => {
    render(<ModernMinimal agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
  });

  it("renders all sections when all enabled", () => {
    render(<ModernMinimal agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Submit Info")).toBeInTheDocument();
    expect(screen.getByText("$750,000")).toBeInTheDocument();
    expect(screen.getByText("Amazing service!")).toBeInTheDocument();
    expect(screen.getByText(/About Jane Smith/)).toBeInTheDocument();
  });

  it("does not render disabled sections", () => {
    render(<ModernMinimal agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
    expect(screen.queryByText("Homes Sold")).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

- [ ] **Step 4: Implement ModernMinimal template**

```typescript
// apps/agent-site/templates/modern-minimal.tsx
import type { AgentConfig, AgentContent } from "@/lib/types";
import { Nav } from "@/components/Nav";
import { Analytics } from "@/components/Analytics";
import {
  HeroSplit,
  StatsCards,
  ServicesClean,
  StepsTimeline,
  SoldMinimal,
  TestimonialsClean,
  CmaForm,
  AboutMinimal,
  Footer,
} from "@/components/sections";

interface TemplateProps {
  agent: AgentConfig;
  content: AgentContent;
}

export function ModernMinimal({ agent, content }: TemplateProps) {
  const s = content.sections;
  return (
    <>
      <Analytics tracking={agent.integrations?.tracking} />
      <Nav agent={agent} />
      <div style={{ paddingTop: "0" }}>
      {s.hero.enabled && (
        <HeroSplit
          data={s.hero.data}
          agentPhotoUrl={agent.identity.headshot_url}
          agentName={agent.identity.name}
        />
      )}
      {s.stats.enabled && s.stats.data.items.length > 0 && (
        <StatsCards items={s.stats.data.items} sourceDisclaimer="Based on data from Zillow. Individual results may vary." />
      )}
      {s.services.enabled && (
        <ServicesClean
          items={s.services.data.items}
          title={s.services.data.title}
          subtitle={s.services.data.subtitle}
        />
      )}
      {s.how_it_works.enabled && (
        <StepsTimeline
          steps={s.how_it_works.data.steps}
          title={s.how_it_works.data.title}
          subtitle={s.how_it_works.data.subtitle}
        />
      )}
      {s.sold_homes.enabled && s.sold_homes.data.items.length > 0 && (
        <SoldMinimal
          items={s.sold_homes.data.items}
          title={s.sold_homes.data.title}
          subtitle={s.sold_homes.data.subtitle}
        />
      )}
      {s.testimonials.enabled && s.testimonials.data.items.length > 0 && (
        <TestimonialsClean
          items={s.testimonials.data.items}
          title={s.testimonials.data.title}
        />
      )}
      {s.cma_form.enabled && (
        <CmaForm
          agentId={agent.id}
          agentName={agent.identity.name}
          defaultState={agent.location.state}
          formHandler={agent.integrations?.form_handler}
          formHandlerId={agent.integrations?.form_handler_id}
          tracking={agent.integrations?.tracking}
          data={s.cma_form.data}
          serviceAreas={agent.location.service_areas}
        />
      )}
      {s.about.enabled && <AboutMinimal agent={agent} data={s.about.data} />}
      <Footer agent={agent} agentId={agent.id} />
      </div>
    </>
  );
}
```

- [ ] **Step 5: Register ModernMinimal in template registry**

Update `apps/agent-site/templates/index.ts`:

```typescript
import { EmeraldClassic } from "./emerald-classic";
import { ModernMinimal } from "./modern-minimal";

export const TEMPLATES: Record<string, typeof EmeraldClassic> = {
  "emerald-classic": EmeraldClassic,
  "modern-minimal": ModernMinimal,
};

export function getTemplate(name: string) {
  return TEMPLATES[name] || EmeraldClassic;
}
```

- [ ] **Step 6: Update registry tests**

Add to `apps/agent-site/__tests__/templates/index.test.ts`:

```typescript
import { ModernMinimal } from "@/templates/modern-minimal";

// Add these tests inside the describe block:
it("returns ModernMinimal for 'modern-minimal'", () => {
  const Template = getTemplate("modern-minimal");
  expect(Template).toBe(ModernMinimal);
});

it("TEMPLATES registry contains modern-minimal key", () => {
  expect("modern-minimal" in TEMPLATES).toBe(true);
});
```

- [ ] **Step 7: Run full test suite**

Run: `cd apps/agent-site && npx vitest run`
Expected: All tests pass

- [ ] **Step 8: Commit**

```bash
git add apps/agent-site/templates/ apps/agent-site/__tests__/templates/ apps/agent-site/components/sections/index.ts
git commit -m "feat: add Modern Minimal template with all section variants"
```

---

## Chunk 3: Warm Community Template

Phase 3 creates all Warm Community section variants and template composition file. Same pattern as Phase 2 but with warm, friendly, rounded styling.

### Task 21: Create HeroCentered variant

**Files:**
- Create: `apps/agent-site/components/sections/heroes/HeroCentered.tsx`
- Create: `apps/agent-site/__tests__/components/heroes/HeroCentered.test.tsx`
- Modify: `apps/agent-site/components/sections/heroes/index.ts`

- [ ] **Step 1: Write test, verify fail**

```typescript
// apps/agent-site/__tests__/components/heroes/HeroCentered.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { HeroCentered } from "@/components/sections/heroes/HeroCentered";
import type { HeroData } from "@/lib/types";

const BASE_DATA: HeroData = {
  headline: "Welcome Home",
  tagline: "Your neighborhood agent",
  cta_text: "Let's Talk",
  cta_link: "#cma-form",
};

describe("HeroCentered", () => {
  it("renders the headline", () => {
    render(<HeroCentered data={BASE_DATA} />);
    expect(screen.getByRole("heading", { level: 1, name: "Welcome Home" })).toBeInTheDocument();
  });

  it("renders the tagline", () => {
    render(<HeroCentered data={BASE_DATA} />);
    expect(screen.getByText("Your neighborhood agent")).toBeInTheDocument();
  });

  it("renders the CTA link", () => {
    render(<HeroCentered data={BASE_DATA} />);
    const link = screen.getByRole("link");
    expect(link.textContent).toContain("Let's Talk");
    expect(link).toHaveAttribute("href", "#cma-form");
  });

  it("renders a section element", () => {
    const { container } = render(<HeroCentered data={BASE_DATA} />);
    expect(container.querySelector("section")).toBeInTheDocument();
  });

  it("uses centered layout with warm background", () => {
    const { container } = render(<HeroCentered data={BASE_DATA} />);
    const section = container.querySelector("section");
    expect(section?.style.textAlign).toBe("center");
    expect(section?.style.background).toBe("#FFF8F0");
  });

  it("does not render agent photo when not provided", () => {
    render(<HeroCentered data={BASE_DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders agent photo when provided", () => {
    render(<HeroCentered data={BASE_DATA} agentPhotoUrl="/photos/agent.jpg" agentName="Jane Smith" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Photo of Jane Smith");
  });

  it("sanitizes javascript: links to #", () => {
    const data = { ...BASE_DATA, cta_link: "javascript:alert(1)" };
    render(<HeroCentered data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("sanitizes data: URLs to #", () => {
    const data = { ...BASE_DATA, cta_link: "data:text/html,<h1>xss</h1>" };
    render(<HeroCentered data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("renders body text when provided", () => {
    const data = { ...BASE_DATA, body: "We treat every client like family." };
    render(<HeroCentered data={data} />);
    expect(screen.getByText("We treat every client like family.")).toBeInTheDocument();
  });

  it("highlights the highlight_word in the headline", () => {
    const data = { ...BASE_DATA, headline: "Welcome Home", highlight_word: "Home" };
    render(<HeroCentered data={data} />);
    const heading = screen.getByRole("heading", { level: 1 });
    const span = heading.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("Home");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/heroes/HeroCentered.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement HeroCentered**

Key differences from other hero variants:
- Centered layout (text-align center, photo above or below text)
- Warm-toned background: `#FFF8F0` (warm cream)
- Rounded photo with warm border (not accent color — use a warm tone)
- CTA button with rounded style, warm colors

```typescript
// apps/agent-site/components/sections/heroes/HeroCentered.tsx
"use client";

import { useState } from "react";
import Image from "next/image";
import type { HeroProps } from "@/components/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroCentered({ data, agentPhotoUrl, agentName }: HeroProps) {
  const [ctaHover, setCtaHover] = useState(false);

  return (
    <section
      style={{
        background: "#FFF8F0",
        color: "#4A3728",
        paddingTop: "100px",
        paddingBottom: "70px",
        paddingLeft: "40px",
        paddingRight: "40px",
        textAlign: "center",
        minHeight: "400px",
      }}
    >
      {agentPhotoUrl && (
        <div style={{
          width: "200px",
          height: "200px",
          borderRadius: "50%",
          overflow: "hidden",
          border: "5px solid var(--color-accent)",
          margin: "0 auto 24px",
          boxShadow: "0 8px 24px rgba(0,0,0,0.1)",
        }}>
          <Image
            src={agentPhotoUrl}
            alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
            width={200}
            height={200}
            style={{ width: "100%", height: "100%", objectFit: "cover" }}
            priority
          />
        </div>
      )}
      <h1 style={{
        fontSize: "40px",
        fontWeight: 700,
        lineHeight: 1.2,
        marginBottom: "12px",
        color: "#4A3728",
        maxWidth: "600px",
        marginLeft: "auto",
        marginRight: "auto",
      }}>
        {renderHeadline(data.headline, data.highlight_word)}
      </h1>
      <p style={{
        fontSize: "18px",
        color: "#8B7355",
        marginBottom: "16px",
        fontStyle: "italic",
      }}>
        {data.tagline}
      </p>
      {data.body && (
        <p style={{
          fontSize: "16px",
          color: "#8B7355",
          marginBottom: "28px",
          maxWidth: "500px",
          marginLeft: "auto",
          marginRight: "auto",
          lineHeight: 1.6,
        }}>
          {data.body}
        </p>
      )}
      <a
        href={safeHref(data.cta_link)}
        onMouseEnter={() => setCtaHover(true)}
        onMouseLeave={() => setCtaHover(false)}
        onFocus={() => setCtaHover(true)}
        onBlur={() => setCtaHover(false)}
        style={{
          display: "inline-block",
          background: ctaHover ? "var(--color-primary)" : "var(--color-accent)",
          color: ctaHover ? "white" : "var(--color-primary)",
          padding: "14px 36px",
          borderRadius: "30px",
          fontSize: "16px",
          fontWeight: 700,
          textDecoration: "none",
          transition: "all 0.3s",
          transform: ctaHover ? "translateY(-2px)" : "none",
          boxShadow: ctaHover ? "0 6px 20px rgba(0,0,0,0.15)" : "0 2px 8px rgba(0,0,0,0.08)",
        }}
      >
        {data.cta_text} &rarr;
      </a>
    </section>
  );
}
```

- [ ] **Step 3: Update barrel, run tests, commit**

```bash
git add apps/agent-site/components/sections/heroes/ apps/agent-site/__tests__/components/heroes/HeroCentered.test.tsx
git commit -m "feat: add HeroCentered variant for Warm Community template"
```

---

### Task 22: Create StatsInline variant

**Files:**
- Create: `apps/agent-site/components/sections/stats/StatsInline.tsx`
- Create: `apps/agent-site/__tests__/components/stats/StatsInline.test.tsx`
- Modify: `apps/agent-site/components/sections/stats/index.ts`

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/stats/StatsInline.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatsInline } from "@/components/sections/stats/StatsInline";

const ITEMS = [
  { value: "150+", label: "Homes Sold" },
  { value: "$2.5M", label: "Total Volume" },
  { value: "5.0", label: "Rating" },
];

describe("StatsInline", () => {
  it("renders all stat values", () => {
    render(<StatsInline items={ITEMS} />);
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("$2.5M")).toBeInTheDocument();
    expect(screen.getByText("5.0")).toBeInTheDocument();
  });

  it("renders all stat labels", () => {
    render(<StatsInline items={ITEMS} />);
    expect(screen.getByText("Homes Sold")).toBeInTheDocument();
    expect(screen.getByText("Total Volume")).toBeInTheDocument();
    expect(screen.getByText("Rating")).toBeInTheDocument();
  });

  it("has aria-label for accessibility", () => {
    render(<StatsInline items={ITEMS} />);
    expect(screen.getByLabelText("Agent statistics")).toBeInTheDocument();
  });

  it("renders disclaimer when provided", () => {
    render(<StatsInline items={ITEMS} sourceDisclaimer="Data from Zillow." />);
    expect(screen.getByText("Data from Zillow.")).toBeInTheDocument();
  });

  it("does not render disclaimer when not provided", () => {
    const { container } = render(<StatsInline items={ITEMS} />);
    // Only the stats section, no disclaimer paragraph
    const paragraphs = container.querySelectorAll("p");
    expect(paragraphs.length).toBe(0);
  });

  it("uses warm styling — soft rounded cards with shadows", () => {
    const { container } = render(<StatsInline items={ITEMS} />);
    const cards = container.querySelectorAll("div[style]");
    // Find a card with borderRadius and boxShadow
    const styledCards = Array.from(cards).filter(
      (el) => (el as HTMLElement).style.borderRadius && (el as HTMLElement).style.boxShadow
    );
    expect(styledCards.length).toBeGreaterThan(0);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/stats/StatsInline.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement StatsInline**

```typescript
// apps/agent-site/components/sections/stats/StatsInline.tsx
import type { StatsProps } from "@/components/sections/types";

export function StatsInline({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      aria-label="Agent statistics"
      style={{
        padding: "50px 40px",
        background: "white",
      }}
    >
      <dl style={{
        display: "flex",
        justifyContent: "center",
        gap: "20px",
        flexWrap: "wrap",
        maxWidth: "900px",
        margin: "0 auto",
      }}>
        {items.map((item) => (
          <div
            key={item.label}
            style={{
              background: "#FFF8F0",
              borderRadius: "16px",
              padding: "24px 32px",
              textAlign: "center",
              minWidth: "140px",
              boxShadow: "0 2px 8px rgba(0,0,0,0.06)",
            }}
          >
            <dd style={{
              fontSize: "28px",
              fontWeight: 700,
              color: "#4A3728",
              margin: 0,
            }}>
              {item.value}
            </dd>
            <dt style={{
              fontSize: "12px",
              textTransform: "uppercase",
              letterSpacing: "1px",
              marginTop: "4px",
              color: "#8B7355",
            }}>
              {item.label}
            </dt>
          </div>
        ))}
      </dl>
      {sourceDisclaimer && (
        <p style={{
          textAlign: "center",
          color: "#B0A090",
          fontSize: "11px",
          marginTop: "16px",
        }}>
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Update stats barrel export**

Add to `apps/agent-site/components/sections/stats/index.ts`:
```typescript
export { StatsInline } from "./StatsInline";
```

- [ ] **Step 5: Run tests, commit**

```bash
git add apps/agent-site/components/sections/stats/ apps/agent-site/__tests__/components/stats/StatsInline.test.tsx
git commit -m "feat: add StatsInline variant for Warm Community template"
```

---

### Task 23: Create ServicesIcons variant

**Files:**
- Create: `apps/agent-site/components/sections/services/ServicesIcons.tsx`
- Create: `apps/agent-site/__tests__/components/services/ServicesIcons.test.tsx`
- Modify: `apps/agent-site/components/sections/services/index.ts`

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/services/ServicesIcons.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ServicesIcons } from "@/components/sections/services/ServicesIcons";
import type { ServiceItem } from "@/lib/types";

const ITEMS: ServiceItem[] = [
  { title: "Market Analysis", description: "Deep market insights" },
  { title: "Photography", description: "Professional photos" },
  { title: "Negotiation", description: "Expert negotiation" },
];

describe("ServicesIcons", () => {
  it("renders the section heading with default title", () => {
    render(<ServicesIcons items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<ServicesIcons items={ITEMS} title="What I Offer" />);
    expect(screen.getByRole("heading", { level: 2, name: "What I Offer" })).toBeInTheDocument();
  });

  it("renders all service card titles", () => {
    render(<ServicesIcons items={ITEMS} />);
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Photography")).toBeInTheDocument();
    expect(screen.getByText("Negotiation")).toBeInTheDocument();
  });

  it("renders service descriptions", () => {
    render(<ServicesIcons items={ITEMS} />);
    expect(screen.getByText("Deep market insights")).toBeInTheDocument();
    expect(screen.getByText("Professional photos")).toBeInTheDocument();
  });

  it("uses id=services for anchor linking", () => {
    const { container } = render(<ServicesIcons items={ITEMS} />);
    expect(container.querySelector("#services")).toBeInTheDocument();
  });

  it("renders icon circles for each service", () => {
    const { container } = render(<ServicesIcons items={ITEMS} />);
    // Each card has a circular icon area — check via computed style
    const articles = container.querySelectorAll("article");
    articles.forEach((article) => {
      const circle = article.querySelector("div");
      expect(circle?.style.borderRadius).toBe("50%");
    });
  });

  it("uses rounded cards with soft shadows", () => {
    const { container } = render(<ServicesIcons items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
    expect(articles[0].style.borderRadius).toBeTruthy();
    expect(articles[0].style.boxShadow).toBeTruthy();
  });

  it("renders subtitle when provided", () => {
    render(<ServicesIcons items={ITEMS} subtitle="We go the extra mile" />);
    expect(screen.getByText("We go the extra mile")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/services/ServicesIcons.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement ServicesIcons**

```typescript
// apps/agent-site/components/sections/services/ServicesIcons.tsx
import type { ServicesProps } from "@/components/sections/types";

export function ServicesIcons({ items, title, subtitle }: ServicesProps) {
  return (
    <section
      id="services"
      style={{
        padding: "70px 40px",
        background: "#FFF8F0",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 700,
          color: "#4A3728",
          marginBottom: subtitle ? "8px" : "40px",
        }}>
          {title ?? "How I Help"}
        </h2>
        {subtitle && (
          <p style={{
            textAlign: "center",
            color: "#8B7355",
            fontSize: "16px",
            marginBottom: "40px",
          }}>
            {subtitle}
          </p>
        )}
        <div style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(250px, 1fr))",
          gap: "24px",
        }}>
          {items.map((item) => (
            <article
              key={item.title}
              style={{
                background: "white",
                borderRadius: "16px",
                padding: "32px 24px",
                textAlign: "center",
                boxShadow: "0 2px 12px rgba(0,0,0,0.06)",
              }}
            >
              <div style={{
                width: "56px",
                height: "56px",
                borderRadius: "50%",
                background: "var(--color-accent)",
                margin: "0 auto 16px",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                opacity: 0.2,
              }} />
              <h3 style={{
                fontSize: "18px",
                fontWeight: 700,
                color: "#4A3728",
                marginBottom: "8px",
              }}>
                {item.title}
              </h3>
              <p style={{
                fontSize: "14px",
                color: "#8B7355",
                lineHeight: 1.6,
              }}>
                {item.description}
              </p>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Update services barrel export**

Add to `apps/agent-site/components/sections/services/index.ts`:
```typescript
export { ServicesIcons } from "./ServicesIcons";
```

- [ ] **Step 5: Run tests, commit**

```bash
git add apps/agent-site/components/sections/services/ apps/agent-site/__tests__/components/services/ServicesIcons.test.tsx
git commit -m "feat: add ServicesIcons variant for Warm Community template"
```

---

### Task 24: Create StepsFriendly variant

**Files:**
- Create: `apps/agent-site/components/sections/steps/StepsFriendly.tsx`
- Create: `apps/agent-site/__tests__/components/steps/StepsFriendly.test.tsx`
- Modify: `apps/agent-site/components/sections/steps/index.ts`

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/steps/StepsFriendly.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StepsFriendly } from "@/components/sections/steps/StepsFriendly";
import type { StepItem } from "@/lib/types";

const STEPS: StepItem[] = [
  { number: 1, title: "Submit Info", description: "Fill out a simple form" },
  { number: 2, title: "Get Analysis", description: "We run the numbers" },
  { number: 3, title: "Review Report", description: "See your CMA report" },
];

describe("StepsFriendly", () => {
  it("renders the section heading with default title", () => {
    render(<StepsFriendly steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<StepsFriendly steps={STEPS} title="Easy Steps" />);
    expect(screen.getByRole("heading", { level: 2, name: "Easy Steps" })).toBeInTheDocument();
  });

  it("renders all step titles", () => {
    render(<StepsFriendly steps={STEPS} />);
    expect(screen.getByText("Submit Info")).toBeInTheDocument();
    expect(screen.getByText("Get Analysis")).toBeInTheDocument();
    expect(screen.getByText("Review Report")).toBeInTheDocument();
  });

  it("renders step descriptions", () => {
    render(<StepsFriendly steps={STEPS} />);
    expect(screen.getByText("Fill out a simple form")).toBeInTheDocument();
    expect(screen.getByText("We run the numbers")).toBeInTheDocument();
  });

  it("uses id=how-it-works for anchor linking", () => {
    const { container } = render(<StepsFriendly steps={STEPS} />);
    expect(container.querySelector("#how-it-works")).toBeInTheDocument();
  });

  it("renders step numbers", () => {
    render(<StepsFriendly steps={STEPS} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("uses warm soft rounded cards", () => {
    const { container } = render(<StepsFriendly steps={STEPS} />);
    const section = container.querySelector("#how-it-works");
    expect(section?.style.background).toBe("#FFF8F0");
  });

  it("renders subtitle when provided", () => {
    render(<StepsFriendly steps={STEPS} subtitle="Simple and easy" />);
    expect(screen.getByText("Simple and easy")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/steps/StepsFriendly.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement StepsFriendly**

```typescript
// apps/agent-site/components/sections/steps/StepsFriendly.tsx
import type { StepsProps } from "@/components/sections/types";

export function StepsFriendly({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="how-it-works"
      style={{
        padding: "70px 40px",
        background: "#FFF8F0",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 700,
          color: "#4A3728",
          marginBottom: subtitle ? "8px" : "40px",
        }}>
          {title ?? "How It Works"}
        </h2>
        {subtitle && (
          <p style={{
            textAlign: "center",
            color: "#8B7355",
            fontSize: "16px",
            marginBottom: "40px",
          }}>
            {subtitle}
          </p>
        )}
        <div style={{
          display: "flex",
          flexDirection: "column",
          gap: "20px",
        }}>
          {steps.map((step) => (
            <div
              key={step.title}
              style={{
                display: "flex",
                alignItems: "center",
                gap: "20px",
                background: "white",
                borderRadius: "16px",
                padding: "24px",
                boxShadow: "0 2px 8px rgba(0,0,0,0.06)",
              }}
            >
              <div style={{
                width: "44px",
                height: "44px",
                borderRadius: "12px",
                background: "var(--color-accent)",
                color: "white",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontWeight: 700,
                fontSize: "18px",
                flexShrink: 0,
              }}>
                {step.number}
              </div>
              <div>
                <h3 style={{
                  fontSize: "18px",
                  fontWeight: 700,
                  color: "#4A3728",
                  marginBottom: "4px",
                }}>
                  {step.title}
                </h3>
                <p style={{
                  fontSize: "14px",
                  color: "#8B7355",
                  lineHeight: 1.5,
                  margin: 0,
                }}>
                  {step.description}
                </p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Update steps barrel export**

Add to `apps/agent-site/components/sections/steps/index.ts`:
```typescript
export { StepsFriendly } from "./StepsFriendly";
```

- [ ] **Step 5: Run tests, commit**

```bash
git add apps/agent-site/components/sections/steps/ apps/agent-site/__tests__/components/steps/StepsFriendly.test.tsx
git commit -m "feat: add StepsFriendly variant for Warm Community template"
```

---

### Task 25: Create SoldCards variant

**Files:**
- Create: `apps/agent-site/components/sections/sold/SoldCards.tsx`
- Create: `apps/agent-site/__tests__/components/sold/SoldCards.test.tsx`
- Modify: `apps/agent-site/components/sections/sold/index.ts`

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/sold/SoldCards.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { SoldCards } from "@/components/sections/sold/SoldCards";
import type { SoldHomeItem } from "@/lib/types";

const ITEMS: SoldHomeItem[] = [
  {
    address: "123 Main St",
    city: "Springfield",
    state: "NJ",
    zip: "07001",
    price: "$750,000",
    image_url: "/homes/home1.jpg",
  },
  {
    address: "456 Oak Ave",
    city: "Newark",
    state: "NJ",
    zip: "07002",
    price: "$500,000",
    image_url: "/homes/home2.jpg",
  },
];

describe("SoldCards", () => {
  it("renders the section heading with default title", () => {
    render(<SoldCards items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<SoldCards items={ITEMS} title="Happy Families" />);
    expect(screen.getByRole("heading", { level: 2, name: "Happy Families" })).toBeInTheDocument();
  });

  it("renders all sold home prices", () => {
    render(<SoldCards items={ITEMS} />);
    expect(screen.getByText("$750,000")).toBeInTheDocument();
    expect(screen.getByText("$500,000")).toBeInTheDocument();
  });

  it("renders sold home addresses", () => {
    render(<SoldCards items={ITEMS} />);
    expect(screen.getByText(/123 Main St/)).toBeInTheDocument();
    expect(screen.getByText(/456 Oak Ave/)).toBeInTheDocument();
  });

  it("uses id=sold for anchor linking", () => {
    const { container } = render(<SoldCards items={ITEMS} />);
    expect(container.querySelector("#sold")).toBeInTheDocument();
  });

  it("renders SOLD badges", () => {
    render(<SoldCards items={ITEMS} />);
    const badges = screen.getAllByText("SOLD");
    expect(badges.length).toBe(2);
  });

  it("renders images with alt text", () => {
    render(<SoldCards items={ITEMS} />);
    const images = screen.getAllByRole("img");
    expect(images.length).toBe(2);
    expect(images[0]).toHaveAttribute("alt", "123 Main St");
  });

  it("uses rounded cards with soft shadows", () => {
    const { container } = render(<SoldCards items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(2);
    expect(articles[0].style.borderRadius).toBeTruthy();
    expect(articles[0].style.boxShadow).toBeTruthy();
  });

  it("renders subtitle when provided", () => {
    render(<SoldCards items={ITEMS} subtitle="Homes I have helped sell" />);
    expect(screen.getByText("Homes I have helped sell")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/sold/SoldCards.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement SoldCards**

```typescript
// apps/agent-site/components/sections/sold/SoldCards.tsx
import Image from "next/image";
import type { SoldHomesProps } from "@/components/sections/types";

export function SoldCards({ items, title, subtitle }: SoldHomesProps) {
  return (
    <section
      id="sold"
      style={{
        padding: "70px 40px",
        background: "white",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 700,
          color: "#4A3728",
          marginBottom: subtitle ? "8px" : "40px",
        }}>
          {title ?? "Recently Sold"}
        </h2>
        {subtitle && (
          <p style={{
            textAlign: "center",
            color: "#8B7355",
            fontSize: "16px",
            marginBottom: "40px",
          }}>
            {subtitle}
          </p>
        )}
        <div style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
          gap: "24px",
        }}>
          {items.map((item) => (
            <article
              key={item.address}
              style={{
                background: "#FFF8F0",
                borderRadius: "16px",
                overflow: "hidden",
                boxShadow: "0 2px 12px rgba(0,0,0,0.06)",
                position: "relative",
              }}
            >
              {item.image_url && (
                <div style={{
                  position: "relative",
                  height: "200px",
                  borderRadius: "16px 16px 0 0",
                  overflow: "hidden",
                }}>
                  <Image
                    src={item.image_url}
                    alt={item.address}
                    fill
                    style={{ objectFit: "cover" }}
                  />
                </div>
              )}
              <span style={{
                position: "absolute",
                top: "12px",
                left: "12px",
                background: "var(--color-accent)",
                color: "white",
                padding: "4px 12px",
                borderRadius: "20px",
                fontSize: "12px",
                fontWeight: 700,
              }}>
                SOLD
              </span>
              <div style={{ padding: "20px" }}>
                <div style={{
                  fontSize: "22px",
                  fontWeight: 700,
                  color: "#4A3728",
                  marginBottom: "4px",
                }}>
                  {item.price}
                </div>
                <div style={{
                  fontSize: "14px",
                  color: "#8B7355",
                }}>
                  {item.address}, {item.city}, {item.state} {item.zip}
                </div>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Update sold barrel export**

Add to `apps/agent-site/components/sections/sold/index.ts`:
```typescript
export { SoldCards } from "./SoldCards";
```

- [ ] **Step 5: Run tests, commit**

```bash
git add apps/agent-site/components/sections/sold/ apps/agent-site/__tests__/components/sold/SoldCards.test.tsx
git commit -m "feat: add SoldCards variant for Warm Community template"
```

---

### Task 26: Create TestimonialsBubble variant

**Files:**
- Create: `apps/agent-site/components/sections/testimonials/TestimonialsBubble.tsx`
- Create: `apps/agent-site/__tests__/components/testimonials/TestimonialsBubble.test.tsx`
- Modify: `apps/agent-site/components/sections/testimonials/index.ts`

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/testimonials/TestimonialsBubble.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { TestimonialsBubble } from "@/components/sections/testimonials/TestimonialsBubble";
import type { TestimonialItem } from "@/lib/types";

const ITEMS: TestimonialItem[] = [
  { reviewer: "Alice M.", text: "Wonderful experience!", rating: 5, source: "Zillow" },
  { reviewer: "Bob K.", text: "Very professional.", rating: 4, source: "Google" },
];

describe("TestimonialsBubble", () => {
  it("renders the default title", () => {
    render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "What My Clients Say" })).toBeInTheDocument();
  });

  it("renders a custom title", () => {
    render(<TestimonialsBubble items={ITEMS} title="Kind Words" />);
    expect(screen.getByRole("heading", { level: 2, name: "Kind Words" })).toBeInTheDocument();
  });

  it("uses id=testimonials for anchor linking", () => {
    const { container } = render(<TestimonialsBubble items={ITEMS} />);
    expect(container.querySelector("#testimonials")).toBeInTheDocument();
  });

  it("renders all testimonial texts", () => {
    render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByText("Wonderful experience!")).toBeInTheDocument();
    expect(screen.getByText("Very professional.")).toBeInTheDocument();
  });

  it("renders reviewer names", () => {
    render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByText(/Alice M\./)).toBeInTheDocument();
    expect(screen.getByText(/Bob K\./)).toBeInTheDocument();
  });

  it("renders star ratings with aria labels", () => {
    render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByLabelText("5 out of 5 stars")).toBeInTheDocument();
    expect(screen.getByLabelText("4 out of 5 stars")).toBeInTheDocument();
  });

  it("renders source attribution", () => {
    render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByText(/via Zillow/)).toBeInTheDocument();
    expect(screen.getByText(/via Google/)).toBeInTheDocument();
  });

  it("includes FTC disclaimer text", () => {
    render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByText(/No compensation was provided/)).toBeInTheDocument();
  });

  it("uses warm background", () => {
    const { container } = render(<TestimonialsBubble items={ITEMS} />);
    const section = container.querySelector("#testimonials");
    expect(section?.style.background).toBe("#FFF8F0");
  });

  it("renders avatar circles for reviewers", () => {
    const { container } = render(<TestimonialsBubble items={ITEMS} />);
    // Each testimonial has an avatar circle — check initials
    expect(screen.getByText("A")).toBeInTheDocument(); // Alice M.
    expect(screen.getByText("B")).toBeInTheDocument(); // Bob K.
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/testimonials/TestimonialsBubble.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement TestimonialsBubble**

```typescript
// apps/agent-site/components/sections/testimonials/TestimonialsBubble.tsx
import type { TestimonialsProps } from "@/components/sections/types";

export function TestimonialsBubble({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "#FFF8F0",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 700,
          color: "#4A3728",
          marginBottom: "8px",
        }}>
          {title ?? "What My Clients Say"}
        </h2>
        <p style={{
          textAlign: "center",
          color: "#B0A090",
          fontSize: "12px",
          marginBottom: "45px",
        }}>
          Real reviews from real clients. Unedited excerpts from verified reviews on Zillow.
          No compensation was provided. Individual results may vary.
        </p>
        <div style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
          gap: "28px",
        }}>
          {items.map((item) => (
            <div key={item.reviewer}>
              {/* Speech bubble */}
              <article style={{
                background: "white",
                borderRadius: "20px",
                padding: "24px",
                position: "relative",
                boxShadow: "0 2px 12px rgba(0,0,0,0.06)",
                marginBottom: "16px",
              }}>
                <span
                  role="img"
                  aria-label={`${item.rating} out of 5 stars`}
                  style={{
                    display: "block",
                    color: "var(--color-accent)",
                    fontSize: "16px",
                    marginBottom: "10px",
                  }}
                >
                  {"★".repeat(item.rating)}{"☆".repeat(5 - item.rating)}
                </span>
                <p style={{
                  fontStyle: "italic",
                  color: "#6B5A4A",
                  fontSize: "14px",
                  lineHeight: 1.7,
                  margin: 0,
                }}>
                  {item.text}
                </p>
                {/* Bubble tail */}
                <div style={{
                  position: "absolute",
                  bottom: "-8px",
                  left: "24px",
                  width: "16px",
                  height: "16px",
                  background: "white",
                  transform: "rotate(45deg)",
                  boxShadow: "2px 2px 4px rgba(0,0,0,0.04)",
                }} />
              </article>
              {/* Reviewer info below bubble */}
              <div style={{
                display: "flex",
                alignItems: "center",
                gap: "12px",
                paddingLeft: "12px",
              }}>
                <div style={{
                  width: "36px",
                  height: "36px",
                  borderRadius: "50%",
                  background: "var(--color-accent)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  color: "white",
                  fontWeight: 700,
                  fontSize: "14px",
                }}>
                  {item.reviewer.charAt(0)}
                </div>
                <div>
                  <div style={{
                    fontWeight: 700,
                    color: "#4A3728",
                    fontSize: "14px",
                  }}>
                    {item.reviewer}
                  </div>
                  {item.source && (
                    <div style={{ color: "#B0A090", fontSize: "12px" }}>
                      via {item.source}
                    </div>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Update testimonials barrel export**

Add to `apps/agent-site/components/sections/testimonials/index.ts`:
```typescript
export { TestimonialsBubble } from "./TestimonialsBubble";
```

- [ ] **Step 5: Run tests, commit**

```bash
git add apps/agent-site/components/sections/testimonials/ apps/agent-site/__tests__/components/testimonials/TestimonialsBubble.test.tsx
git commit -m "feat: add TestimonialsBubble variant for Warm Community template"
```

---

### Task 27: Create AboutCard variant

**Files:**
- Create: `apps/agent-site/components/sections/about/AboutCard.tsx`
- Create: `apps/agent-site/__tests__/components/about/AboutCard.test.tsx`
- Modify: `apps/agent-site/components/sections/about/index.ts`

- [ ] **Step 1: Write the test file**

```typescript
// apps/agent-site/__tests__/components/about/AboutCard.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutCard } from "@/components/sections/about/AboutCard";
import { AGENT } from "../fixtures";
import type { AboutData } from "@/lib/types";

const ABOUT_DATA: AboutData = {
  bio: "I love helping families find their dream home.",
  credentials: ["Licensed REALTOR", "Certified Negotiation Expert"],
};

describe("AboutCard", () => {
  it("renders the agent name in heading", () => {
    render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByRole("heading", { level: 2, name: /About/ })).toBeInTheDocument();
  });

  it("renders the bio text", () => {
    render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByText("I love helping families find their dream home.")).toBeInTheDocument();
  });

  it("uses id=about for anchor linking", () => {
    const { container } = render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    expect(container.querySelector("#about")).toBeInTheDocument();
  });

  it("renders agent photo when available", () => {
    render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    const img = screen.queryByRole("img");
    if (AGENT.identity.headshot_url) {
      expect(img).toBeInTheDocument();
    }
  });

  it("renders credentials as badges", () => {
    render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByText("Licensed REALTOR")).toBeInTheDocument();
    expect(screen.getByText("Certified Negotiation Expert")).toBeInTheDocument();
  });

  it("uses rounded card layout with shadow", () => {
    const { container } = render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    const section = container.querySelector("#about");
    // The section contains a card with rounded corners
    const card = section?.querySelector("[style*='border-radius']");
    expect(card).toBeInTheDocument();
  });

  it("renders phone number when available", () => {
    render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    if (AGENT.identity.phone) {
      expect(screen.getByText(new RegExp(AGENT.identity.phone.replace(/[()]/g, "\\$&")))).toBeInTheDocument();
    }
  });

  it("renders email when available", () => {
    render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    if (AGENT.identity.email) {
      expect(screen.getByText(AGENT.identity.email)).toBeInTheDocument();
    }
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/about/AboutCard.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement AboutCard**

```typescript
// apps/agent-site/components/sections/about/AboutCard.tsx
import Image from "next/image";
import type { AboutProps } from "@/components/sections/types";

export function AboutCard({ agent, data }: AboutProps) {
  return (
    <section
      id="about"
      style={{
        padding: "70px 40px",
        background: "#FFF8F0",
      }}
    >
      <div style={{
        maxWidth: "700px",
        margin: "0 auto",
        background: "white",
        borderRadius: "24px",
        padding: "48px",
        boxShadow: "0 4px 20px rgba(0,0,0,0.06)",
        textAlign: "center",
      }}>
        {agent.identity.headshot_url && (
          <div style={{
            width: "140px",
            height: "140px",
            borderRadius: "50%",
            overflow: "hidden",
            margin: "0 auto 24px",
            border: "4px solid var(--color-accent)",
          }}>
            <Image
              src={agent.identity.headshot_url}
              alt={`Photo of ${agent.identity.name}`}
              width={140}
              height={140}
              style={{ width: "100%", height: "100%", objectFit: "cover" }}
            />
          </div>
        )}
        <h2 style={{
          fontSize: "28px",
          fontWeight: 700,
          color: "#4A3728",
          marginBottom: "16px",
        }}>
          About {agent.identity.name}
        </h2>
        {Array.isArray(data.bio) ? (
          data.bio.map((paragraph, i) => (
            <p key={i} style={{ fontSize: "16px", color: "#6B5A4A", lineHeight: 1.7, marginBottom: "12px" }}>
              {paragraph}
            </p>
          ))
        ) : (
          <p style={{
            fontSize: "16px",
            color: "#6B5A4A",
            lineHeight: 1.7,
            marginBottom: "24px",
          }}>
            {data.bio}
          </p>
        )}
        {data.credentials && data.credentials.length > 0 && (
          <div style={{
            display: "flex",
            flexWrap: "wrap",
            justifyContent: "center",
            gap: "8px",
            marginBottom: "24px",
          }}>
            {data.credentials.map((cred) => (
              <span
                key={cred}
                style={{
                  background: "#FFF0E0",
                  color: "#8B6914",
                  padding: "6px 16px",
                  borderRadius: "20px",
                  fontSize: "13px",
                  fontWeight: 600,
                }}
              >
                {cred}
              </span>
            ))}
          </div>
        )}
        <div style={{
          fontSize: "14px",
          color: "#8B7355",
          lineHeight: 2,
        }}>
          {agent.identity.phone && <div>{agent.identity.phone}</div>}
          {agent.identity.email && <div>{agent.identity.email}</div>}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Update about barrel export**

Add to `apps/agent-site/components/sections/about/index.ts`:
```typescript
export { AboutCard } from "./AboutCard";
```

- [ ] **Step 5: Run tests, commit**

```bash
git add apps/agent-site/components/sections/about/ apps/agent-site/__tests__/components/about/AboutCard.test.tsx
git commit -m "feat: add AboutCard variant for Warm Community template"
```

---

### Task 28: Create WarmCommunity template and register it

Same pattern as Task 20 (ModernMinimal template) but for Warm Community.

**Files:**
- Create: `apps/agent-site/templates/warm-community.tsx`
- Create: `apps/agent-site/__tests__/templates/warm-community.test.tsx`
- Modify: `apps/agent-site/templates/index.ts`
- Modify: `apps/agent-site/__tests__/templates/index.test.ts`
- Modify: `apps/agent-site/components/sections/index.ts`

- [ ] **Step 1: Update sections barrel to export Warm Community variants**

Add to `apps/agent-site/components/sections/index.ts`:
```typescript
export { HeroCentered } from "./heroes/HeroCentered";
export { StatsInline } from "./stats/StatsInline";
export { ServicesIcons } from "./services/ServicesIcons";
export { StepsFriendly } from "./steps/StepsFriendly";
export { SoldCards } from "./sold/SoldCards";
export { TestimonialsBubble } from "./testimonials/TestimonialsBubble";
export { AboutCard } from "./about/AboutCard";
```

- [ ] **Step 2: Write WarmCommunity template test**

```typescript
// apps/agent-site/__tests__/templates/warm-community.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { WarmCommunity } from "@/templates/warm-community";
import { AGENT, CONTENT, CONTENT_ALL_DISABLED } from "../components/fixtures";

vi.mock("next/script", () => ({
  __esModule: true,
  default: ({ id, src }: { id?: string; src: string }) => (
    <script data-testid={id} data-src={src} />
  ),
}));

describe("WarmCommunity template", () => {
  it("always renders the Nav", () => {
    render(<WarmCommunity agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("navigation", { name: "Main navigation" })).toBeInTheDocument();
  });

  it("always renders the Footer", () => {
    render(<WarmCommunity agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("contentinfo")).toBeInTheDocument();
  });

  it("renders Hero section when enabled", () => {
    render(<WarmCommunity agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });

  it("does not render Hero when disabled", () => {
    render(<WarmCommunity agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
  });

  it("renders all sections when all enabled", () => {
    render(<WarmCommunity agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Submit Info")).toBeInTheDocument();
    expect(screen.getByText("$750,000")).toBeInTheDocument();
    expect(screen.getByText("Amazing service!")).toBeInTheDocument();
    expect(screen.getByText(/About Jane Smith/)).toBeInTheDocument();
  });

  it("does not render disabled sections", () => {
    render(<WarmCommunity agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
    expect(screen.queryByText("Homes Sold")).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Implement WarmCommunity template**

```typescript
// apps/agent-site/templates/warm-community.tsx
import type { AgentConfig, AgentContent } from "@/lib/types";
import { Nav } from "@/components/Nav";
import { Analytics } from "@/components/Analytics";
import {
  HeroCentered,
  StatsInline,
  ServicesIcons,
  StepsFriendly,
  SoldCards,
  TestimonialsBubble,
  CmaForm,
  AboutCard,
  Footer,
} from "@/components/sections";

interface TemplateProps {
  agent: AgentConfig;
  content: AgentContent;
}

export function WarmCommunity({ agent, content }: TemplateProps) {
  const s = content.sections;
  return (
    <>
      <Analytics tracking={agent.integrations?.tracking} />
      <Nav agent={agent} />
      <div style={{ paddingTop: "0" }}>
      {s.hero.enabled && (
        <HeroCentered
          data={s.hero.data}
          agentPhotoUrl={agent.identity.headshot_url}
          agentName={agent.identity.name}
        />
      )}
      {s.stats.enabled && s.stats.data.items.length > 0 && (
        <StatsInline items={s.stats.data.items} sourceDisclaimer="Based on data from Zillow. Individual results may vary." />
      )}
      {s.services.enabled && (
        <ServicesIcons
          items={s.services.data.items}
          title={s.services.data.title}
          subtitle={s.services.data.subtitle}
        />
      )}
      {s.how_it_works.enabled && (
        <StepsFriendly
          steps={s.how_it_works.data.steps}
          title={s.how_it_works.data.title}
          subtitle={s.how_it_works.data.subtitle}
        />
      )}
      {s.sold_homes.enabled && s.sold_homes.data.items.length > 0 && (
        <SoldCards
          items={s.sold_homes.data.items}
          title={s.sold_homes.data.title}
          subtitle={s.sold_homes.data.subtitle}
        />
      )}
      {s.testimonials.enabled && s.testimonials.data.items.length > 0 && (
        <TestimonialsBubble
          items={s.testimonials.data.items}
          title={s.testimonials.data.title}
        />
      )}
      {s.cma_form.enabled && (
        <CmaForm
          agentId={agent.id}
          agentName={agent.identity.name}
          defaultState={agent.location.state}
          formHandler={agent.integrations?.form_handler}
          formHandlerId={agent.integrations?.form_handler_id}
          tracking={agent.integrations?.tracking}
          data={s.cma_form.data}
          serviceAreas={agent.location.service_areas}
        />
      )}
      {s.about.enabled && <AboutCard agent={agent} data={s.about.data} />}
      <Footer agent={agent} agentId={agent.id} />
      </div>
    </>
  );
}
```

- [ ] **Step 4: Register WarmCommunity in template registry**

Update `apps/agent-site/templates/index.ts`:

```typescript
import { EmeraldClassic } from "./emerald-classic";
import { ModernMinimal } from "./modern-minimal";
import { WarmCommunity } from "./warm-community";

export const TEMPLATES: Record<string, typeof EmeraldClassic> = {
  "emerald-classic": EmeraldClassic,
  "modern-minimal": ModernMinimal,
  "warm-community": WarmCommunity,
};

export function getTemplate(name: string) {
  return TEMPLATES[name] || EmeraldClassic;
}
```

- [ ] **Step 5: Update registry tests**

Add to `apps/agent-site/__tests__/templates/index.test.ts`:

```typescript
import { WarmCommunity } from "@/templates/warm-community";

it("returns WarmCommunity for 'warm-community'", () => {
  const Template = getTemplate("warm-community");
  expect(Template).toBe(WarmCommunity);
});

it("TEMPLATES registry contains warm-community key", () => {
  expect("warm-community" in TEMPLATES).toBe(true);
});

it("TEMPLATES registry has exactly 3 templates", () => {
  expect(Object.keys(TEMPLATES)).toHaveLength(3);
});
```

- [ ] **Step 6: Run full test suite**

Run: `cd apps/agent-site && npx vitest run`
Expected: All tests pass

- [ ] **Step 7: Commit**

```bash
git add apps/agent-site/templates/ apps/agent-site/__tests__/templates/ apps/agent-site/components/sections/index.ts
git commit -m "feat: add Warm Community template with all section variants"
```

---

### Task 29: Final verification

- [ ] **Step 1: Run full test suite**

Run: `cd apps/agent-site && npx vitest run`
Expected: All tests pass

- [ ] **Step 2: Run TypeScript check**

Run: `cd apps/agent-site && npx tsc --noEmit`
Expected: No new errors

- [ ] **Step 3: Run lint**

Run: `cd apps/agent-site && npx eslint .`
Expected: No new errors

- [ ] **Step 4: Build**

Run: `cd apps/agent-site && npm run build`
Expected: Build succeeds
