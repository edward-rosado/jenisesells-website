# Legal & ADA Compliance Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix all NJ legal compliance and WCAG 2.1 AA accessibility issues in the agent site.

**Architecture:** Component-level fixes across 15+ files. Each task is independent and can be parallelized. All changes are in `apps/agent-site/` except the shared LeadForm in `packages/ui/`.

**Tech Stack:** Next.js 16, React, Tailwind v4, TypeScript, Vitest

---

## File Structure

| File | Changes |
|------|---------|
| `apps/agent-site/app/globals.css` | Add `:focus-visible`, `prefers-reduced-motion` |
| `apps/agent-site/app/layout.tsx` | Change `<div>` to `<main>` |
| `apps/agent-site/components/Nav.tsx` | Focus trap, Escape key, aria-expanded, role="dialog" |
| `apps/agent-site/components/sections/Hero.tsx` | Focus-visible on CTA button |
| `apps/agent-site/components/sections/Footer.tsx` | Brokerage prominence, contrast, license # |
| `apps/agent-site/components/sections/CmaForm.tsx` | Section wrapper, TCPA checkbox, CMA disclaimer, aria-live, outline fix |
| `apps/agent-site/components/sections/Testimonials.tsx` | Star rating a11y, article tags, review disclosure |
| `apps/agent-site/components/sections/HowItWorks.tsx` | Ordered list semantics |
| `apps/agent-site/components/sections/StatsBar.tsx` | Description list semantics |
| `apps/agent-site/components/sections/About.tsx` | Credentials list semantics |
| `apps/agent-site/components/sections/Services.tsx` | Minor contrast tweak |
| `apps/agent-site/components/sections/SoldHomes.tsx` | Article tags, price aria-label |
| `apps/agent-site/app/terms/page.tsx` | NJ LAD notice, NJ Fair Housing, license info |
| `apps/agent-site/app/privacy/page.tsx` | NJ privacy rights, data retention, TCPA, third-party services |
| `apps/agent-site/app/thank-you/page.tsx` | CMA disclaimer |
| `config/agents/jenise-buckalew/config.json` | Add license_type field |

---

## Task 1: Global CSS — Focus Indicators & Reduced Motion

**Files:**
- Modify: `apps/agent-site/app/globals.css`
- Test: `apps/agent-site/__tests__/pages/accessibility.test.tsx` (verify skip-nav still works)

**Why:** Without `:focus-visible`, keyboard users see NO focus indicator on any interactive element. This is WCAG 2.4.7 (Focus Visible) — a hard AA requirement.

- [ ] **Step 1: Add `:focus-visible` rule to globals.css**

Add after the existing `.skip-nav:focus` block:

```css
/* Global focus indicator for keyboard navigation (WCAG 2.4.7) */
:focus-visible {
  outline: 3px solid #2E7D32;
  outline-offset: 2px;
}

/* Remove default outline only when focus-visible is supported */
:focus:not(:focus-visible) {
  outline: none;
}
```

- [ ] **Step 2: Add `prefers-reduced-motion` media query**

Add at the end of globals.css:

```css
/* Respect user motion preferences (WCAG 2.3.3) */
@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
    scroll-behavior: auto !important;
  }
}
```

- [ ] **Step 3: Run tests**

```bash
npm run test:coverage --prefix apps/agent-site
```

- [ ] **Step 4: Commit**

```bash
git add apps/agent-site/app/globals.css
git commit -m "fix(a11y): add global focus-visible indicator and prefers-reduced-motion"
```

---

## Task 2: Layout — Semantic Main Element

**Files:**
- Modify: `apps/agent-site/app/layout.tsx`
- Test: existing layout tests

- [ ] **Step 1: Change `<div id="main-content">` to `<main id="main-content">`**

Replace the `<div id="main-content" tabIndex={-1}>` with `<main id="main-content" tabIndex={-1}>`. Update closing tag too.

