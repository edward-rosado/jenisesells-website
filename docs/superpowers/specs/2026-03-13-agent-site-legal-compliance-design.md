# Agent-Site Legal Compliance + Config Directory Migration

**Date:** 2026-03-13
**Status:** Approved
**Author:** Eddie Rosado + Claude

## Overview

Two changes in one PR:

1. **Config directory migration** — move from flat agent config files to per-agent directories, laying the groundwork for the future template architecture
2. **Legal compliance pages** — add personalized Privacy, Terms, Accessibility pages and Cookie Consent Banner to every agent-site

## Decision: Per-Agent Directory Structure

Migrate from flat files to per-agent directories. This sets the foundation for future template work (5 templates with shared components and template-specific overrides).

```
BEFORE (flat)                         AFTER (per-agent directories)
=============                         ============================

config/agents/                        config/agents/
  jenise-buckalew.json                  jenise-buckalew/
  jenise-buckalew.content.json            config.json          ← was .json
                                          content.json         ← was .content.json
                                          legal/               ← NEW
                                            privacy-above.md
                                            terms-below.md
```

The config loader (`lib/config.ts`) is updated to read from the new paths. This is a backwards-compatible migration — if the directory doesn't exist, fall back to the flat file path.

### Future Template Architecture (NOT in this PR)

This directory structure is designed to support the future template system:

```
apps/agent-site/
  templates/
    emerald-classic/              ← each template gets its own directory
      Template.tsx                ← layout + section ordering
      sections/                   ← template-specific section overrides
        Hero.tsx                  ← custom Hero for this template only
    midnight-modern/
      Template.tsx
      sections/
        Hero.tsx
    coastal-blue/
    bold-agent/
    minimal-pro/

  components/
    shared/                       ← components ALL templates use (never overridden)
      Nav.tsx
      Footer.tsx
      CookieConsentBanner.tsx
      SkipNav.tsx
    sections/                     ← default section implementations
      Hero.tsx                    ← fallback if template has no override
      CmaForm.tsx
      About.tsx
      ...
    legal/                        ← legal pages (always shared)
      LegalPageLayout.tsx
      MarkdownContent.tsx
      constants.ts
```

```
┌──────────────────────────────────────────────────────────────┐
│          FUTURE: COMPONENT RESOLUTION ORDER                   │
│                                                              │
│  When a template renders <Hero>:                             │
│                                                              │
│  1. Check template's own sections/                           │
│     templates/emerald-classic/sections/Hero.tsx               │
│              │                                               │
│              ├── EXISTS? → Use template-specific Hero         │
│              │                                               │
│              └── NOT FOUND? ↓                                │
│                                                              │
│  2. Fall back to shared default                              │
│     components/sections/Hero.tsx                             │
│              │                                               │
│              └── Use default Hero                            │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ALWAYS from shared/ (never overridden by templates):        │
│                                                              │
│     components/shared/Nav.tsx                                │
│     components/shared/Footer.tsx                             │
│     components/shared/CookieConsentBanner.tsx                │
│     components/shared/SkipNav.tsx                            │
│     components/legal/*                                       │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

**This PR only creates the directory structure and legal components.** The template override system, `components/shared/` rename, and additional templates are future work.

## Decision: Template Components (Option A)

Legal pages are **React template components** with agent identity interpolated from `AgentConfig`. The legal structure is identical across all agents — only names/contact/brokerage change. This keeps legal language consistent (we control liability).

## Decision: Optional Custom Legal Sections

Each legal page supports optional custom markdown content rendered above and/or below the standard boilerplate. This allows agents to add brokerage-specific disclosures, state-required addenda, or other legal language.

Custom content lives as **markdown files** in the agent's directory:

```
config/agents/jenise-buckalew/
  legal/
    privacy-above.md        ← optional, renders above standard privacy content
    privacy-below.md        ← optional, renders below standard privacy content
    terms-above.md
    terms-below.md
    accessibility-above.md
    accessibility-below.md
