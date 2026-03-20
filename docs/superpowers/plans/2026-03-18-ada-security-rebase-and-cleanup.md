# ADA/Security Rebase, Fix & Cleanup Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a clean `feat/ada-security-v2` branch from `main` HEAD that contains only the correct ADA/security fixes — properly integrated with the account-based architecture — plus dead code and unused config cleanup.

**Architecture:** The original ada-security-remediation branch has 19 commits but contains an incomplete type migration that conflicts with the already-completed pages-architecture now on `main`. Instead of rebasing (80+ conflict files), we cherry-pick the pure ADA/security changes onto a fresh branch from `main` HEAD, fix the critical regressions found in code review, and clean up dead code.

**Tech Stack:** Next.js 16, React 19, Vitest, @testing-library/react

---

## Strategy: Cherry-Pick, Don't Rebase

The ada-security-remediation branch has 19 commits, but only ~12 contain actual ADA/security fixes. The rest are type migration noise from Task 3's subagent. A rebase would create 50+ conflicts across types, templates, tests, and config — all already correctly handled on `main`.

**Approach:**
1. Create fresh branch from `main` HEAD
2. Cherry-pick the pure ADA/security commits (skip the type migration ones)
3. Resolve cherry-pick conflicts using `main` types as truth
4. Fix the 2 critical + 4 high issues found in code review
5. Clean up dead code and unused configs
6. Run full verification

---

## Phase 0: Branch Setup

### Task 1: Create Clean Branch

**Files:** None (git operations only)

- [ ] **Step 1: Create fresh branch from premium-components HEAD**

```bash
git branch feat/ada-security-v2 main
git worktree add .worktrees/ada-security-v2 feat/ada-security-v2
```

- [ ] **Step 2: Identify cherry-pick candidates**

These commits from `feat/ada-security-remediation` contain pure ADA/security work (no type migration):

```
4b7f4dc feat: add useFocusTrap hook for modal focus management
42ebb0b fix: add focus trap and aria attributes to cookie consent banner
1a24963 feat: validate API URL in CSP and add security headers (S-H3, S-H4)
8a6a31b fix: strip API error detail from user-facing CMA error message (S-H1)
1dadf80 fix: add contact href validators to prevent href injection (S-H2)
51b11c7 fix: fix stats component contrast and dt/dd semantic order
57f8e03 fix(ada): fix testimonial contrast colors and add avatar aria-hidden
45ec89c fix: add aria-hidden to decorative SVG icons in ServicesIcons
169d053 fix: add role=list to step lists, hide decorative numbers, fix contrast
```

**DO NOT cherry-pick these** (broken type migration, wrong function signatures, or type import conflicts):
```
307c293 fix: add focus trap to nav drawer (depends on partial migration)
d7226ff fix: move skip link target (contains massive type migration)
bc4cc7d fix: validate email on thank-you page (uses old agent.identity)
e3bb0f4 fix: hero/footer contrast (mixed with type migration)
5b1b582 fix: about components (mixed with type migration)
dfeb37a fix: nav aria-labels (depends on partial migration)
b50ccf6 fix: legal page agentId (uses old loadAgentConfig signature)
06e37ec fix: JSON-LD escape (trivial, re-do manually)
1b3b05e fix: analytics inline scripts (imports AgentTracking which doesn't exist on premium-components)
06887a2 test: analytics test fix (same AgentTracking import issue)
```

- [ ] **Step 3: Cherry-pick clean commits**

```bash
cd .worktrees/ada-security-v2
git cherry-pick 4b7f4dc   # useFocusTrap hook
git cherry-pick 42ebb0b   # cookie banner focus
git cherry-pick 1a24963   # security headers
git cherry-pick 8a6a31b   # strip API error
git cherry-pick 1dadf80   # safe-contact validators
git cherry-pick 51b11c7   # stats contrast + dt/dd
git cherry-pick 57f8e03   # testimonial contrast
git cherry-pick 45ec89c   # ServicesIcons aria-hidden
git cherry-pick 169d053   # steps role=list + contrast
```