- [ ] **Step 2: Update any tests that query for `div#main-content`**

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/app/layout.tsx
git commit -m "fix(a11y): use semantic main element instead of div"
```

---

## Task 3: Nav — Focus Trap, Escape Key, ARIA Attributes

**Files:**
- Modify: `apps/agent-site/components/Nav.tsx`
- Test: `apps/agent-site/__tests__/components/Nav.test.tsx`

**Why:** Mobile drawer is a modal-like component. Without focus trap and Escape key, keyboard users get stuck or can't close it.

- [ ] **Step 1: Add `aria-expanded` to hamburger button**

On the hamburger `<button>`, add `aria-expanded={drawerOpen}`.

- [ ] **Step 2: Add `role="dialog"` and `aria-modal="true"` to drawer container**

On the drawer div, add `role="dialog"` `aria-modal="true"` `aria-label="Navigation menu"`.

- [ ] **Step 3: Add Escape key handler**

Add a `useEffect` that listens for `keydown` event. When `Escape` is pressed and `drawerOpen` is true, call `setDrawerOpen(false)`.

```tsx
useEffect(() => {
  if (!drawerOpen) return;
  const handleKeyDown = (e: KeyboardEvent) => {
    if (e.key === "Escape") setDrawerOpen(false);
  };
  document.addEventListener("keydown", handleKeyDown);
  return () => document.removeEventListener("keydown", handleKeyDown);
}, [drawerOpen]);
```

- [ ] **Step 4: Add focus trap**

When drawer opens, focus the first focusable element. When drawer closes, return focus to the hamburger button. Use a ref for the hamburger button and the drawer.

```tsx
const hamburgerRef = useRef<HTMLButtonElement>(null);
const drawerRef = useRef<HTMLDivElement>(null);

useEffect(() => {
  if (drawerOpen && drawerRef.current) {
    const firstFocusable = drawerRef.current.querySelector<HTMLElement>(
      'a, button, [tabindex]:not([tabindex="-1"])'
    );
    firstFocusable?.focus();
  } else if (!drawerOpen) {
    hamburgerRef.current?.focus();
  }
}, [drawerOpen]);
```

- [ ] **Step 5: Add `aria-hidden="true"` to overlay div**

The backdrop overlay should have `aria-hidden="true"`.

- [ ] **Step 6: Write tests**

Test: Escape key closes drawer, aria-expanded toggles, focus moves to first link on open, focus returns to hamburger on close.

- [ ] **Step 7: Run coverage and commit**

```bash
npm run test:coverage --prefix apps/agent-site
git add apps/agent-site/components/Nav.tsx apps/agent-site/__tests__/components/Nav.test.tsx
git commit -m "fix(a11y): add focus trap, Escape key, and ARIA attributes to Nav drawer"
```

---

## Task 4: Hero — Focus Indicator on CTA Button

**Files:**
- Modify: `apps/agent-site/components/sections/Hero.tsx`
- Test: `apps/agent-site/__tests__/components/Hero.test.tsx`

- [ ] **Step 1: Add onFocus/onBlur handlers to CTA button**

The button currently only tracks hover. Add focus state so keyboard users see the same visual treatment:

```tsx
onFocus={() => setCtaHover(true)}
onBlur={() => setCtaHover(false)}
```

Or add a separate `ctaFocused` state if hover and focus should look different.

- [ ] **Step 2: Ensure focus ring is visible**

The global `:focus-visible` from Task 1 should handle this, but verify the button doesn't have `outline: none` inline.

- [ ] **Step 3: Write test for keyboard focus**

Test that tabbing to CTA button shows visual indicator (onFocus fires).

- [ ] **Step 4: Commit**

```bash
git add apps/agent-site/components/sections/Hero.tsx apps/agent-site/__tests__/components/Hero.test.tsx
git commit -m "fix(a11y): add keyboard focus indicator to Hero CTA button"
```

---

## Task 5: Footer — Brokerage Prominence, Contrast, License Number

**Files:**
- Modify: `apps/agent-site/components/sections/Footer.tsx`
- Modify: `config/agents/jenise-buckalew/config.json` (add license_type)
- Test: `apps/agent-site/__tests__/components/Footer.test.tsx`

**Why:** NJAC 11:5-6 requires brokerage name be equally prominent to agent name. Current: agent name is 22px, brokerage is 14px. Also, #A5D6A7 on #1B5E20 is ~2.8:1 contrast (needs 4.5:1).

- [ ] **Step 1: Increase brokerage name size and add license number**

Make brokerage name same font size as agent name. Display brokerage license number from config.

- [ ] **Step 2: Fix contrast — change light green text to white or #E8F5E9**

Change #A5D6A7 and #66BB6A text on #1B5E20 background to white (#FFFFFF) or very light green (#E8F5E9) for 4.5:1+ contrast.

- [ ] **Step 3: Add license_type to config**

In `config/agents/jenise-buckalew/config.json`, add `"license_type": "REALTOR"` in the identity section.

- [ ] **Step 4: Display license type dynamically in footer**

Instead of hardcoded "Licensed Real Estate Salesperson", read from `config.identity.license_type`.

- [ ] **Step 5: Write tests for brokerage display and contrast**

Test that brokerage name renders, license number renders, and text colors pass contrast.

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/components/sections/Footer.tsx config/agents/jenise-buckalew/config.json apps/agent-site/__tests__/components/Footer.test.tsx
git commit -m "fix(legal): brokerage prominence, contrast, and license display per NJAC 11:5-6"
```

