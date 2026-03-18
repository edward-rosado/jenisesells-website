# ADA & Security Remediation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix all Critical/High ADA (WCAG 2.1 AA) and Security findings from the March 18, 2026 audit of the agent-site templates and section components.

**Architecture:** The agent-site uses Next.js 16 with inline styles, CSS variables for branding, and a multi-template system (emerald-classic, modern-minimal, warm-community). All templates compose shared section components. Changes are surgical edits to existing components — no new architecture. A shared `useFocusTrap` hook is introduced for modal focus management, and a `safeEmail` utility extends the existing `safeHref` pattern.

**Tech Stack:** Next.js 16, React 19, Vitest, @testing-library/react

---

## File Map

### New Files
| File | Responsibility |
|------|---------------|
| `apps/agent-site/lib/use-focus-trap.ts` | Reusable focus trap hook for modals/drawers |
| `apps/agent-site/__tests__/lib/use-focus-trap.test.ts` | Tests for focus trap hook |
| `apps/agent-site/lib/safe-contact.ts` | `safeEmail()` and `safeTel()` validators (mirrors `safeHref` pattern) |
| `apps/agent-site/__tests__/lib/safe-contact.test.ts` | Tests for contact validators |
| `apps/agent-site/lib/security-headers.ts` | `applySecurityHeaders(response)` helper |

### Modified Files
| File | Changes |
|------|---------|
| `apps/agent-site/components/Nav.tsx` | Focus trap on drawer, dynamic aria-label on hamburger, aria-labels on contact links, `safeEmail`/`safeTel` on hrefs |
| `apps/agent-site/components/legal/CookieConsentBanner.tsx` | `aria-modal`, `aria-describedby`, focus management via `useFocusTrap` |
| `apps/agent-site/templates/emerald-classic.tsx` | Extract `<Nav>` outside template wrapper |
| `apps/agent-site/templates/modern-minimal.tsx` | Extract `<Nav>` outside template wrapper |
| `apps/agent-site/templates/warm-community.tsx` | Extract `<Nav>` outside template wrapper |
| `apps/agent-site/app/layout.tsx` | Move skip link target to after nav |
| `apps/agent-site/middleware.ts` | Validate `apiUrl`, add security headers |
| `packages/ui/cma/cma-api.ts` | Strip error detail from user-facing message |
| `apps/agent-site/components/sections/stats/StatsCards.tsx` | Fix `<dt>`/`<dd>` order, fix contrast |
| `apps/agent-site/components/sections/stats/StatsInline.tsx` | Fix `<dt>`/`<dd>` order, fix contrast |
| `apps/agent-site/components/sections/stats/StatsBar.tsx` | Fix disclaimer contrast |
| `apps/agent-site/components/sections/shared/Footer.tsx` | Fix contrast on legal links and copyright |
| `apps/agent-site/components/sections/testimonials/TestimonialsClean.tsx` | Fix FTC disclaimer contrast |
| `apps/agent-site/components/sections/testimonials/TestimonialsGrid.tsx` | Fix FTC disclaimer contrast |
| `apps/agent-site/components/sections/testimonials/TestimonialsBubble.tsx` | `aria-hidden` on avatar, fix FTC contrast |
| `apps/agent-site/components/sections/heroes/HeroCentered.tsx` | Fix body text contrast, `aria-hidden` on arrow |
| `apps/agent-site/components/sections/heroes/HeroSplit.tsx` | Fix body text contrast, `aria-hidden` on arrow |
| `apps/agent-site/components/sections/heroes/HeroGradient.tsx` | `aria-hidden` on arrow |
| `apps/agent-site/components/sections/about/AboutCard.tsx` | Make phone/email actionable links, add credentials list |
| `apps/agent-site/components/sections/about/AboutMinimal.tsx` | Fix headshot alt, fix credentials contrast |
| `apps/agent-site/components/sections/about/AboutSplit.tsx` | Fix headshot alt |
| `apps/agent-site/components/sections/services/ServicesIcons.tsx` | `aria-hidden` on all SVGs |
| `apps/agent-site/components/sections/steps/StepsFriendly.tsx` | `aria-hidden` on step number, `role="list"` on `<ol>` |
| `apps/agent-site/components/sections/steps/StepsNumbered.tsx` | `role="list"` on `<ol>` |
| `apps/agent-site/components/sections/steps/StepsTimeline.tsx` | `role="list"` on `<ol>`, fix contrast |
| `apps/agent-site/components/sections/shared/CmaSection.tsx` | `aria-hidden` on arrow |
| `apps/agent-site/app/thank-you/page.tsx` | Validate `?email` param |
| `apps/agent-site/app/privacy/page.tsx` | Align `agentId` resolution with `page.tsx` |
| `apps/agent-site/app/terms/page.tsx` | Align `agentId` resolution with `page.tsx` |
| `apps/agent-site/app/layout.tsx` | Escape `</script>` in JSON-LD |

---

## Phase 1: Critical ADA — Focus Management

### Task 1: Create `useFocusTrap` Hook

**Files:**
- Create: `apps/agent-site/lib/use-focus-trap.ts`
- Create: `apps/agent-site/__tests__/lib/use-focus-trap.test.ts`

- [ ] **Step 1: Write the failing tests**

```ts
// apps/agent-site/__tests__/lib/use-focus-trap.test.ts
import { describe, it, expect, vi } from "vitest";
import { renderHook } from "@testing-library/react";
import { useFocusTrap } from "../../lib/use-focus-trap";

describe("useFocusTrap", () => {
  it("returns a ref", () => {
    const { result } = renderHook(() => useFocusTrap(true));
    expect(result.current).toBeDefined();
    expect(result.current.current).toBeNull();
  });

  it("does not trap when disabled", () => {
    const container = document.createElement("div");
    const button = document.createElement("button");
    container.appendChild(button);
    document.body.appendChild(container);

    const { result } = renderHook(() => useFocusTrap(false));
    // Should not throw or add event listeners
    expect(result.current).toBeDefined();
    document.body.removeChild(container);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/agent-site && npx vitest run __tests__/lib/use-focus-trap.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the hook**

```ts
// apps/agent-site/lib/use-focus-trap.ts
"use client";