Note: Analytics commits (`1b3b05e`, `06887a2`) are NOT cherry-picked because they import `AgentTracking` which doesn't exist on `main` (it's `AccountTracking`). The analytics fix is re-implemented manually in Phase 1 Task 9.

Resolve any conflicts by keeping `main` types and adjusting the ADA fix to match. If a cherry-pick fails with too many conflicts, skip it and re-implement manually in a later task.

- [ ] **Step 4: Verify clean cherry-picks compiled**

```bash
cd apps/agent-site && npx vitest run 2>&1 | tail -5
```

- [ ] **Step 5: Commit any conflict resolutions**

---

## Phase 1: Re-implement Skipped Fixes Against Correct Types

These fixes were skipped because they were entangled with broken type migration. Re-implement them cleanly.

### Task 2: Fix Nav Drawer Focus Trap + Dynamic aria-label + Contact aria-labels

**Files:**
- Modify: `apps/agent-site/components/Nav.tsx`
- Modify: `apps/agent-site/__tests__/components/Nav.test.tsx`

This combines original Tasks 2 and 15 (Nav focus trap + contact aria-labels).

**IMPORTANT:** On `main`, Nav's prop signature is:
```tsx
interface NavProps {
  account: AccountConfig;        // NOT "agent"
  navigation?: NavigationConfig;
  enabledSections?: Set<string>;
}
```
There is NO `contactInfo` prop — contacts are derived from `account.contact_info` internally. Test fixtures use `ACCOUNT` (not `AGENT`).

- [ ] **Step 1: Read Nav.tsx** — understand the `account` prop, find the drawer div, hamburger button, and contact links. Note how `account.contact_info` is used for email/phone rendering.

- [ ] **Step 2: Add `useFocusTrap` import and usage**

```tsx
import { useFocusTrap } from "../lib/use-focus-trap";
// Inside Nav component:
const focusTrapRef = useFocusTrap(drawerOpen);
// Replace drawer ref with focusTrapRef
```

- [ ] **Step 3: Change hamburger aria-label**

From: `aria-label="Menu"` → To: `aria-label={drawerOpen ? "Close menu" : "Open menu"}`

- [ ] **Step 4: Add aria-labels to all contact links**

Desktop email: `aria-label={`Email ${email.value}`}`
Desktop phone: `aria-label={`Call ${formatPhoneDisplay(...)}`}`
Drawer links: same pattern

- [ ] **Step 5: Replace all `mailto:`/`tel:` href constructions with `safeMailtoHref`/`safeTelHref`**

```tsx
import { safeMailtoHref, safeTelHref } from "../lib/safe-contact";
```

- [ ] **Step 6: Validate NavItem.href** — non-fragment hrefs pass through unvalidated. Move `safeHref` from `hero-utils.tsx` to `lib/safe-href.ts` (shared utility) and apply to external hrefs:

```tsx
import { safeHref } from "../lib/safe-href";
// In nav link construction:
href: item.href.startsWith("#") ? `${prefix}${item.href}` : safeHref(item.href)
```

- [ ] **Step 7: Update Nav tests** — use `ACCOUNT` fixture, fix any references to `NavItem.section` (now `.href`), update aria-label assertions.

- [ ] **Step 8: Run Nav tests, verify**

```bash
npx vitest run __tests__/components/Nav.test.tsx
```

- [ ] **Step 9: Commit**

```bash
git commit -m "fix: add focus trap, dynamic aria-label, contact aria-labels, and href validation to Nav"
```

---

### Task 3: Fix Skip Link Targeting

**Files:**
- Modify: `apps/agent-site/templates/emerald-classic.tsx`
- Modify: `apps/agent-site/templates/modern-minimal.tsx`
- Modify: `apps/agent-site/templates/warm-community.tsx`
- Modify: All other template files (7 more)
- Modify: `apps/agent-site/app/layout.tsx`
- Modify: `apps/agent-site/app/thank-you/page.tsx` (add `id="main-content"`)

