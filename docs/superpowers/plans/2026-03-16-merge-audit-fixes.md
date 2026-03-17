# Merge Audit Fix Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 3 regressions on `feat/cma-api-integration` identified by the merge integrity audit.

**Architecture:** Minimal, targeted fixes — remove duplicate Analytics from templates, swap hardcoded colors for CSS variables in CmaSection and TestimonialsGrid. No new components or patterns.

**Tech Stack:** Next.js 16, React, Vitest, CSS custom properties

**Branch:** `feat/cma-api-integration`

**Audit report:** `docs/audits/2026-03-16-merge-integrity-audit.md`

---

## Chunk 1: Fix All Issues

### Task 1: Remove duplicate Analytics from all 3 templates

The post-merge CMA commits re-introduced `<Analytics>` in each template file. `page.tsx` already renders `<Analytics tracking={agent.integrations?.tracking} />` at line 77, so every visitor gets double-counted.

**Files:**
- Modify: `apps/agent-site/templates/emerald-classic.tsx`
- Modify: `apps/agent-site/templates/modern-minimal.tsx`
- Modify: `apps/agent-site/templates/warm-community.tsx`

- [ ] **Step 1: Remove Analytics from emerald-classic.tsx**

Remove the `import { Analytics } from "@/components/Analytics";` line and the `<Analytics tracking={agent.integrations?.tracking} />` JSX line.

Before:
```tsx
import { Nav } from "@/components/Nav";
import { Analytics } from "@/components/Analytics";
import { Hero, StatsBar, Services, HowItWorks, SoldHomes, Testimonials, CmaSection, About, Footer } from "@/components/sections";
import type { TemplateProps } from "./types";

export function EmeraldClassic({ agent, content }: TemplateProps) {
  const s = content.sections;
  return (
    <>
      <Analytics tracking={agent.integrations?.tracking} />
      <Nav agent={agent} />
```

After:
```tsx
import { Nav } from "@/components/Nav";
import { Hero, StatsBar, Services, HowItWorks, SoldHomes, Testimonials, CmaSection, About, Footer } from "@/components/sections";
import type { TemplateProps } from "./types";

export function EmeraldClassic({ agent, content }: TemplateProps) {
  const s = content.sections;
  return (
    <>
      <Nav agent={agent} />
```

- [ ] **Step 2: Remove Analytics from modern-minimal.tsx**

Same pattern — remove the `Analytics` import and JSX.

Before:
```tsx
import { Nav } from "@/components/Nav";
import { Analytics } from "@/components/Analytics";
import {
  HeroSplit,
  ...
} from "@/components/sections";
```

After:
```tsx
import { Nav } from "@/components/Nav";
import {
  HeroSplit,
  ...
} from "@/components/sections";
```

And remove `<Analytics tracking={agent.integrations?.tracking} />` from the JSX.

- [ ] **Step 3: Remove Analytics from warm-community.tsx**

Same pattern as step 2.

- [ ] **Step 4: Run tests to verify nothing breaks**

Run: `cd apps/agent-site && npx vitest run`
Expected: All tests pass. The emerald-classic template test no longer has Analytics-specific tests (those were already removed in the compliance branch and should not be present on cma-api-integration either — verified: they are not present).

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/templates/emerald-classic.tsx apps/agent-site/templates/modern-minimal.tsx apps/agent-site/templates/warm-community.tsx
git commit -m "fix: remove duplicate Analytics from templates (already rendered by page.tsx)"
```

---

### Task 2: Fix CmaSection.tsx hardcoded colors

The CMA consolidation created a new `CmaSection.tsx` with hardcoded emerald colors. The compliance branch had already fixed the predecessor `CmaForm.tsx` to use CSS variables.

**Files:**
- Modify: `apps/agent-site/components/sections/shared/CmaSection.tsx`

- [ ] **Step 1: Replace hardcoded heading color**

In `CmaSection.tsx`, change the h2 style:

Before:
```tsx
color: "#1B5E20",
```

After:
```tsx
color: "var(--color-primary)",
```

- [ ] **Step 2: Replace hardcoded subtitle color**

Change the subtitle `<p>` style:

Before:
```tsx
color: "#C8A951",
```

After:
```tsx
color: "var(--color-accent)",
```

- [ ] **Step 3: Replace hardcoded gradient background**

Change the `<section>` background:

Before:
```tsx
background: "linear-gradient(135deg, #E8F5E9, #C8E6C9)",
```

After:
```tsx
background: "#f7f7f7",
```

This uses a neutral light gray that works with any template's color scheme, matching the pattern established by the compliance branch.

- [ ] **Step 4: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run`
Expected: All tests pass. The CmaSection tests mock the component's behavior, not its inline styles, so no test changes needed.

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/shared/CmaSection.tsx
git commit -m "fix: CmaSection uses CSS variables for brand colors instead of hardcoded emerald"
```

---

### Task 3: Fix TestimonialsGrid hardcoded colors

`TestimonialsGrid` is the only testimonial variant still using hardcoded colors. The other two (`TestimonialsBubble`, `TestimonialsClean`) already use `var(--color-accent)`.

**Files:**
- Modify: `apps/agent-site/components/sections/testimonials/TestimonialsGrid.tsx`

- [ ] **Step 1: Replace hardcoded heading color**

Change the h2 style:

Before:
```tsx
color: "#1B5E20",
```

After:
```tsx
color: "var(--color-primary)",
```

- [ ] **Step 2: Replace hardcoded star color**

Change the star `<span>` style:

Before:
```tsx
color: "#C8A951",
```

After:
```tsx
color: "var(--color-accent)",
```

- [ ] **Step 3: Replace hardcoded reviewer name color**

Change the reviewer `<div>` style:

Before:
```tsx
color: "#1B5E20",
```

After:
```tsx
color: "var(--color-primary)",
```

- [ ] **Step 4: Run tests to verify**

Run: `cd apps/agent-site && npx vitest run`
Expected: All tests pass.

- [ ] **Step 5: Run full coverage check**

Run: `cd apps/agent-site && npx vitest run --coverage`
Expected: 100% across all metrics (statements, branches, functions, lines).

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/components/sections/testimonials/TestimonialsGrid.tsx
git commit -m "fix: TestimonialsGrid uses CSS variables for brand color consistency"
```

---

## Verification

After all 3 tasks:

- [ ] **Run full test suite with coverage**

Run: `cd apps/agent-site && npx vitest run --coverage`
Expected: All tests pass, 100% coverage.

- [ ] **Verify no hardcoded emerald colors remain in shared or cross-template components**

Run: `grep -rn "#1B5E20\|#C8A951" apps/agent-site/components/sections/shared/ apps/agent-site/components/sections/testimonials/ apps/agent-site/components/Nav.tsx`
Expected: No matches. (Emerald Classic-only variants like `ServicesGrid`, `StatsBar`, `SoldGrid` may still have them — that's intentional since those are only used by the emerald-classic template.)

- [ ] **Verify no Analytics import in any template file**

Run: `grep -rn "Analytics" apps/agent-site/templates/`
Expected: No matches.