---

## Task 6: CmaForm — A11y Fixes, TCPA Consent, CMA Disclaimer

**Files:**
- Modify: `apps/agent-site/components/sections/CmaForm.tsx`
- Test: `apps/agent-site/__tests__/components/CmaForm.test.tsx`

**Why:** Multiple critical issues: outline:none removes focus ring, no TCPA consent for phone collection, CMA disclaimer is tiny, error messages not announced to screen readers.

- [ ] **Step 1: Wrap form in `<section>` with proper ARIA**

Change outer `<div id="cma-form">` to `<section id="cma-form" aria-label="Home Value Request Form">`.

- [ ] **Step 2: Remove `outline: "none"` from input styles**

Delete the `outline: "none"` from inputStyle. The global `:focus-visible` from Task 1 will handle focus rings.

- [ ] **Step 3: Add TCPA consent checkbox**

Before the submit button, add:
```tsx
<label style={{ display: "flex", alignItems: "flex-start", gap: "8px", fontSize: "0.85rem", color: "#555" }}>
  <input type="checkbox" required name="tcpa_consent" />
  <span>I consent to receive calls, texts, and emails from {agent.identity.name} regarding real estate services. Message and data rates may apply. Reply STOP to opt out.</span>
</label>
```

- [ ] **Step 4: Make CMA disclaimer more prominent**

Change from 0.75rem italic to 0.85rem with clear styling. Add `role="note"`.

- [ ] **Step 5: Add `role="alert"` and `aria-live="polite"` to error messages**

Wrap the error display in `<div role="alert" aria-live="polite">`.

- [ ] **Step 6: Add `aria-required="true"` to required inputs**

For each required input, add `aria-required="true"`. Replace visual-only asterisks with `<span aria-hidden="true">*</span>` plus screen-reader text.

- [ ] **Step 7: Write tests**

Test: TCPA checkbox renders and is required, error messages have role="alert", section has aria-label, required inputs have aria-required.

- [ ] **Step 8: Run coverage and commit**

```bash
npm run test:coverage --prefix apps/agent-site
git add apps/agent-site/components/sections/CmaForm.tsx apps/agent-site/__tests__/components/CmaForm.test.tsx
git commit -m "fix(a11y+legal): form accessibility, TCPA consent, CMA disclaimer prominence"
```

---

## Task 7: Testimonials — A11y + FTC Disclosure

**Files:**
- Modify: `apps/agent-site/components/sections/Testimonials.tsx`
- Test: `apps/agent-site/__tests__/components/Testimonials.test.tsx`

- [ ] **Step 1: Fix star ratings for screen readers**

Wrap stars in `<span role="img" aria-label="{rating} out of 5 stars">`.

- [ ] **Step 2: Change testimonial cards to `<article>` elements**

Replace generic `<div>` cards with `<article>` tags.

- [ ] **Step 3: Add review disclosure header**

Add introductory text: "Verified customer reviews from Zillow. Individual results may vary."

- [ ] **Step 4: Write tests and commit**

```bash
git add apps/agent-site/components/sections/Testimonials.tsx apps/agent-site/__tests__/components/Testimonials.test.tsx
git commit -m "fix(a11y+legal): testimonial star ratings, article semantics, FTC disclosure"
```

---

## Task 8: Semantic HTML — HowItWorks, StatsBar, About, SoldHomes, Services

**Files:**
- Modify: `apps/agent-site/components/sections/HowItWorks.tsx` — ordered list
- Modify: `apps/agent-site/components/sections/StatsBar.tsx` — description list
- Modify: `apps/agent-site/components/sections/About.tsx` — credentials list
- Modify: `apps/agent-site/components/sections/SoldHomes.tsx` — article tags
- Modify: `apps/agent-site/components/sections/Services.tsx` — minor contrast
- Tests: corresponding test files for each

- [ ] **Step 1: HowItWorks — Convert to `<ol>` with `<li>` items**

Replace the flex container div with `<ol>` and each step div with `<li>`.