import { useEffect, useRef, useCallback } from "react";

const FOCUSABLE_SELECTOR =
  'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

export function useFocusTrap(active: boolean) {
  const containerRef = useRef<HTMLDivElement>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);

  const handleKeyDown = useCallback((e: KeyboardEvent) => {
    if (e.key !== "Tab" || !containerRef.current) return;

    const focusable = containerRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR);
    if (focusable.length === 0) return;

    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    if (e.shiftKey && document.activeElement === first) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && document.activeElement === last) {
      e.preventDefault();
      first.focus();
    }
  }, []);

  useEffect(() => {
    if (!active) return;

    previousFocusRef.current = document.activeElement as HTMLElement;

    const container = containerRef.current;
    if (!container) return;

    // Focus the first focusable element
    const focusable = container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR);
    if (focusable.length > 0) {
      focusable[0].focus();
    }

    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      // Return focus to the element that was focused before the trap
      previousFocusRef.current?.focus();
    };
  }, [active, handleKeyDown]);

  return containerRef;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/agent-site && npx vitest run __tests__/lib/use-focus-trap.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/lib/use-focus-trap.ts apps/agent-site/__tests__/lib/use-focus-trap.test.ts
git commit -m "feat: add useFocusTrap hook for modal focus management"
```

---

### Task 2: Fix Nav Drawer Focus Trap (CRIT-2)

**Files:**
- Modify: `apps/agent-site/components/Nav.tsx`
- Modify: `apps/agent-site/__tests__/components/Nav.test.tsx`

The Nav drawer at line 366 has `role="dialog"` and `aria-modal="true"` but no programmatic focus trap. Also, the hamburger button `aria-label` is static "Menu" (line 316) — it should toggle.

- [ ] **Step 1: Write failing tests for focus trap and dynamic aria-label**

Add to `apps/agent-site/__tests__/components/Nav.test.tsx` (use existing `AGENT` fixture — Nav has no `agentId` prop):

```tsx
it("hamburger button has dynamic aria-label based on drawer state", async () => {
  render(<Nav agent={AGENT} />);
  const button = screen.getByRole("button", { name: "Open menu" });
  expect(button).toHaveAttribute("aria-label", "Open menu");

  await fireEvent.click(button);
  expect(button).toHaveAttribute("aria-label", "Close menu");
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/Nav.test.tsx -t "hamburger button has dynamic"`
Expected: FAIL — finds "Menu" not "Open menu"

- [ ] **Step 3: Apply Nav.tsx changes**

In `apps/agent-site/components/Nav.tsx`:

1. Add import at top:
```tsx
import { useFocusTrap } from "../lib/use-focus-trap";
```

2. Inside the Nav component, after the existing `useEffect` hooks (~line 100), add:
```tsx
const drawerRef = useFocusTrap(drawerOpen);
```

3. Change hamburger button `aria-label` (line 316) from:
```tsx
aria-label="Menu"
```
to:
```tsx
aria-label={drawerOpen ? "Close menu" : "Open menu"}
```

4. On the drawer `<div>` (~line 366), replace the existing `ref={drawerRef}` with the focus trap ref. Since Nav already has `const drawerRef = useRef<HTMLDivElement>(null)` at line 66, rename the existing ref to `drawerDomRef` and use the focus trap ref on the drawer element:
```tsx
const focusTrapRef = useFocusTrap(drawerOpen);
```
Then on the drawer div:
```tsx
<div
  ref={focusTrapRef}
  id="nav-drawer"
  role="dialog"
  // ... rest unchanged
```

5. Remove the existing manual focus logic in the `useEffect` at lines 88-100 (the `useFocusTrap` hook now handles initial focus and return-focus). **Note:** The existing effect restores focus to `hamburgerRef` specifically — verify the `useFocusTrap` hook's `previousFocusRef` captures the hamburger button (it will, since the hamburger is the last focused element before drawer opens).

- [ ] **Step 4: Run full Nav tests**

Run: `cd apps/agent-site && npx vitest run __tests__/components/Nav.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/Nav.tsx apps/agent-site/__tests__/components/Nav.test.tsx
git commit -m "fix: add focus trap to nav drawer, toggle hamburger aria-label"
```

---

### Task 3: Fix Skip Link Targeting (CRIT-1)

**Files:**
- Modify: `apps/agent-site/templates/emerald-classic.tsx`
- Modify: `apps/agent-site/templates/modern-minimal.tsx`
- Modify: `apps/agent-site/templates/warm-community.tsx`
- Modify: `apps/agent-site/app/layout.tsx`
- Modify: `apps/agent-site/__tests__/templates/emerald-classic.test.tsx`
- Modify: `apps/agent-site/__tests__/templates/modern-minimal.test.tsx`
- Modify: `apps/agent-site/__tests__/templates/warm-community.test.tsx`

The skip link targets `#main-content` on `<main>`, but `<Nav>` renders inside `<main>` via each template. Fix: each template wraps its content (after Nav) with `id="main-content"` so the skip link jumps past the nav.

- [ ] **Step 1: Write failing tests**

In each template test file, add (note: `TemplateProps` is `{ agent: AgentConfig; content: AgentContent }` — no `agentId` prop):
```tsx
it("has main-content target after Nav for skip link", () => {
  render(<EmeraldClassic agent={mockAgent} content={mockContent} />);
  const target = document.getElementById("main-content");
  expect(target).toBeTruthy();
  // The target should NOT contain the nav
  const nav = target?.querySelector("nav");
  expect(nav).toBeNull();
});
```

(Repeat for ModernMinimal and WarmCommunity with their component names. Use existing test fixtures from each file.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/agent-site && npx vitest run __tests__/templates/`
Expected: FAIL — `main-content` wraps everything including nav

- [ ] **Step 3: Update templates**

In `apps/agent-site/templates/emerald-classic.tsx`, change from:
```tsx
<div style={{ paddingTop: "0" }}>
  <Nav agent={agent} agentId={agentId} />
  {/* sections */}
</div>
```
to:
```tsx
<>
  <Nav agent={agent} agentId={agentId} />
  <div id="main-content" tabIndex={-1} style={{ paddingTop: "0" }}>
    {/* sections */}
  </div>
</>
```

Apply the same pattern to `modern-minimal.tsx` and `warm-community.tsx`.

In `apps/agent-site/app/layout.tsx`, change `<main>` from:
```tsx
<main id="main-content" tabIndex={-1}>
```
to:
```tsx
<main>
```

(The `id="main-content"` now lives inside each template, after the nav.)

- [ ] **Step 4: Run all template tests**

Run: `cd apps/agent-site && npx vitest run __tests__/templates/`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/templates/ apps/agent-site/app/layout.tsx apps/agent-site/__tests__/templates/
git commit -m "fix: move skip link target past Nav in all templates"
```

---

### Task 4: Fix Cookie Banner Focus Management (CRIT-3)

**Files:**
- Modify: `apps/agent-site/components/legal/CookieConsentBanner.tsx`
- Modify: `apps/agent-site/__tests__/components/legal/CookieConsentBanner.test.tsx`

Missing: `aria-modal="true"`, `aria-describedby`, focus-on-mount.

- [ ] **Step 1: Write failing tests**

Add to `CookieConsentBanner.test.tsx`:
```tsx
it("has aria-modal and aria-describedby on dialog", () => {
  render(<CookieConsentBanner />);
  const dialog = screen.getByRole("dialog");
  expect(dialog).toHaveAttribute("aria-modal", "true");
  expect(dialog).toHaveAttribute("aria-describedby", "cookie-desc");
});

it("moves focus to first button on mount", () => {
  render(<CookieConsentBanner />);
  const declineBtn = screen.getByRole("button", { name: /decline/i });
  expect(document.activeElement).toBe(declineBtn);
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/agent-site && npx vitest run __tests__/components/legal/CookieConsentBanner.test.tsx`
Expected: FAIL — no aria-modal, focus not on button

- [ ] **Step 3: Update CookieConsentBanner.tsx**

1. Add import:
```tsx
import { useFocusTrap } from "../../lib/use-focus-trap";
```

2. Inside the component, add after the `dismissed` state hook (line 25). The component returns `null` when `consent || dismissed` (line 37), so the trap should be active when neither is set:
```tsx
const bannerRef = useFocusTrap(!consent && !dismissed);
```

3. On the dialog `<div>` (line 43), change:
```tsx
<div
  ref={bannerRef}
  role="dialog"
  aria-label="Cookie consent"
  aria-modal="true"
  aria-describedby="cookie-desc"
  // ... rest of styles unchanged
```

4. On the `<p>` element (line 49), add `id="cookie-desc"`:
```tsx
<p id="cookie-desc" style={{ /* existing styles */ }}>
```

- [ ] **Step 4: Run tests**

Run: `cd apps/agent-site && npx vitest run __tests__/components/legal/CookieConsentBanner.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/legal/CookieConsentBanner.tsx apps/agent-site/__tests__/components/legal/CookieConsentBanner.test.tsx
git commit -m "fix: add focus trap and aria attributes to cookie consent banner"
```

---

## Phase 2: High Security

### Task 5: Validate `NEXT_PUBLIC_API_URL` in CSP & Add Security Headers (S-H3, S-H4)

**Files:**
- Create: `apps/agent-site/lib/security-headers.ts`
- Modify: `apps/agent-site/middleware.ts`
- Modify: `apps/agent-site/__tests__/middleware/middleware.test.ts`

- [ ] **Step 1: Write failing tests**

Add to `middleware.test.ts`:
```tsx
it("sets X-Content-Type-Options header", async () => {
  const req = new NextRequest("https://jenise.real-estate-star.com/");
  const res = await middleware(req);
  expect(res.headers.get("X-Content-Type-Options")).toBe("nosniff");
});

it("sets Referrer-Policy header", async () => {
  const req = new NextRequest("https://jenise.real-estate-star.com/");
  const res = await middleware(req);
  expect(res.headers.get("Referrer-Policy")).toBe("strict-origin-when-cross-origin");
});

it("sets Strict-Transport-Security header", async () => {
  const req = new NextRequest("https://jenise.real-estate-star.com/");
  const res = await middleware(req);
  expect(res.headers.get("Strict-Transport-Security")).toBe("max-age=63072000; includeSubDomains");
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/agent-site && npx vitest run __tests__/middleware/middleware.test.ts`
Expected: FAIL — headers not set

- [ ] **Step 3: Create security-headers.ts**

```ts
// apps/agent-site/lib/security-headers.ts
import { NextResponse } from "next/server";

export function applySecurityHeaders(response: NextResponse): void {
  response.headers.set("X-Content-Type-Options", "nosniff");
  response.headers.set("Referrer-Policy", "strict-origin-when-cross-origin");
  response.headers.set("Strict-Transport-Security", "max-age=63072000; includeSubDomains");
}

const SAFE_URL = /^https?:\/\/[a-zA-Z0-9._:-]+$/;

/** Validate a URL is safe to inject into a CSP header (no semicolons, no directives). */
export function safeCspUrl(url: string | undefined): string {
  if (!url) return "";
  return SAFE_URL.test(url) ? url : "";
}
```

- [ ] **Step 4: Update middleware.ts**

In `apps/agent-site/middleware.ts`:

1. Add import:
```ts
import { applySecurityHeaders, safeCspUrl } from "./lib/security-headers";
```

2. Change the `apiUrl` line (line 5) from:
```ts
const apiUrl = process.env.NEXT_PUBLIC_API_URL || "";
```
to:
```ts
const apiUrl = safeCspUrl(process.env.NEXT_PUBLIC_API_URL);
```

3. In every branch that returns a `NextResponse`, before the return, call:
```ts
applySecurityHeaders(response);
```

There are ~4 response sites in the middleware (the agent rewrite, the root redirect, the fallback). Add `applySecurityHeaders(response)` before each return.

- [ ] **Step 5: Run tests**

Run: `cd apps/agent-site && npx vitest run __tests__/middleware/middleware.test.ts`
Expected: ALL PASS

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/lib/security-headers.ts apps/agent-site/middleware.ts apps/agent-site/__tests__/middleware/middleware.test.ts
git commit -m "fix: add security headers and validate API URL in CSP"
```

---

### Task 6: Strip API Error Detail from User-Facing Message (S-H1)

**Files:**
- Modify: `packages/ui/cma/cma-api.ts`
- Modify: `packages/ui/cma/cma-api.test.ts`

**Important:** The function is `submitCma(apiBaseUrl, agentId, request)` with 3 params. The existing tests at lines 76-93 explicitly assert error bodies ARE included (`"CMA submission failed (400): Validation error"`). Those must be updated too.

- [ ] **Step 1: Write failing test**

Add to `packages/ui/cma/cma-api.test.ts`:
```ts
it("attaches server detail to error object but not to message", async () => {
  vi.spyOn(globalThis, "fetch").mockResolvedValue(
    new Response("NullReferenceException at Server.Internal.Handler", { status: 500 }),
  );

  try {
    await submitCma(API_BASE, AGENT_ID, makeRequest());
    expect.fail("should have thrown");
  } catch (e) {
    const err = e as Error & { serverDetail?: string };
    expect(err.message).toBe("CMA submission failed (500)");
    expect(err.message).not.toContain("NullReferenceException");
    expect(err.serverDetail).toBe("NullReferenceException at Server.Internal.Handler");
  }
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd packages/ui && npx vitest run cma/cma-api.test.ts -t "attaches server detail"`
Expected: FAIL — error message includes the server body

- [ ] **Step 3: Update cma-api.ts**

In `packages/ui/cma/cma-api.ts`, change the error block (lines 30-33) from:
```ts
const text = await response.text().catch(() => "");
const detail = text ? `: ${text}` : "";
throw new Error(`CMA submission failed (${response.status})${detail}`);
```
to:
```ts
// Capture detail for Sentry but don't expose to user
const detail = await response.text().catch(() => "");
const error = new Error(`CMA submission failed (${response.status})`);
(error as Error & { serverDetail?: string }).serverDetail = detail || undefined;
throw error;
```

- [ ] **Step 4: Update existing tests that assert error body in message**

In `packages/ui/cma/cma-api.test.ts`, update the two tests that expect detail in the message:

Line 76 test: Change from:
```ts
await expect(submitCma(API_BASE, AGENT_ID, makeRequest())).rejects.toThrow(
  "CMA submission failed (400): Validation error",
);
```
to:
```ts
await expect(submitCma(API_BASE, AGENT_ID, makeRequest())).rejects.toThrow(
  "CMA submission failed (400)",
);
```

Line 86 test: Change from:
```ts
await expect(submitCma(API_BASE, AGENT_ID, makeRequest())).rejects.toThrow(
  "CMA submission failed (500): Internal Server Error",
);
```
to:
```ts
await expect(submitCma(API_BASE, AGENT_ID, makeRequest())).rejects.toThrow(
  "CMA submission failed (500)",
);
```

- [ ] **Step 5: Run all cma-api tests**

Run: `cd packages/ui && npx vitest run cma/cma-api.test.ts`
Expected: ALL PASS

- [ ] **Step 6: Commit**

```bash
git add packages/ui/cma/cma-api.ts packages/ui/cma/cma-api.test.ts
git commit -m "fix: strip raw API error body from user-facing error message"
```

---

### Task 7: Add Contact Href Validators (S-H2)

**Files:**
- Create: `apps/agent-site/lib/safe-contact.ts`
- Create: `apps/agent-site/__tests__/lib/safe-contact.test.ts`
- Modify: `apps/agent-site/components/Nav.tsx`
- Modify: `apps/agent-site/components/sections/shared/Footer.tsx`

- [ ] **Step 1: Write failing tests for validators**

```ts
// apps/agent-site/__tests__/lib/safe-contact.test.ts
import { describe, it, expect } from "vitest";
import { safeMailtoHref, safeTelHref } from "../../lib/safe-contact";

describe("safeMailtoHref", () => {
  it("returns mailto: for valid email", () => {
    expect(safeMailtoHref("agent@example.com")).toBe("mailto:agent@example.com");
  });
  it("returns # for empty string", () => {
    expect(safeMailtoHref("")).toBe("#");
  });
  it("returns # for javascript: attempt", () => {
    expect(safeMailtoHref("javascript:alert(1)")).toBe("#");
  });
  it("returns # for email with protocol prefix", () => {
    expect(safeMailtoHref("mailto:real@example.com")).toBe("#");
  });
  it("returns # for email with spaces", () => {
    expect(safeMailtoHref("has spaces@example.com")).toBe("#");
  });
});

describe("safeTelHref", () => {
  it("returns tel: for valid phone digits", () => {
    expect(safeTelHref("(732) 555-1234")).toBe("tel:7325551234");
  });
  it("returns tel: with extension", () => {
    expect(safeTelHref("(732) 555-1234", "5")).toBe("tel:7325551234,5");
  });
  it("returns # for empty string", () => {
    expect(safeTelHref("")).toBe("#");
  });
  it("strips non-digits", () => {
    expect(safeTelHref("+1-800-FLOWERS")).toBe("tel:1800");
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/agent-site && npx vitest run __tests__/lib/safe-contact.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Implement safe-contact.ts**

```ts
// apps/agent-site/lib/safe-contact.ts
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function safeMailtoHref(email: string): string {
  if (!email || !EMAIL_RE.test(email)) return "#";
  return `mailto:${email}`;
}

export function safeTelHref(phone: string, ext?: string): string {
  const digits = phone.replace(/\D/g, "");
  if (!digits) return "#";
  const extSuffix = ext ? `,${ext.replace(/\D/g, "")}` : "";
  return `tel:${digits}${extSuffix}`;
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/agent-site && npx vitest run __tests__/lib/safe-contact.test.ts`
Expected: ALL PASS

- [ ] **Step 5: Update Nav.tsx and Footer.tsx to use validators**

In `Nav.tsx`, add import:
```tsx
import { safeMailtoHref, safeTelHref } from "../lib/safe-contact";
```

Replace all `mailto:` and `tel:` href constructions:
- Desktop email link (~line 230): `href={\`mailto:${emails[0].value}\`}` -> `href={safeMailtoHref(emails[0].value)}`
- Replace `buildTelHref` function body to use `safeTelHref`:
  ```ts
  function buildTelHref(phone: { value: string; ext?: string }): string {
    return safeTelHref(phone.value, phone.ext);
  }
  ```
- Drawer email links (~line 453): same mailto replacement
- Drawer phone links (~line 428): same tel replacement

In `Footer.tsx`, add import:
```tsx
import { safeMailtoHref, safeTelHref } from "../../lib/safe-contact";
```

Replace:
- Line 61: `href={\`tel:${identity.phone.replace(/\D/g, "")}\`}` -> `href={safeTelHref(identity.phone)}`
- Line 71: `href={\`tel:${identity.office_phone.replace(/[^0-9]/g, "")}\`}` -> `href={safeTelHref(identity.office_phone)}`
- Line 82: `href={\`mailto:${identity.email}\`}` -> `href={safeMailtoHref(identity.email)}`

- [ ] **Step 6: Run Nav and Footer tests**

Run: `cd apps/agent-site && npx vitest run __tests__/components/Nav.test.tsx __tests__/components/shared/Footer.test.tsx`
Expected: ALL PASS

- [ ] **Step 7: Commit**

```bash
git add apps/agent-site/lib/safe-contact.ts apps/agent-site/__tests__/lib/safe-contact.test.ts apps/agent-site/components/Nav.tsx apps/agent-site/components/sections/shared/Footer.tsx
git commit -m "fix: add safeMailtoHref/safeTelHref validators for contact links"
```

---

### Task 8: Validate Email on Thank-You Page (S-M2)

**Files:**
- Modify: `apps/agent-site/app/thank-you/page.tsx`
- Modify or create: `apps/agent-site/__tests__/pages/thank-you.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
it("does not display invalid email from query param", async () => {
  render(await ThankYouPage({ searchParams: { email: "not-an-email<script>" } }));
  expect(screen.queryByText("not-an-email<script>")).not.toBeInTheDocument();
});

it("displays valid email from query param", async () => {
  render(await ThankYouPage({ searchParams: { email: "test@example.com" } }));
  expect(screen.getByText("test@example.com")).toBeInTheDocument();
});
```

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL — invalid email is rendered

- [ ] **Step 3: Update thank-you/page.tsx**

Add email validation before rendering. Near the top of the component (after extracting `email` from searchParams):

```tsx
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const validEmail = email && EMAIL_RE.test(email) && email.length <= 254 ? email : null;
```

Then use `validEmail` instead of `email` in the JSX:
```tsx
{validEmail && (
  <p>We&apos;ll send your report to <strong>{validEmail}</strong></p>
)}
```

- [ ] **Step 4: Run tests**

Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/app/thank-you/page.tsx apps/agent-site/__tests__/pages/
git commit -m "fix: validate email query param on thank-you page before rendering"
```

---

## Phase 3: High ADA — Contrast Fixes

All contrast fixes follow the same pattern: change a hardcoded color hex value. Batch them into one task per component group.

### Task 9: Fix Stats Component Contrast + `<dt>`/`<dd>` Order

**Files:**
- Modify: `apps/agent-site/components/sections/stats/StatsCards.tsx`
- Modify: `apps/agent-site/components/sections/stats/StatsInline.tsx`
- Modify: `apps/agent-site/components/sections/stats/StatsBar.tsx`
- Modify: `apps/agent-site/__tests__/components/stats/StatsCards.test.tsx`
- Modify: `apps/agent-site/__tests__/components/stats/StatsInline.test.tsx`

- [ ] **Step 1: Write failing tests for dt/dd order**

Add to `StatsCards.test.tsx` (note: each dt/dd pair is wrapped in a `<div>` inside the `<dl>`):
```tsx
it("renders dt before dd in DOM order within each stat card", () => {
  const { container } = render(<StatsCards items={mockItems} />);
  // Each stat pair is inside a <div> wrapper within the <dl>
  const wrappers = container.querySelectorAll("dl > div");
  wrappers.forEach((wrapper) => {
    const children = Array.from(wrapper.children);
    const dtIndex = children.findIndex((c) => c.tagName === "DT");
    const ddIndex = children.findIndex((c) => c.tagName === "DD");
    expect(dtIndex).not.toBe(-1);
    expect(ddIndex).not.toBe(-1);
    expect(dtIndex).toBeLessThan(ddIndex);
  });
});
```

Same pattern for `StatsInline.test.tsx` (check the wrapper structure in that component).

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/agent-site && npx vitest run __tests__/components/stats/`
Expected: FAIL — `<dd>` comes before `<dt>` in DOM

- [ ] **Step 3: Fix StatsCards.tsx**

Swap `<dd>` and `<dt>` so `<dt>` (label) comes first in DOM. Use CSS `order` to keep the visual layout (value on top, label below):

```tsx
<dl style={{ /* existing */ }}>
  <dt style={{ order: 2, /* existing label styles but change color */ color: "#767676" }}>{stat.label}</dt>
  <dd style={{ order: 1, /* existing value styles */ }}>{stat.value}</dd>
</dl>
```

Change disclaimer `color: "#aaa"` to `color: "#767676"`.

- [ ] **Step 4: Fix StatsInline.tsx**

Same `<dt>`/`<dd>` swap with CSS `order`. Change disclaimer `color: "#B0A090"` to `color: "#8B7355"`.

- [ ] **Step 5: Fix StatsBar.tsx**

Change disclaimer `color: "rgba(255,255,255,0.6)"` to `color: "rgba(255,255,255,0.85)"`.

- [ ] **Step 6: Run all stats tests**

Run: `cd apps/agent-site && npx vitest run __tests__/components/stats/`
Expected: ALL PASS

- [ ] **Step 7: Commit**

```bash
git add apps/agent-site/components/sections/stats/ apps/agent-site/__tests__/components/stats/
git commit -m "fix: correct dt/dd order in stats components, fix contrast on disclaimers"
```

---

### Task 10: Fix Testimonial Contrast

**Files:**
- Modify: `apps/agent-site/components/sections/testimonials/TestimonialsClean.tsx`
- Modify: `apps/agent-site/components/sections/testimonials/TestimonialsGrid.tsx`
- Modify: `apps/agent-site/components/sections/testimonials/TestimonialsBubble.tsx`

- [ ] **Step 1: Apply contrast fixes**

`TestimonialsClean.tsx`:
- Line 26: `color: "#aaa"` -> `color: "#767676"` (FTC disclaimer)
- Line 75: `color: "#aaa"` -> `color: "#767676"` (source)

`TestimonialsGrid.tsx`:
- Line 28: `color: "#999"` -> `color: "#767676"` (FTC disclaimer)
- Line 84: `color: "#999"` -> `color: "#767676"` (source)

`TestimonialsBubble.tsx`:
- Line 24: `color: "#B0A090"` -> `color: "#8B7355"` (FTC disclaimer)
- Line 109: `color: "#B0A090"` -> `color: "#8B7355"` (source)
- Line 86: Add `aria-hidden="true"` to avatar initial div

- [ ] **Step 2: Run testimonial tests**

Run: `cd apps/agent-site && npx vitest run __tests__/components/testimonials/`
Expected: ALL PASS

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/components/sections/testimonials/
git commit -m "fix: improve contrast on testimonial disclaimers, hide avatar from AT"
```

---

### Task 11: Fix Hero and Footer Contrast + Arrow aria-hidden

**Files:**
- Modify: `apps/agent-site/components/sections/heroes/HeroCentered.tsx`
- Modify: `apps/agent-site/components/sections/heroes/HeroSplit.tsx`
- Modify: `apps/agent-site/components/sections/heroes/HeroGradient.tsx`
- Modify: `apps/agent-site/components/sections/shared/Footer.tsx`
- Modify: `apps/agent-site/components/sections/shared/CmaSection.tsx`

- [ ] **Step 1: Apply fixes**

`HeroCentered.tsx`:
- Body text (line 67): `color: "#8B7355"` at 16px -> `color: "#6B5040"` (darken for 4.5:1 on #FFF8F0)
- CTA arrow (line 97): change `{data.cta_text} →` to `{data.cta_text} <span aria-hidden="true"> →</span>`

`HeroSplit.tsx`:
- Body text (line 50): `color: "#888"` -> `color: "#767676"`
- CTA arrow: wrap in `<span aria-hidden="true">`

`HeroGradient.tsx`:
- CTA arrow: wrap in `<span aria-hidden="true">`

`Footer.tsx`:
- Line 100: `color: "rgba(255,255,255,0.7)"` -> `color: "rgba(255,255,255,0.85)"`
- Line 117: same change
- Line 128: remove `opacity: 0.6` (or change to `opacity: 0.85`)
- Line 109: `fontSize: "11px"` -> `fontSize: "12px"`

`CmaSection.tsx`:
- CTA arrow (~line 108-110): wrap arrow in `<span aria-hidden="true">`

- [ ] **Step 2: Run hero and footer tests**

Run: `cd apps/agent-site && npx vitest run __tests__/components/heroes/ __tests__/components/shared/`
Expected: ALL PASS

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/components/sections/heroes/ apps/agent-site/components/sections/shared/
git commit -m "fix: improve contrast in heroes/footer, hide decorative arrows from AT"
```

---

## Phase 4: High ADA — Semantic Fixes

### Task 12: Fix About Components

**Files:**
- Modify: `apps/agent-site/components/sections/about/AboutCard.tsx`
- Modify: `apps/agent-site/components/sections/about/AboutMinimal.tsx`
- Modify: `apps/agent-site/components/sections/about/AboutSplit.tsx`
- Modify: corresponding test files

- [ ] **Step 1: Write failing tests**

Add to `AboutCard.test.tsx`:
```tsx
it("renders phone as a tel: link", () => {
  render(<AboutCard agent={mockAgent} />);
  const phoneLink = screen.getByRole("link", { name: /call/i });
  expect(phoneLink).toHaveAttribute("href", expect.stringMatching(/^tel:/));
});

it("renders email as a mailto: link", () => {
  render(<AboutCard agent={mockAgent} />);
  const emailLink = screen.getByRole("link", { name: /email/i });
  expect(emailLink).toHaveAttribute("href", expect.stringMatching(/^mailto:/));
});

it("renders credentials as a list", () => {
  render(<AboutCard agent={mockAgent} />);
  const list = screen.getByRole("list", { name: /credentials/i });
  expect(list).toBeInTheDocument();
});
```

Add to `AboutMinimal.test.tsx` and `AboutSplit.test.tsx`:
```tsx
it("uses descriptive alt text on headshot", () => {
  render(<AboutMinimal agent={mockAgent} />);
  const img = screen.getByRole("img");
  expect(img).toHaveAttribute("alt", expect.stringContaining("Photo of"));
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/agent-site && npx vitest run __tests__/components/about/`
Expected: FAIL

- [ ] **Step 3: Apply fixes**

`AboutCard.tsx`:
- Import `safeMailtoHref, safeTelHref` from `../../lib/safe-contact`
- Lines 89-96: Replace plain `<div>` phone/email with `<a>` links:
  ```tsx
  <a href={safeTelHref(agent.identity.phone)} aria-label={`Call ${agent.identity.name}`}>
    {agent.identity.phone}
  </a>
  <a href={safeMailtoHref(agent.identity.email)} aria-label={`Email ${agent.identity.name}`}>
    {agent.identity.email}
  </a>
  ```
- Lines 64-88: Replace `<div>/<span>` credentials with `<ul aria-label="Credentials">` and `<li>` elements

`AboutMinimal.tsx`:
- Line 32: `alt={agent.identity.name}` -> `alt={\`Photo of ${agent.identity.name}\`}`
- Line 64: `color: "#888"` -> `color: "#767676"` (credentials contrast)

`AboutSplit.tsx`:
- Line 37: `alt={agent.identity.name}` -> `alt={\`Photo of ${agent.identity.name}\`}`

- [ ] **Step 4: Run tests**

Run: `cd apps/agent-site && npx vitest run __tests__/components/about/`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/about/ apps/agent-site/__tests__/components/about/
git commit -m "fix: make AboutCard contacts actionable, fix headshot alt text, credentials list"
```

---

### Task 13: Fix ServicesIcons SVG aria-hidden

**Files:**
- Modify: `apps/agent-site/components/sections/services/ServicesIcons.tsx`
- Modify: `apps/agent-site/__tests__/components/services/ServicesIcons.test.tsx`

- [ ] **Step 1: Write failing test**

Add to `ServicesIcons.test.tsx`:
```tsx
it("hides decorative SVG icons from assistive technology", () => {
  const { container } = render(<ServicesIcons services={mockServices} agent={mockAgent} />);
  const svgs = container.querySelectorAll("svg");
  svgs.forEach((svg) => {
    expect(svg).toHaveAttribute("aria-hidden", "true");
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/agent-site && npx vitest run __tests__/components/services/ServicesIcons.test.tsx`

- [ ] **Step 3: Add `"aria-hidden": "true"` to the `SVG_PROPS` object**

In `ServicesIcons.tsx`, find the `SVG_PROPS` constant (line 5) and add `"aria-hidden": "true"`:
```ts
const SVG_PROPS = {
  width: "28",
  height: "28",
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "1.8",
  strokeLinecap: "round" as const,
  strokeLinejoin: "round" as const,
  "aria-hidden": "true" as const,
};
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `cd apps/agent-site && npx vitest run __tests__/components/services/ServicesIcons.test.tsx`

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/services/ServicesIcons.tsx apps/agent-site/__tests__/components/services/ServicesIcons.test.tsx
git commit -m "fix: add aria-hidden to decorative SVG icons in ServicesIcons"
```

---

### Task 14: Fix Steps Components — `role="list"` and `aria-hidden` on Numbers

**Files:**
- Modify: `apps/agent-site/components/sections/steps/StepsFriendly.tsx`
- Modify: `apps/agent-site/components/sections/steps/StepsNumbered.tsx`
- Modify: `apps/agent-site/components/sections/steps/StepsTimeline.tsx`
- Modify: corresponding test files

- [ ] **Step 1: Write failing tests**

Add to each steps test file:
```tsx
it("has role=list on the ordered list", () => {
  const { container } = render(<StepsFriendly steps={mockSteps} />);
  const ol = container.querySelector("ol");
  expect(ol).toHaveAttribute("role", "list");
});
```

For `StepsFriendly.test.tsx` specifically:
```tsx
it("hides step number from assistive technology", () => {
  const { container } = render(<StepsFriendly steps={mockSteps} />);
  const numberDivs = container.querySelectorAll("[aria-hidden='true']");
  expect(numberDivs.length).toBeGreaterThanOrEqual(mockSteps.length);
});
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `cd apps/agent-site && npx vitest run __tests__/components/steps/`

- [ ] **Step 3: Apply fixes**

`StepsFriendly.tsx`:
- Line 32: Add `role="list"` to `<ol>`
- Line 53: Add `aria-hidden="true"` to the step number `<div>`

`StepsNumbered.tsx`:
- Line 37: Add `role="list"` to `<ol>`

`StepsTimeline.tsx`:
- Line 33: Add `role="list"` to `<ol>`
- Line 26: `color: "#888"` -> `color: "#555"` (subtitle contrast)
- Line 91: `color: "#888"` -> `color: "#555"` (description contrast)

- [ ] **Step 4: Run all steps tests**

Run: `cd apps/agent-site && npx vitest run __tests__/components/steps/`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/steps/ apps/agent-site/__tests__/components/steps/
git commit -m "fix: add role=list to step lists, hide decorative numbers, fix contrast"
```

---

### Task 15: Nav Contact Link aria-labels

**Files:**
- Modify: `apps/agent-site/components/Nav.tsx`
- Modify: `apps/agent-site/__tests__/components/Nav.test.tsx`

Note: `Nav` props are `{ agent: AgentConfig; navigation?: ...; contactInfo?: ... }` — no `agentId` prop. Use existing `AGENT` fixture.

- [ ] **Step 1: Write failing tests**

```tsx
it("email link has descriptive aria-label", () => {
  render(<Nav agent={AGENT} />);
  const emailLink = screen.getByRole("link", { name: /email/i });
  expect(emailLink).toHaveAttribute("aria-label", expect.stringContaining("Email"));
});

it("phone link has descriptive aria-label", () => {
  render(<Nav agent={AGENT} />);
  const phoneLink = screen.getByRole("link", { name: /call/i });
  expect(phoneLink).toHaveAttribute("aria-label", expect.stringContaining("Call"));
});
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `cd apps/agent-site && npx vitest run __tests__/components/Nav.test.tsx -t "aria-label"`

- [ ] **Step 3: Add aria-labels to contact links in Nav.tsx**

Desktop email link (~line 230):
```tsx
<a href={safeMailtoHref(emails[0].value)} aria-label={`Email ${emails[0].value}`} ...>
```

Desktop phone link (~line 253):
```tsx
<a href={buildTelHref(preferredPhone)} aria-label={`Call ${formatPhoneDisplay(preferredPhone.value, preferredPhone.ext)}`} ...>
```

Drawer email links (~line 453):
```tsx
aria-label={`Email ${email.value}`}
```

Drawer phone links (~line 428):
```tsx
aria-label={`Call ${formatPhoneDisplay(phone.value, phone.ext)}`}
```

- [ ] **Step 4: Run Nav tests**

Run: `cd apps/agent-site && npx vitest run __tests__/components/Nav.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/Nav.tsx apps/agent-site/__tests__/components/Nav.test.tsx
git commit -m "fix: add descriptive aria-labels to Nav contact links"
```

---

## Phase 5: Medium Security

### Task 16: Align Legal Page agentId Resolution (S-M3)

**Files:**
- Modify: `apps/agent-site/app/privacy/page.tsx`
- Modify: `apps/agent-site/app/terms/page.tsx`
- Modify: `apps/agent-site/__tests__/pages/privacy.test.tsx`
- Modify: `apps/agent-site/__tests__/pages/terms.test.tsx`

- [ ] **Step 1: Write failing tests**

```tsx
it("ignores agentId query param in production without PREVIEW", async () => {
  const origEnv = process.env.NODE_ENV;
  const origPreview = process.env.PREVIEW;
  process.env.NODE_ENV = "production";
  delete process.env.PREVIEW;

  // Render with query param — should use default, not the param
  const page = await PrivacyPage({ searchParams: { agentId: "evil-agent" } });
  // Assert it uses DEFAULT_AGENT_ID or fallback

  process.env.NODE_ENV = origEnv;
  process.env.PREVIEW = origPreview;
});
```

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Update both legal pages**

In `privacy/page.tsx` and `terms/page.tsx`, replace the simple `agentId || DEFAULT_AGENT_ID` pattern with the same logic used in `page.tsx`:

```tsx
function resolveAgentId(paramId?: string): string {
  // In production without PREVIEW flag, ignore query param
  if (process.env.NODE_ENV === "production" && !process.env.PREVIEW) {
    return process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
  }
  return paramId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/agent-site && npx vitest run __tests__/pages/privacy.test.tsx __tests__/pages/terms.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/app/privacy/page.tsx apps/agent-site/app/terms/page.tsx apps/agent-site/__tests__/pages/
git commit -m "fix: align legal page agentId resolution with main page security"
```

---

### Task 17: Fix JSON-LD Script Escape in layout.tsx (S-LOW-5)

**Files:**
- Modify: `apps/agent-site/app/layout.tsx`

- [ ] **Step 1: Apply fix**

In `layout.tsx`, find the script tag with JSON-LD (line 17). The `__html` value currently uses `JSON.stringify(structuredData)` without the `</script>` escape that `page.tsx` has. Change to:

```tsx
__html: JSON.stringify(structuredData).replace(/<\/script>/gi, "<\\/script>")
```

This matches the pattern already used in `page.tsx:76`.

- [ ] **Step 2: Run layout tests**

Run: `cd apps/agent-site && npx vitest run`
Expected: ALL PASS

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/app/layout.tsx
git commit -m "fix: escape script tag in layout.tsx JSON-LD for consistency with page.tsx"
```

---

### Task 18: Fix Analytics Init Scripts 404 (S-H5)

**Files:**
- Modify: `apps/agent-site/components/Analytics.tsx`

The component references `/scripts/ga4-init.js` and `/scripts/meta-pixel-init.js` which don't exist. Replace with inline `next/script` initialization that uses the already-validated `safeId` values.

- [ ] **Step 1: Update Analytics.tsx**

Replace the GA4 standalone script block (lines 34-45) from:
```tsx
{gaId && !gtmId && (
  <>
    <Script
      src={`https://www.googletagmanager.com/gtag/js?id=${gaId}`}
      strategy="afterInteractive"
    />
    <Script
      id="ga4-config"
      strategy="afterInteractive"
      src={`/scripts/ga4-init.js?id=${gaId}`}
    />
  </>
)}
```
to (inline init, no external file dependency):
```tsx
{gaId && !gtmId && (
  <>
    <Script
      src={`https://www.googletagmanager.com/gtag/js?id=${gaId}`}
      strategy="afterInteractive"
    />
    <Script id="ga4-config" strategy="afterInteractive">
      {`window.dataLayer=window.dataLayer||[];function gtag(){dataLayer.push(arguments);}gtag('js',new Date());gtag('config','${gaId}');`}
    </Script>
  </>
)}
```

Replace the Meta Pixel script block (lines 49-54) from:
```tsx
{pixelId && (
  <Script
    id="meta-pixel"
    strategy="afterInteractive"
    src={`/scripts/meta-pixel-init.js?id=${pixelId}`}
  />
)}
```
to:
```tsx
{pixelId && (
  <Script id="meta-pixel" strategy="afterInteractive">
    {`!function(f,b,e,v,n,t,s){if(f.fbq)return;n=f.fbq=function(){n.callMethod?n.callMethod.apply(n,arguments):n.queue.push(arguments)};if(!f._fbq)f._fbq=n;n.push=n;n.loaded=!0;n.version='2.0';n.queue=[];t=b.createElement(e);t.async=!0;t.src=v;s=b.getElementsByTagName(e)[0];s.parentNode.insertBefore(t,s)}(window,document,'script','https://connect.facebook.net/en_US/fbevents.js');fbq('init','${pixelId}');fbq('track','PageView');`}
  </Script>
)}
```

Note: Both `gaId` and `pixelId` are already validated by `safeId()` (regex `/^[A-Za-z0-9-]+$/`), so inline interpolation is safe.

- [ ] **Step 2: Run Analytics tests (if any exist) and full test suite**

Run: `cd apps/agent-site && npx vitest run`
Expected: ALL PASS

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/components/Analytics.tsx
git commit -m "fix: replace missing analytics init scripts with inline initialization"
```

---

## Phase 6: Final Verification

### Task 19: Full Test Suite + Lint + Build

- [ ] **Step 1: Run full agent-site test suite with coverage**

```bash
cd apps/agent-site && npx vitest run --coverage
```
Expected: ALL PASS, no regressions

- [ ] **Step 2: Run lint**

```bash
cd apps/agent-site && npx next lint
```
Expected: No errors

- [ ] **Step 3: Run packages/ui tests (LeadForm/CMA changes)**

```bash
cd packages/ui && npx vitest run
```
Expected: ALL PASS

- [ ] **Step 4: Build check**

```bash
cd apps/agent-site && npx next build
```
Expected: Build succeeds

- [ ] **Step 5: Final commit if any formatting changes from lint auto-fix**

```bash
git status
# Only commit if there are auto-fix changes
```

---

## Summary

| Phase | Tasks | Focus |
|-------|-------|-------|
| 1 | Tasks 1-4 | Critical ADA: focus traps, skip link, cookie banner |
| 2 | Tasks 5-8 | High Security: CSP validation, error stripping, contact validators, email validation |
| 3 | Tasks 9-11 | High ADA: contrast fixes across stats, testimonials, heroes, footer |
| 4 | Tasks 12-15 | High ADA: semantic HTML fixes (about, services, steps, nav labels) |
| 5 | Tasks 16-18 | Medium Security: legal page agentId, JSON-LD escape, analytics init scripts |
| 6 | Task 19 | Full verification |

**Total: 19 tasks, ~50 files touched, estimated 18 commits.**

> **Timing note:** This plan operates on the current codebase (pre-pages-architecture migration). Execute before the pages architecture spec is implemented, as that migration will change file paths and section types.