```

- Files are **optional** — if absent, nothing renders (no empty divs or spacing)
- Content is **markdown**, rendered via `react-markdown`
- Naming convention: `{page}-{position}.md`
- The loader reads the file if it exists, returns `undefined` if not

## Decision: Markdown Rendering for All Legal Pages

**Dependency:** `react-markdown` must be added to `apps/agent-site/package.json` (already used in `apps/platform`).

All legal page content (both the standard template text and custom sections) is rendered as markdown using `react-markdown`. This means:
- Standard legal boilerplate is stored as markdown strings in template constants
- Custom above/below sections are markdown files from the agent's `legal/` directory
- Both flow through the same `react-markdown` renderer with consistent styling
- Headings, lists, links, bold/italic, and tables are all supported

## Decision: Agent Brand Colors for Cookie Banner (Option A)

The cookie consent banner uses `var(--color-primary)` and `var(--color-accent)` CSS custom properties so it matches the agent's brand. This looks more professional than a generic dark/light theme.

## Pages

### `/privacy` — Consumer Privacy Policy
- Who collects data: the agent (by name, brokerage) and Real Estate Star (platform operator)
- What data: CMA form fields (property address, email, phone, name)
- How it's processed: sent to Real Estate Star platform for market analysis
- Data sharing: with the agent, with service providers (no sale to third parties)
- Cookies/local storage: cookie consent preference, analytics (if configured)
- CCPA rights: right to know, delete, opt-out
- Children's privacy: not directed at children under 13
- Contact: agent's email from config

### `/terms` — Terms of Use
- CMA disclaimer: estimates only, not appraisals, not guaranteed
- MLS data accuracy disclaimer
- Fair housing commitment
- Intellectual property (agent's content, Real Estate Star platform)
- Limitation of liability
- Governing law: uses a `STATE_NAMES` mapping (e.g. `"NJ"` → `"New Jersey"`) to render full state name in legal text. `agent.location.state` stores the abbreviation; legal clauses read: "governed by the laws of the State of New Jersey"
- Contact: agent's email

### `/accessibility` — Accessibility Statement
- WCAG 2.1 Level AA commitment
- Known limitations
- Contact for accessibility issues: agent's email
- Third-party content disclaimer

## Components

### `components/legal/LegalPageLayout.tsx`
Shared wrapper for all legal pages. Props:
- `agent: AgentConfig` — for Nav, Footer, branding
- `agentId: string` — for cookie banner key
- `children` — the standard legal content (markdown rendered)
- `customAbove?: string` — optional markdown rendered above standard content
- `customBelow?: string` — optional markdown rendered below standard content

Calls `buildCssVariableStyle(agent.branding)` and injects as `style={cssVars}` on root `<div>`. Renders: Nav → skip target → `customAbove` (if present, via `react-markdown`) → `{children}` → `customBelow` (if present, via `react-markdown`) → Footer → CookieConsentBanner. All inside the CSS-variable-scoped wrapper.

### `components/legal/MarkdownContent.tsx`
Thin wrapper around `react-markdown` with prose styling (headings, lists, links, tables). Used by both the standard legal template content and the custom above/below sections. Applies consistent typography: `prose prose-invert` (dark theme) or agent-branded styles.

### `components/legal/CookieConsentBanner.tsx`
Port of platform's banner (`apps/platform/components/legal/CookieConsentBanner.tsx`) adapted for agent branding:
- `var(--color-primary)` background
- `var(--color-accent)` accept button
- Same `useSyncExternalStore` + localStorage pattern
- **Per-agent localStorage key**: `res-cookie-consent-{agentId}` — prevents cross-site consent leakage (a user who accepted on the platform shouldn't be silently consented on every agent site). The `agentId` is passed as a prop.
- Privacy link points to `/privacy` (agent-site's own page)
- `role="dialog"`, `aria-label="Cookie consent"`, labeled buttons

## Layout Changes

### `app/layout.tsx`
- Add skip-nav link: `<a href="#main-content">Skip to main content</a>` with CSS focus styles
- Add `<div id="main-content" tabIndex={-1}>` wrapper
- **No `CookieConsentBanner` here** — the banner is rendered inside `LegalPageLayout` and the main page template (inside the CSS-variable-scoped `<div>`) so it inherits agent brand colors. The root layout has no access to `searchParams` or agent config.

### `components/sections/Footer.tsx`
- Add `<nav aria-label="Legal links">` with static `<Link>` components pointing to `/privacy`, `/terms`, `/accessibility` (no config data needed — paths are fixed)
- Placed between EHO section and copyright line

## Data Flow

```
┌─────────────────────────────────────────────────────────────┐
│                     INCOMING REQUEST                        │
│            jenise-buckalew.realestatestar.com/privacy        │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌──────────────────────────────────────────────────────────────┐
│                     middleware.ts                             │
│                                                              │
│  1. Extract hostname: "jenise-buckalew.realestatestar.com"   │
│                           │                                  │
│                           ▼                                  │
│  2. extractAgentId(hostname)     ◄── lib/routing.ts          │
│     Strip port → check BASE_DOMAINS → extract subdomain      │
│     → "jenise-buckalew"                                      │
│                           │                                  │
│                           ▼                                  │
│  3. Rewrite URL: /privacy?agentId=jenise-buckalew            │
│     + Generate CSP nonce                                     │
│     + Set Content-Security-Policy header                     │
└──────────────────────────┬───────────────────────────────────┘
                           │
                           ▼