- [ ] **Step 2: StatsBar — Convert to `<dl>` with `<dt>`/`<dd>`**

Wrap stats in `<dl>`. Each stat value becomes `<dt>`, label becomes `<dd>`.

- [ ] **Step 3: About — Wrap credentials in `<ul>` with `<li>`**

Change credential spans to proper list items.

- [ ] **Step 4: SoldHomes — Change cards to `<article>`, add price aria-label**

- [ ] **Step 5: Services — Darken card border from #2E7D32 to #1B5E20**

- [ ] **Step 6: Update all tests for new semantic elements**

- [ ] **Step 7: Run coverage and commit**

```bash
npm run test:coverage --prefix apps/agent-site
git add apps/agent-site/components/sections/HowItWorks.tsx apps/agent-site/components/sections/StatsBar.tsx apps/agent-site/components/sections/About.tsx apps/agent-site/components/sections/SoldHomes.tsx apps/agent-site/components/sections/Services.tsx
git add apps/agent-site/__tests__/components/
git commit -m "fix(a11y): semantic HTML for HowItWorks, StatsBar, About, SoldHomes, Services"
```

---

## Task 9: Legal Pages — Terms, Privacy, Thank-You

**Files:**
- Modify: `apps/agent-site/app/terms/page.tsx`
- Modify: `apps/agent-site/app/privacy/page.tsx`
- Modify: `apps/agent-site/app/thank-you/page.tsx`
- Tests: `apps/agent-site/__tests__/pages/terms.test.tsx`, `privacy.test.tsx`

- [ ] **Step 1: Terms — Add NJ LAD notice**

Add section citing NJSA 10:5-1 et seq. with all NJ protected classes:
- Race, creed, color, national origin, nationality, ancestry
- Age, sex (including pregnancy), gender identity or expression
- Disability, marital status, domestic partnership/civil union status
- Affectional or sexual orientation, familial status
- Source of lawful income or rent payments
- Military service

- [ ] **Step 2: Terms — Add NJ Fair Housing Act reference**

Add reference to NJSA 10:5-12 alongside existing federal Fair Housing Act language.

- [ ] **Step 3: Terms — Add licensing info section**

Add: "Licensed by the New Jersey Real Estate Commission. Brokerage: {brokerage} (License #{brokerage_id}). Agent: {name} (License #{license_id})."

- [ ] **Step 4: Privacy — Add data retention period**

Add: "We retain form submission data for two years for record-keeping, then securely delete it."

- [ ] **Step 5: Privacy — List third-party services**

Add section listing: Formspree (form handling), Sentry (error tracking if applicable), Cloudflare (hosting), Google Maps (address autocomplete). Link to each provider's privacy policy.

- [ ] **Step 6: Privacy — Add TCPA disclosure**

Add: "Phone numbers collected through our forms are used solely to deliver requested services. We do not share phone numbers with third parties for marketing. You may opt out of communications at any time by contacting us."

- [ ] **Step 7: Thank-you — Add CMA disclaimer**

Add after the thank-you message:
"This home value report is a Comparative Market Analysis (CMA) and is not an appraisal. It should not be considered the equivalent of an appraisal."

- [ ] **Step 8: Update tests for new content**

- [ ] **Step 9: Commit**

```bash
git add apps/agent-site/app/terms/page.tsx apps/agent-site/app/privacy/page.tsx apps/agent-site/app/thank-you/page.tsx
git add apps/agent-site/__tests__/pages/
git commit -m "fix(legal): NJ LAD, TCPA, data retention, CMA disclaimers on legal pages"
```

---

## Parallelization Strategy

These tasks can be executed in parallel groups:

**Group A (CSS + Layout — no component deps):**
- Task 1: globals.css
- Task 2: layout.tsx

**Group B (Component a11y — independent components):**
- Task 3: Nav.tsx
- Task 4: Hero.tsx
- Task 5: Footer.tsx
- Task 6: CmaForm.tsx
- Task 7: Testimonials.tsx
- Task 8: Semantic HTML batch (5 components)

**Group C (Legal pages — independent from components):**
- Task 9: Terms, Privacy, Thank-You

All three groups can run in parallel. Within Group B, all tasks are independent.

---

## Verification

After all tasks complete:

```bash
# Run full test suite with coverage
npm run test:coverage --prefix apps/agent-site

# Run lint
npm run lint --prefix apps/agent-site

# Build to verify no errors
npm run build --prefix apps/agent-site
```

Expected: 100% branch coverage maintained, all tests pass, no lint errors.