- [ ] **Step 1: Read all template files** — they already use `content.pages.home.sections` (correct). Find the Nav + content wrapper structure.

- [ ] **Step 2: In each template**, wrap the content div (after Nav) with `id="main-content" tabIndex={-1}`:

```tsx
<>
  <Nav account={account} ... />
  <div id="main-content" tabIndex={-1}>
    {/* sections */}
  </div>
</>
```

- [ ] **Step 3: Remove `id="main-content"` from `<main>` in layout.tsx** (if present)

- [ ] **Step 4: Add `id="main-content"` to thank-you page wrapper** (C-MEDIUM from review — skip link fails on non-template pages)

- [ ] **Step 5: Update template tests** — add skip-link assertions

- [ ] **Step 6: Run template tests**

```bash
npx vitest run __tests__/templates/
```

- [ ] **Step 7: Commit**

---

### Task 4: Fix Hero/Footer Contrast + Arrows

**Files:**
- Modify: `apps/agent-site/components/sections/heroes/HeroCentered.tsx`
- Modify: `apps/agent-site/components/sections/heroes/HeroSplit.tsx`
- Modify: `apps/agent-site/components/sections/heroes/HeroGradient.tsx`
- Modify: `apps/agent-site/components/sections/shared/Footer.tsx`
- Modify: `apps/agent-site/components/sections/shared/CmaSection.tsx`

- [ ] **Step 1: Apply contrast fixes** (same values as original plan):
  - HeroCentered body: `#8B7355` → `#6B5040`
  - HeroSplit body: `#888` → `#767676`
  - Footer links: `rgba(255,255,255,0.7)` → `rgba(255,255,255,0.85)`
  - Footer copyright opacity: `0.6` → `0.85`
  - Footer font: `11px` → `12px`

- [ ] **Step 2: Wrap CTA arrows** in `<span aria-hidden="true"> →</span>` in all 3 heroes

- [ ] **Step 3: Apply `safeMailtoHref`/`safeTelHref` to Footer.tsx** contact links

- [ ] **Step 4: Run hero/footer tests**

- [ ] **Step 5: Commit**

---

### Task 5: Fix About Components

**Files:**
- Modify: `apps/agent-site/components/sections/about/AboutCard.tsx`
- Modify: `apps/agent-site/components/sections/about/AboutMinimal.tsx`
- Modify: `apps/agent-site/components/sections/about/AboutSplit.tsx`

- [ ] **Step 1: AboutCard** — make phone/email into `<a>` links using `safeTelHref`/`safeMailtoHref`, replace credentials div with `<ul aria-label="Credentials">`

- [ ] **Step 2: AboutMinimal/AboutSplit** — fix headshot alt to `Photo of ${name}`, fix credentials contrast `#888` → `#767676`

- [ ] **Step 3: Run about tests, commit**

---

### Task 6: Fix Thank-You Page (email validation + safeTelHref + skip link target)

**Files:**
- Modify: `apps/agent-site/app/thank-you/page.tsx`
- Create or Modify: `apps/agent-site/__tests__/pages/thank-you.test.tsx`

**IMPORTANT:** On `main`, thank-you page uses:
- `loadAccountConfig(id)` (NOT `loadAgentConfig`)
- `account.agent?.phone` (NOT `agent.identity.phone`)
- `account.agent?.name` (NOT `agent.identity.name`)
- The `tel:` href at ~line 91 uses raw `phone.replace(/\D/g, "")` — needs `safeTelHref`

- [ ] **Step 1: Read the actual thank-you/page.tsx** to confirm current code shape

- [ ] **Step 2: Add email validation** — after extracting `email` from searchParams:
```tsx
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const validEmail = email && EMAIL_RE.test(email) && email.length <= 254 ? email : null;
```
Use `validEmail` instead of `email` in JSX.

- [ ] **Step 3: Replace raw `tel:` href** with `safeTelHref`:
```tsx
import { safeTelHref } from "../lib/safe-contact";
// Replace: href={`tel:${phone.replace(/\D/g, "")}`}
// With:    href={safeTelHref(phone)}
```