┌──────────────────────────────────────────────────────────────┐
│                   app/privacy/page.tsx                        │
│                                                              │
│  1. Read searchParams.agentId → "jenise-buckalew"            │
│                           │                                  │
│                           ▼                                  │
│  2. loadAgentConfig("jenise-buckalew")  ◄── lib/config.ts    │
│     Try: config/agents/jenise-buckalew/config.json           │
│     Fallback: config/agents/jenise-buckalew.json             │
│     Validate ID (regex) → assert required fields → return    │
│                           │                                  │
│                           ▼                                  │
│  3. Interpolate agent data into legal template:              │
│     agent.identity.name  → "Jenise Buckalew"                 │
│     agent.identity.email → contact email                     │
│     agent.identity.brokerage → brokerage name                │
│     agent.location.state → governing law state               │
│                           │                                  │
│                           ▼                                  │
│  4. loadLegalContent("jenise-buckalew", "privacy")           │
│     Try: config/agents/jenise-buckalew/legal/privacy-above.md│
│     Try: config/agents/jenise-buckalew/legal/privacy-below.md│
│     Returns { above?: string, below?: string }               │
└──────────────────────────┬───────────────────────────────────┘
                           │
                           ▼
┌──────────────────────────────────────────────────────────────┐
│                    RENDERED HTML                              │
│                                                              │
│  ┌─────────────── Nav ──────────────────┐                    │
│  │  Skip-nav link → #main-content       │                    │
│  └──────────────────────────────────────┘                    │
│  ┌─────────────── Content ──────────────┐                    │
│  │  ┌── privacy-above.md (optional) ──┐ │                    │
│  │  │  "## Brokerage Disclosure..."    │ │                    │
│  │  └─────────────────────────────────┘ │                    │
│  │  Privacy Policy (standard template)  │                    │
│  │  "Jenise Buckalew ('Agent') of       │                    │
│  │   RE/MAX First... collects data..."  │                    │
│  │  ┌── privacy-below.md (optional) ──┐ │                    │
│  │  │  "## NJ-Specific Disclosures"   │ │                    │
│  │  └─────────────────────────────────┘ │                    │
│  └──────────────────────────────────────┘                    │
│  ┌─────────────── Footer ───────────────┐                    │
│  │  Legal links: /privacy /terms        │                    │
│  │  /accessibility                      │                    │
│  │  EHO logo │ © Jenise Buckalew        │                    │
│  └──────────────────────────────────────┘                    │
│  ┌─────────── CookieConsentBanner ──────┐                    │
│  │  Uses var(--color-primary/accent)    │                    │
│  │  Key: res-cookie-consent-jenise-...  │                    │
│  │  Links to /privacy (agent's own)     │                    │
│  └──────────────────────────────────────┘                    │
└──────────────────────────────────────────────────────────────┘
```

## Config Loader Changes

### `lib/config.ts`

Update `loadAgentConfig` and `loadAgentContent` to support both directory and flat file layouts:

```typescript
// Resolution order:
// 1. config/agents/{id}/config.json     (new directory layout)
// 2. config/agents/{id}.json            (legacy flat layout)

async function resolveConfigPath(agentId: string): Promise<string> {
  const dirPath = path.join(CONFIG_DIR, agentId, "config.json");
  try {
    await access(dirPath);
    return dirPath;
  } catch {
    return path.join(CONFIG_DIR, `${agentId}.json`);
  }
}
```

Add a new `loadLegalContent` function:

```typescript
// Reads optional markdown files from config/agents/{id}/legal/
export async function loadLegalContent(
  agentId: string,
  page: "privacy" | "terms" | "accessibility",
): Promise<{ above?: string; below?: string }> {
  validateAgentId(agentId);
  const legalDir = path.join(CONFIG_DIR, agentId, "legal");
  const [above, below] = await Promise.all([
    readFileSafe(path.join(legalDir, `${page}-above.md`)),
    readFileSafe(path.join(legalDir, `${page}-below.md`)),
  ]);
  return { above, below };
}

async function readFileSafe(filePath: string): Promise<string | undefined> {
  try {
    return await readFile(filePath, "utf-8");
  } catch {
    return undefined;
  }
}
```

## Agent Config Fields Used

All fields come from existing `AgentConfig` — no schema changes needed.

| Field | Used In |
|-------|---------|
| `identity.name` | All pages — agent name |
| `identity.email` | All pages — contact email |
| `identity.phone` | Privacy, Terms — contact phone |
| `identity.brokerage` | Privacy, Terms — brokerage name |
| `identity.license_id` | Terms — license reference |
| `location.state` | Terms — governing law jurisdiction |
| `location.service_areas` | Privacy — geographic scope |
| `branding.*` | Cookie banner — brand colors via CSS vars |

## Constants

All legal pages share a single effective date constant to make future updates easy:

```tsx
// components/legal/constants.ts
export const LEGAL_EFFECTIVE_DATE = "March 13, 2026";
```

A `STATE_NAMES` mapping converts two-letter abbreviations to full names for legal clauses:

```tsx
export const STATE_NAMES: Record<string, string> = {
  NJ: "New Jersey",
  NY: "New York",
  // ... all 50 states
};
```

## Testing Plan

### Config Loader Migration
- `loadAgentConfig` reads from `{id}/config.json` when directory exists
- `loadAgentConfig` falls back to `{id}.json` when directory doesn't exist
- `loadAgentContent` reads from `{id}/content.json` when directory exists
- `loadAgentContent` falls back to `{id}.content.json` when directory doesn't exist
- `loadLegalContent` returns markdown content when files exist
- `loadLegalContent` returns `undefined` for above/below when files don't exist
- `loadLegalContent` validates agentId (rejects path traversal attempts)

### Legal Pages
- Each page renders with agent config and displays agent name/email/brokerage
- Each page has correct `<title>` metadata that includes the agent's name (e.g. `"Privacy Policy | Jenise Buckalew"`)
- Pages call `notFound()` when `loadAgentConfig` throws (agent not found) — test the 404 branch explicitly
- Effective date renders from the `LEGAL_EFFECTIVE_DATE` constant
- `custom_above` markdown renders above standard content when provided
- `custom_below` markdown renders below standard content when provided
- Neither custom section renders when absent/empty (no empty divs or spacing)
- Markdown headings, lists, links, and bold render correctly in both standard and custom content

### Cookie Consent Banner
- Accept persists `"accepted"` to localStorage under `res-cookie-consent-{agentId}`
- Decline persists `"declined"` to localStorage under `res-cookie-consent-{agentId}`
- Banner hidden after accept/decline
- Banner hidden on return visit (consent already stored)
- Uses brand color CSS variables
- Links to `/privacy`
- Server snapshot returns `"pending"` (test the `useSyncExternalStore` SSR path for coverage)

### Footer
- Legal links render with correct hrefs (`/privacy`, `/terms`, `/accessibility`)
- Links have `aria-label` attributes
- `<nav>` has `aria-label="Legal links"`

### Layout
- Skip-nav link present and focusable
- `#main-content` target exists

### Accessibility
- All pages have proper heading hierarchy (single `<h1>`)
- All links have descriptive text or aria-labels
- Cookie banner has `role="dialog"` and `aria-label`
- Skip-nav works with keyboard (Tab → Enter → focus moves to main content)

## Files to Create
- `config/agents/jenise-buckalew/config.json` — moved from `jenise-buckalew.json`
- `config/agents/jenise-buckalew/content.json` — moved from `jenise-buckalew.content.json`
- `config/agents/jenise-buckalew/legal/` — empty directory (no custom legal content for now)
- `apps/agent-site/app/privacy/page.tsx`
- `apps/agent-site/app/terms/page.tsx`
- `apps/agent-site/app/accessibility/page.tsx`
- `apps/agent-site/components/legal/LegalPageLayout.tsx`
- `apps/agent-site/components/legal/CookieConsentBanner.tsx`
- `apps/agent-site/components/legal/MarkdownContent.tsx` — shared `react-markdown` wrapper with prose styling
- `apps/agent-site/components/legal/constants.ts` — `LEGAL_EFFECTIVE_DATE`, `STATE_NAMES`

## Files to Modify
- `apps/agent-site/lib/config.ts` — directory-first resolution, `loadLegalContent()`, backwards-compatible fallback
- `apps/agent-site/app/layout.tsx` — skip-nav, main-content target (no cookie banner here)
- `apps/agent-site/app/globals.css` — skip-nav focus styles
- `apps/agent-site/app/page.tsx` — add `<CookieConsentBanner>` inside CSS-variable-scoped `<div>`
- `apps/agent-site/app/thank-you/page.tsx` — add `<CookieConsentBanner>` inside CSS-variable-scoped `<div>`
- `apps/agent-site/components/sections/Footer.tsx` — legal links nav
- `apps/agent-site/package.json` — add `react-markdown` dependency

## Files to Delete (after migration)
- `config/agents/jenise-buckalew.json` — replaced by `jenise-buckalew/config.json`
- `config/agents/jenise-buckalew.content.json` — replaced by `jenise-buckalew/content.json`

## Not In Scope
- DMCA page on agent-sites (disputes go through platform's DMCA process)
- Full replacement of standard legal text (agents can add above/below, not replace the core boilerplate)
- GDPR/ePrivacy cookie inventory (future, if EU users targeted)
- `lang` attribute localization (currently hardcoded `"en"` — future work if non-English agent sites are needed)
- SEO `X-Robots-Tag` or canonical headers for legal pages
- `next.config.ts` header modifications
- Template override system (`components/shared/` rename, template-specific sections)
- Additional templates beyond `emerald-classic`
- `components/sections/` → `components/shared/` directory rename
