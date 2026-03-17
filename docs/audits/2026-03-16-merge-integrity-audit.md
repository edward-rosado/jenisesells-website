# Merge Integrity Audit: feat/compliance-and-ux-polish -> feat/cma-api-integration

**Date:** 2026-03-16
**Auditor:** Claude (automated)
**Merge commit:** `cd77f53`

## Context

The `feat/compliance-and-ux-polish` branch contained multi-template system changes including:
- Security hardening (HTTPS-only links, dev-only query params, rating clamping)
- Brand color consistency (CSS variables in Footer, CmaForm)
- Code quality (deduplicated TemplateProps, removed duplicate Analytics, semantic HTML)
- 100% test coverage (590 tests)

This branch was merged into `feat/cma-api-integration` at commit `cd77f53`. Six additional commits were then made on `cma-api-integration`:
- `b091aac` feat: consolidate CMA form submission into packages/ui, remove Formspree
- `7a97dec` fix: address code review findings for CMA integration
- `fd2de10` fix: buyer-only form submission, thank-you page UX, dev CORS
- `1b8f2bb` fix: thank-you page button sizing and checkmark styling
- `e965ebd` docs: add architecture diagrams
- `270f3ff` fix: CI failures

This audit checks whether the compliance branch changes survived the merge and post-merge work.

---

## Results Summary

| Status | Count |
|--------|-------|
| PASS   | 23    |
| FAIL   | 3     |
| WARN   | 1     |

---

## FAIL: Issues Found

### FAIL 1: Analytics rendered twice (double-tracking)

**Files:** `templates/emerald-classic.tsx`, `templates/modern-minimal.tsx`, `templates/warm-community.tsx`

The compliance branch removed `<Analytics>` from all 3 template files because `page.tsx` already renders it. The post-merge CMA consolidation commits **re-introduced** `<Analytics>` in all 3 templates, causing every pageview to fire analytics twice.

**Current state (broken):**
- `page.tsx` line 77: `<Analytics tracking={agent.integrations?.tracking} />`
- `emerald-classic.tsx`: `<Analytics tracking={agent.integrations?.tracking} />`
- `modern-minimal.tsx`: `<Analytics tracking={agent.integrations?.tracking} />`
- `warm-community.tsx`: `<Analytics tracking={agent.integrations?.tracking} />`

**Expected state:**
- Analytics ONLY in `page.tsx` (single rendering point)
- Templates should NOT import or render `<Analytics>`

**Impact:** Every visitor is double-counted in Google Analytics / Meta Pixel / GTM.

---

### FAIL 2: CmaSection.tsx uses hardcoded colors (brand consistency broken)

**File:** `apps/agent-site/components/sections/shared/CmaSection.tsx`

The compliance branch changed `CmaForm.tsx` to use CSS variables:
- Heading: `var(--color-primary)` instead of `#1B5E20`
- Subtitle: `var(--color-accent)` instead of `#C8A951`
- Background: `#f7f7f7` instead of gradient with emerald tints

The post-merge CMA consolidation replaced `CmaForm.tsx` with a new `CmaSection.tsx`, but the new file was written with **hardcoded colors** instead of CSS variables.

**Current state (broken):**
```tsx
// CmaSection.tsx
color: "#1B5E20"      // heading — should be var(--color-primary)
color: "#C8A951"       // subtitle — should be var(--color-accent)
background: "linear-gradient(135deg, #E8F5E9, #C8E6C9)"  // should be neutral #f7f7f7
```

**Expected state:**
```tsx
color: "var(--color-primary)"
color: "var(--color-accent)"
background: "#f7f7f7"
```

**Impact:** Modern Minimal and Warm Community templates render the CMA form section with Emerald Classic green/gold colors instead of their own brand palette.

---

### FAIL 3: Same as FAIL 1

(Listed separately in template structure validation — same root cause.)

---

## WARN: Review Recommended

### WARN 1: TestimonialsGrid uses hardcoded colors

**File:** `apps/agent-site/components/sections/testimonials/TestimonialsGrid.tsx`

Uses hardcoded `#1B5E20` (heading, reviewer name) and `#C8A951` (stars) instead of CSS variables. The other two testimonial variants (`TestimonialsBubble`, `TestimonialsClean`) correctly use `var(--color-accent)` for stars.

This is a pre-existing issue (not caused by the merge), but it breaks brand consistency. `TestimonialsGrid` is used by the Emerald Classic template, which currently only has emerald-themed agents, so it works visually — but it won't adapt if an agent with different brand colors uses this template.

**Low priority** — only affects Emerald Classic agents who happen to override brand colors from the emerald default.

---

## PASS: All Verified

### Security Hardening
- `resolveAgentId()` production guard in page.tsx
- `resolveTemplateOverride()` production guard in page.tsx
- `safeHref()` blocks http:, javascript:, data: protocols
- `clampRating()` function in types.ts
- `FTC_DISCLAIMER` constant in types.ts
- All 3 testimonial variants use `clampRating()` and `FTC_DISCLAIMER`

### Brand Colors
- Footer uses `var(--color-primary)` and `var(--color-accent)`

### Code Quality
- `TemplateProps` in `templates/types.ts` (not duplicated)
- SoldCards composite key (`address-city`)
- SoldCards `sizes` attribute present
- AboutCard uses `data.title` fallback
- StepsFriendly uses semantic `<ol>/<li>`

### CMA Consolidation
- Old `cma-api.ts` and `useCmaSubmit.ts` removed from agent-site
- New `packages/ui/cma/` package with tests
- `CmaSection.tsx` exists and functions
- Barrel exports updated (`shared/index.ts` exports CmaSection)

### Test Coverage
- 100% thresholds maintained in vitest.config.ts
- Coverage includes/excludes correct

### Cross-File Consistency
- All template imports match barrel exports
- Template registry correctly maps all 3 templates
- Fallback to EmeraldClassic works

---

## Recommended Fixes

1. **Remove `<Analytics>` from all 3 template files** — delete the import and JSX from emerald-classic.tsx, modern-minimal.tsx, warm-community.tsx. Analytics is already rendered by page.tsx.

2. **Update CmaSection.tsx colors to CSS variables** — replace `#1B5E20` with `var(--color-primary)`, `#C8A951` with `var(--color-accent)`, and the gradient background with `#f7f7f7`.

3. **(Optional) Update TestimonialsGrid.tsx** — replace hardcoded colors with CSS variables for future brand flexibility.