- [ ] **Step 4: Add skip link target** — add `id="main-content" tabIndex={-1}` to the content wrapper div (after Nav) so the skip link works on this page too.

- [ ] **Step 5: Write tests** using `AccountConfig` shape — mock `loadAccountConfig` (not `loadAgentConfig`), use `account.agent?.phone` in assertions.

- [ ] **Step 6: Run tests, commit**

---

### Task 7: Fix Legal Pages (agentId resolution + mailto sanitization)

**Files:**
- Modify: `apps/agent-site/app/privacy/page.tsx`
- Modify: `apps/agent-site/app/terms/page.tsx`

- [ ] **Step 1: Align `resolveAgentId`** with main page.tsx pattern (ignore query param in production without PREVIEW)

- [ ] **Step 2: Sanitize email in markdown template** — replace raw `mailto:${identity.email}` with `safeMailtoHref()` output (H-2 from code review)

- [ ] **Step 3: Run tests, commit**

---

### Task 8: Fix JSON-LD Escape in layout.tsx

**File:** `apps/agent-site/app/layout.tsx`

- [ ] **Step 1: Add `.replace(/<\/script>/gi, "<\\/script>")` to JSON-LD**

- [ ] **Step 2: Commit**

---

## Phase 2: Fix Critical Regressions from Code Review

### Task 9: Fix Analytics Inline Scripts + AccountTracking Type (C-1, C-2, S-H5)

**Files:**
- Modify: `apps/agent-site/components/Analytics.tsx`
- Modify: `apps/agent-site/__tests__/components/Analytics.test.tsx`

**IMPORTANT:** On `main`, Analytics.tsx already uses `AccountTracking` (not `AgentTracking`). It still references `/scripts/ga4-init.js` and `/scripts/meta-pixel-init.js` which don't exist (silent 404). This task was NOT cherry-picked because the ada-security version imported the wrong type.

- [ ] **Step 1: Read Analytics.tsx** — confirm it uses `AccountTracking` and has external script references.

- [ ] **Step 2: Replace GA4 init script** — change from `src={/scripts/ga4-init.js?id=${gaId}}` to inline:
```tsx
<Script id="ga4-config" strategy="afterInteractive">
  {`window.dataLayer=window.dataLayer||[];function gtag(){dataLayer.push(arguments);}gtag('js',new Date());gtag('config','${gaId}');`}
</Script>
```

- [ ] **Step 3: Replace Meta Pixel init script** — change from `src={/scripts/meta-pixel-init.js?id=${pixelId}}` to inline:
```tsx
<Script id="meta-pixel" strategy="afterInteractive">
  {`!function(f,b,e,v,n,t,s){if(f.fbq)return;n=f.fbq=function(){n.callMethod?n.callMethod.apply(n,arguments):n.queue.push(arguments)};if(!f._fbq)f._fbq=n;n.push=n;n.loaded=!0;n.version='2.0';n.queue=[];t=b.createElement(e);t.async=!0;t.src=v;s=b.getElementsByTagName(e)[0];s.parentNode.insertBefore(t,s)}(window,document,'script','https://connect.facebook.net/en_US/fbevents.js');fbq('init','${pixelId}');fbq('track','PageView');`}
</Script>
```

Note: Both IDs are already validated by `safeId()` regex, so inline interpolation is safe.

- [ ] **Step 4: Update Analytics tests** — the test mock renders `data-src` from the `src` prop. Update the mock to also capture `children` and update the GA4/Pixel test assertions to check `data-content` instead of `data-src`.

- [ ] **Step 5: Run Analytics tests, commit**

```bash
npx vitest run __tests__/components/Analytics.test.tsx
git commit -m "fix: replace missing analytics init scripts with inline initialization"
```

---

### Task 10: Fix `useFocusTrap` Edge Case (H-4)

**File:** `apps/agent-site/lib/use-focus-trap.ts`

- [ ] **Step 1: Guard against null container before setting previousFocusRef**

```ts
useEffect(() => {
  if (!active) return;

  const container = containerRef.current;
  if (!container) return;

  // Only save previous focus AFTER confirming container exists
  previousFocusRef.current = document.activeElement as HTMLElement;

  const focusable = container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR);
  if (focusable.length > 0) {
    focusable[0].focus();
  }

  document.addEventListener("keydown", handleKeyDown);

  return () => {
    document.removeEventListener("keydown", handleKeyDown);
    previousFocusRef.current?.focus();
    previousFocusRef.current = null;
  };
}, [active, handleKeyDown]);
```

- [ ] **Step 2: Run hook tests, commit**

---

### Task 11: Fix `safeCspUrl` to Block HTTP in Production (M from review)

**File:** `apps/agent-site/lib/security-headers.ts`

- [ ] **Step 1: Update regex to only allow https, with localhost exception**

```ts
const SAFE_HTTPS_URL = /^https:\/\/[a-zA-Z0-9._:-]+$/;
const SAFE_LOCALHOST = /^http:\/\/localhost(:\d+)?$/;

export function safeCspUrl(url: string | undefined): string {
  if (!url) return "";
  if (SAFE_HTTPS_URL.test(url) || SAFE_LOCALHOST.test(url)) return url;
  return "";
}
```

- [ ] **Step 2: Update middleware test that uses `http://localhost:5135`** — should still pass

- [ ] **Step 3: Run middleware tests, commit**

---

## Phase 3: Dead Code & Unused Config Cleanup

### Task 12: Remove Old Config Schema Files

**Files:**
- Delete: `config/agent.schema.json` (old format, not used by accounts)
- Delete: `config/agent-content.schema.json` (old format)

- [ ] **Step 1: Verify nothing imports these files**

```bash
grep -r "agent.schema.json" apps/ packages/ scripts/ --include="*.ts" --include="*.mjs" --include="*.json"
grep -r "agent-content.schema.json" apps/ packages/ scripts/ --include="*.ts" --include="*.mjs" --include="*.json"
```

- [ ] **Step 2: Update CLAUDE.md** — the root `CLAUDE.md` references `config/agents/{agent-id}.json` and `config/agent.schema.json`. Update these references to the new `config/accounts/` structure.

- [ ] **Step 3: Delete if unused, commit**

```bash
git rm config/agent.schema.json config/agent-content.schema.json
git add CLAUDE.md
git commit -m "chore: remove old agent config schema files, update CLAUDE.md references"
```

---

### Task 13: Remove Old `config/agents/` Directory (if exists)

- [ ] **Step 1: Check if `config/agents/` exists**

```bash
ls config/agents/ 2>/dev/null
```

- [ ] **Step 2: If it exists with old-format agent configs, delete**

These were from `feat/branding-test-agents` and have been migrated to `config/accounts/`. Verify each old agent has a corresponding account before deleting.

- [ ] **Step 3: Commit**

---

### Task 14: Remove Deprecated Type Aliases

**File:** `apps/agent-site/lib/types.ts`

- [ ] **Step 1: Check for `@deprecated` aliases**

Look for lines like:
```ts
/** @deprecated */ export type ServiceItem = FeatureItem;
/** @deprecated */ export type SoldHomeItem = GalleryItem;
```

- [ ] **Step 2: Search codebase for usages of deprecated names**

```bash
grep -r "ServiceItem\|SoldHomeItem\|CmaFormData" apps/agent-site/components/ apps/agent-site/templates/ --include="*.ts" --include="*.tsx"
```

- [ ] **Step 3: If no usages remain, remove the aliases. If usages exist, update them to new names first.**

- [ ] **Step 4: Commit**

---

### Task 15: Remove Unused Worktree Branches

- [ ] **Step 1: List all worktree branches**

```bash
git branch --list "worktree-agent-*"
```

- [ ] **Step 2: Delete branches that have no associated worktree (use lowercase -d for safety — refuses to delete unmerged branches)**

```bash
git worktree list  # check which are active
# For each orphan: git branch -d worktree-agent-XXXX
# If a branch is truly unmerged but obsolete, use -D only after manual confirmation
```

- [ ] **Step 3: Clean up stale worktree references**

```bash
git worktree prune
```

---

### Task 16: Remove Unused CI Workflows

- [ ] **Step 1: Check for orphaned workflow files**

```bash
ls .github/workflows/
```

Look for: `deploy-agent-site.yml` (if deployment moved to a different workflow), any disabled or outdated workflows.

- [ ] **Step 2: Verify each workflow is referenced or active before removing**

- [ ] **Step 3: Commit removals**

---

### Task 17: Ensure Test Agent Per Template

Verify every registered template has a corresponding test account config. Currently:

| Template | Test Agent | Status |
|----------|-----------|--------|
| `emerald-classic` | `test-emerald` | Exists |
| `modern-minimal` | `test-modern` | Exists |
| `warm-community` | `test-warm` | Exists |

- [ ] **Step 1: Read `apps/agent-site/templates/index.ts`** — list all registered templates

- [ ] **Step 2: For each template, verify a test account exists** in `config/accounts/` with `"template": "<name>"` in its `account.json`.

- [ ] **Step 3: If any template lacks a test agent, create one** by copying `config/accounts/test-emerald/` and updating:
  - `account.json`: change `template`, `branding` colors, `agent.name/phone/email`
  - `content.json`: customize section content for that template's style

- [ ] **Step 4: Verify the config registry generates correctly**

```bash
cd apps/agent-site && node scripts/generate-config-registry.mjs
```

- [ ] **Step 5: Commit any new test accounts**

---

## Phase 4: Final Verification

### Task 18: Full Test Suite + Lint + Build

- [ ] **Step 1: Run full agent-site tests**

```bash
cd apps/agent-site && npx vitest run --coverage
```
Expected: ALL PASS (0 failures — the pre-existing failures should be fixed by using correct types)

- [ ] **Step 2: Run packages/ui tests**

```bash
cd packages/ui && npx vitest run
```

- [ ] **Step 3: Run lint**

```bash
cd apps/agent-site && npx next lint
```

- [ ] **Step 4: Build check**

```bash
cd apps/agent-site && npx next build
```

- [ ] **Step 5: Commit any auto-fixes**

---

## Summary

| Phase | Tasks | Focus |
|-------|-------|-------|
| 0 | Task 1 | Branch setup + cherry-pick 9 clean commits |
| 1 | Tasks 2-8 | Re-implement skipped fixes against correct AccountConfig types |
| 2 | Tasks 9-11 | Analytics fix, useFocusTrap edge case, safeCspUrl hardening |
| 3 | Tasks 12-17 | Dead code, unused configs, deprecated aliases, orphan branches, test agent coverage |
| 4 | Task 18 | Full verification |

**Total: 18 tasks, estimated 17 commits.**

**Key difference from v1 plan:** This plan works WITH the pages-architecture types instead of fighting them. No type migration needed — `main` already has the correct `AccountConfig`/`ContentConfig`/`NavItem` types. All tasks reference the actual `main` code (prop names, function signatures, type imports).

**Review fixes applied (v2):**
- C-1: Analytics commits moved to DO NOT cherry-pick; re-implemented manually as Task 9
- C-2: Task 6 rewritten against `main` actual code (`loadAccountConfig`, `account.agent?.phone`)
- I-1: Task 2 updated for `account` prop (not `agent`), `enabledSections`, `ACCOUNT` fixture
- I-3: Covered by C-1 — analytics fix is now manual re-implementation
- S-1: Task 12 includes CLAUDE.md reference update
- S-2: Task 2 Step 6 moves `safeHref` to shared `lib/safe-href.ts`
- S-3: Task 15 uses `git branch -d` (safe) instead of `-D`
- NEW: Task 17 ensures every template has a test agent config

> **Timing:** Execute immediately — the `main` branch is the correct base with pages-architecture fully integrated.
