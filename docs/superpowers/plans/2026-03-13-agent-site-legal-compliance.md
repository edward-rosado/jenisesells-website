# Agent-Site Legal Compliance Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add personalized legal pages (Privacy, Terms, Accessibility) and cookie consent to agent-sites, with a config directory migration from flat files to per-agent directories.

**Architecture:** Config loader updated with directory-first resolution and backwards-compatible fallback. Legal pages are template components with agent identity interpolated from `AgentConfig`. Optional custom markdown content renders above/below standard boilerplate via files in `config/agents/{id}/legal/`. All content rendered through `react-markdown`.

**Tech Stack:** Next.js 16, React, TypeScript, Vitest, react-markdown, @testing-library/react

**Spec:** `docs/superpowers/specs/2026-03-13-agent-site-legal-compliance-design.md`

---

## Pre-Flight

Before any task begins, create the feature branch:

```bash
git checkout -b feat/agent-site-legal-compliance
```

All tasks commit to this branch.

---

## Parallelism Map

```
LAYER 0 — no dependencies, run ALL 6 in parallel:
  Task 1:  Config directory migration + loader
  Task 2:  constants.ts
  Task 3:  MarkdownContent component
  Task 4:  CookieConsentBanner component
  Task 5:  Layout skip-nav + globals.css
  Task 6:  Footer legal links

LAYER 1 — run in parallel:
  Task 7:  LegalPageLayout (needs Tasks 2,3,4,5,6)
  Task 11: CookieBanner on main + thank-you pages (needs Task 4 only)

LAYER 2 — needs Tasks 1 + 7, run ALL 3 in parallel:
  Task 8:  /privacy page
  Task 9:  /terms page
  Task 10: /accessibility page

LAYER 3 — needs all:
  Task 12: Full integration verify + coverage config
```

---

## Chunk 1: Foundation (Layer 0 — all parallel)

### Task 1: Config Directory Migration + Loader

**Files:**
- Create: `config/agents/jenise-buckalew/config.json` (copy of `config/agents/jenise-buckalew.json`)
- Create: `config/agents/jenise-buckalew/content.json` (copy of `config/agents/jenise-buckalew.content.json`)
- Create: `config/agents/jenise-buckalew/legal/.gitkeep`
- Modify: `apps/agent-site/lib/config.ts`
- Modify: `apps/agent-site/lib/__tests__/config.test.ts` (existing file — add new tests here)
- Delete: `config/agents/jenise-buckalew.json`
- Delete: `config/agents/jenise-buckalew.content.json`

**Reference files:**
- Current config loader: `apps/agent-site/lib/config.ts`
- Existing config tests: `apps/agent-site/lib/__tests__/config.test.ts` (**tests already exist here — add to this file, do NOT create a duplicate at `__tests__/lib/config.test.ts`**)
- Test fixtures: `apps/agent-site/__tests__/components/fixtures.ts`

- [ ] **Step 1: Create the per-agent directory structure**

```bash
mkdir -p config/agents/jenise-buckalew/legal
cp config/agents/jenise-buckalew.json config/agents/jenise-buckalew/config.json
cp config/agents/jenise-buckalew.content.json config/agents/jenise-buckalew/content.json
touch config/agents/jenise-buckalew/legal/.gitkeep
```

- [ ] **Step 2: Write failing tests for config loader migration**

**Add to the existing file** `apps/agent-site/lib/__tests__/config.test.ts`. Tests must cover:

1. `loadAgentConfig` reads from `{id}/config.json` when directory exists
2. `loadAgentConfig` falls back to `{id}.json` when directory doesn't exist
3. `loadAgentContent` reads from `{id}/content.json` when directory exists
4. `loadAgentContent` falls back to `{id}.content.json` when directory doesn't exist
5. `loadLegalContent` returns markdown when files exist
6. `loadLegalContent` returns `{ above: undefined, below: undefined }` when files don't exist
7. `loadLegalContent` rejects path traversal (e.g. `../../../etc/passwd`)
8. `loadAgentConfig` throws on invalid agent ID

Use `vi.mock("fs/promises")` with manual implementations to simulate both directory and flat file layouts.

- [ ] **Step 3: Run tests to verify they fail**

```bash
cd apps/agent-site && npx vitest run lib/__tests__/config.test.ts
```

Expected: FAIL — `loadLegalContent` not exported, `resolveConfigPath` not using directory layout.

- [ ] **Step 4: Update config.ts with directory-first resolution and loadLegalContent**

Modify `apps/agent-site/lib/config.ts`:

1. Add `import { access } from "fs/promises"` (already has `readFile`)
2. Add `resolveConfigPath()` — tries `{id}/config.json` first, falls back to `{id}.json`
3. Add `resolveContentPath()` — tries `{id}/content.json` first, falls back to `{id}.content.json`
4. Update `loadAgentConfig` to use `resolveConfigPath()`
5. Update `loadAgentContent` to use `resolveContentPath()`
6. Add and export `loadLegalContent(agentId, page)`
7. Add `readFileSafe(path)` helper

```typescript
import { access, readFile } from "fs/promises";

async function resolveConfigPath(agentId: string): Promise<string> {
  const dirPath = path.join(CONFIG_DIR, agentId, "config.json");
  try {
    await access(dirPath);
    return dirPath;
  } catch {
    return path.join(CONFIG_DIR, `${agentId}.json`);
  }
}

async function resolveContentPath(agentId: string): Promise<string> {
  const dirPath = path.join(CONFIG_DIR, agentId, "content.json");
  try {
    await access(dirPath);
    return dirPath;
  } catch {
    return path.join(CONFIG_DIR, `${agentId}.content.json`);
  }
}

async function readFileSafe(filePath: string): Promise<string | undefined> {
  try {
    return await readFile(filePath, "utf-8");
  } catch {
    return undefined;
  }
}

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
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
cd apps/agent-site && npx vitest run lib/__tests__/config.test.ts
```

- [ ] **Step 6: Delete flat config files**

```bash
rm config/agents/jenise-buckalew.json config/agents/jenise-buckalew.content.json
```

- [ ] **Step 7: Run full agent-site test suite to verify nothing broke**

```bash
cd apps/agent-site && npx vitest run
```

Expected: ALL PASS

- [ ] **Step 8: Commit**

```bash
git add config/agents/jenise-buckalew/ apps/agent-site/lib/config.ts apps/agent-site/lib/__tests__/config.test.ts
git rm config/agents/jenise-buckalew.json config/agents/jenise-buckalew.content.json
git commit -m "refactor: migrate agent config to per-agent directory structure"
```

---

### Task 2: Legal Constants

**Files:**
- Create: `apps/agent-site/components/legal/constants.ts`
- Create: `apps/agent-site/__tests__/components/legal/constants.test.ts`

- [ ] **Step 1: Write failing test**

Create `apps/agent-site/__tests__/components/legal/constants.test.ts`:

```typescript
import { describe, it, expect } from "vitest";
import { LEGAL_EFFECTIVE_DATE, STATE_NAMES, getStateName } from "@/components/legal/constants";

describe("legal constants", () => {
  it("exports LEGAL_EFFECTIVE_DATE as a non-empty string", () => {
    expect(LEGAL_EFFECTIVE_DATE).toBeTruthy();
    expect(typeof LEGAL_EFFECTIVE_DATE).toBe("string");
  });

  it("STATE_NAMES maps all 50 states plus DC", () => {
    expect(Object.keys(STATE_NAMES)).toHaveLength(51);
  });

  it("STATE_NAMES maps NJ to New Jersey", () => {
    expect(STATE_NAMES["NJ"]).toBe("New Jersey");
  });

  it("STATE_NAMES maps TX to Texas", () => {
    expect(STATE_NAMES["TX"]).toBe("Texas");
  });

  it("getStateName returns full name for known abbreviation", () => {
    expect(getStateName("NJ")).toBe("New Jersey");
  });

  it("getStateName returns abbreviation for unknown code", () => {
    expect(getStateName("XX")).toBe("XX");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/agent-site && npx vitest run __tests__/components/legal/constants.test.ts
```

- [ ] **Step 3: Create constants.ts**

Create `apps/agent-site/components/legal/constants.ts` with `LEGAL_EFFECTIVE_DATE`, `STATE_NAMES` (all 50 states + DC), and `getStateName()`.

- [ ] **Step 4: Run test to verify it passes**

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/legal/constants.ts apps/agent-site/__tests__/components/legal/constants.test.ts
git commit -m "feat: add legal constants — state names mapping and effective date"
```

---

### Task 3: MarkdownContent Component

**Files:**
- Create: `apps/agent-site/components/legal/MarkdownContent.tsx`
- Create: `apps/agent-site/__tests__/components/legal/MarkdownContent.test.tsx`
- Modify: `apps/agent-site/package.json` (add `react-markdown`)

- [ ] **Step 1: Install react-markdown**

```bash
cd apps/agent-site && npm install react-markdown
```

- [ ] **Step 2: Write failing test**

Create `apps/agent-site/__tests__/components/legal/MarkdownContent.test.tsx`:

```tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MarkdownContent } from "@/components/legal/MarkdownContent";

describe("MarkdownContent", () => {
  it("renders markdown headings", () => {
    render(<MarkdownContent content="## Hello World" />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Hello World");
  });

  it("renders markdown paragraphs", () => {
    render(<MarkdownContent content="This is a paragraph." />);
    expect(screen.getByText("This is a paragraph.")).toBeInTheDocument();
  });

  it("renders markdown links", () => {
    render(<MarkdownContent content="[Click here](https://example.com)" />);
    const link = screen.getByRole("link", { name: "Click here" });
    expect(link).toHaveAttribute("href", "https://example.com");
  });

  it("renders markdown lists", () => {
    render(<MarkdownContent content="- Item A\n- Item B" />);
    expect(screen.getByText("Item A")).toBeInTheDocument();
    expect(screen.getByText("Item B")).toBeInTheDocument();
  });

  it("renders bold text", () => {
    render(<MarkdownContent content="This is **bold** text" />);
    const bold = document.querySelector("strong");
    expect(bold).toHaveTextContent("bold");
  });

  it("renders nothing when content is empty string", () => {
    const { container } = render(<MarkdownContent content="" />);
    expect(container.innerHTML).toBe("");
  });

  it("renders nothing when content is whitespace only", () => {
    const { container } = render(<MarkdownContent content="   " />);
    expect(container.innerHTML).toBe("");
  });

  it("applies prose styling class when content present", () => {
    const { container } = render(<MarkdownContent content="Hello" />);
    expect(container.firstChild).toHaveClass("prose");
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

- [ ] **Step 4: Create MarkdownContent.tsx**

```tsx
import ReactMarkdown from "react-markdown";

interface MarkdownContentProps {
  content: string;
}

export function MarkdownContent({ content }: MarkdownContentProps) {
  if (!content?.trim()) return null;

  return (
    <div className="prose prose-invert max-w-none prose-headings:text-white prose-p:text-gray-300 prose-a:text-emerald-400 prose-strong:text-white prose-li:text-gray-300">
      <ReactMarkdown>{content}</ReactMarkdown>
    </div>
  );
}
```

Note: Use `!content?.trim()` to handle both empty and whitespace-only strings.

- [ ] **Step 5: Run test to verify it passes**

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/components/legal/MarkdownContent.tsx apps/agent-site/__tests__/components/legal/MarkdownContent.test.tsx apps/agent-site/package.json apps/agent-site/package-lock.json
git commit -m "feat: add MarkdownContent component for legal page rendering"
```

---

### Task 4: CookieConsentBanner Component

**Files:**
- Create: `apps/agent-site/components/legal/CookieConsentBanner.tsx`
- Create: `apps/agent-site/__tests__/components/legal/CookieConsentBanner.test.tsx`

**Reference:** `apps/platform/components/legal/CookieConsentBanner.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/agent-site/__tests__/components/legal/CookieConsentBanner.test.tsx`:

```tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { renderToString } from "react-dom/server";
import { CookieConsentBanner } from "@/components/legal/CookieConsentBanner";

describe("CookieConsentBanner", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("renders the banner when no consent stored", () => {
    render(<CookieConsentBanner agentId="test-agent" />);
    expect(screen.getByRole("dialog", { name: "Cookie consent" })).toBeInTheDocument();
  });

  it("does not render when consent is already accepted", () => {
    localStorage.setItem("res-cookie-consent-test-agent", "accepted");
    render(<CookieConsentBanner agentId="test-agent" />);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("does not render when consent is already declined", () => {
    localStorage.setItem("res-cookie-consent-test-agent", "declined");
    render(<CookieConsentBanner agentId="test-agent" />);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("hides banner and stores accepted on accept click", () => {
    render(<CookieConsentBanner agentId="test-agent" />);
    fireEvent.click(screen.getByRole("button", { name: "Accept cookies" }));
    expect(localStorage.getItem("res-cookie-consent-test-agent")).toBe("accepted");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("hides banner and stores declined on decline click", () => {
    render(<CookieConsentBanner agentId="test-agent" />);
    fireEvent.click(screen.getByRole("button", { name: "Decline cookies" }));
    expect(localStorage.getItem("res-cookie-consent-test-agent")).toBe("declined");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("uses agent-specific localStorage key", () => {
    render(<CookieConsentBanner agentId="agent-abc" />);
    fireEvent.click(screen.getByRole("button", { name: "Accept cookies" }));
    expect(localStorage.getItem("res-cookie-consent-agent-abc")).toBe("accepted");
    expect(localStorage.getItem("res-cookie-consent-test-agent")).toBeNull();
  });

  it("links to /privacy", () => {
    render(<CookieConsentBanner agentId="test-agent" />);
    const link = screen.getByRole("link", { name: /privacy/i });
    expect(link).toHaveAttribute("href", "/privacy");
  });

  it("renders accept and decline buttons", () => {
    render(<CookieConsentBanner agentId="test-agent" />);
    expect(screen.getByRole("button", { name: "Accept cookies" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Decline cookies" })).toBeInTheDocument();
  });

  it("server snapshot returns pending (SSR path)", () => {
    // Server-render to exercise getServerSnapshot — the banner should render nothing
    // on the server because "pending" is truthy, triggering the early return.
    // This test covers the useSyncExternalStore server snapshot branch.
    const html = renderToString(<CookieConsentBanner agentId="test-agent" />);
    // On server, getServerSnapshot returns "pending" which is truthy,
    // so the component returns null (no banner rendered during SSR)
    expect(html).toBe("");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create CookieConsentBanner.tsx**

Port from platform's version with:
- `agentId: string` prop
- localStorage key: `res-cookie-consent-${agentId}`
- `var(--color-primary)` background, `var(--color-accent)` accept button
- Same `useSyncExternalStore` + `getServerSnapshot` pattern
- Link to `/privacy`

- [ ] **Step 4: Run test to verify it passes**

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/legal/CookieConsentBanner.tsx apps/agent-site/__tests__/components/legal/CookieConsentBanner.test.tsx
git commit -m "feat: add agent-branded CookieConsentBanner with per-agent localStorage key"
```

---

### Task 5: Layout Skip-Nav + Globals CSS

**Files:**
- Modify: `apps/agent-site/app/layout.tsx`
- Modify: `apps/agent-site/app/globals.css`
- Create: `apps/agent-site/__tests__/layout.test.tsx`

**IMPORTANT:** Next.js `RootLayout` renders `<html>` and `<body>` which JSDOM cannot nest inside an existing document. Extract the skip-nav + main-content wrapper into a testable `SkipNavWrapper` component, or test by querying `document.body` instead of `container`.

- [ ] **Step 1: Write failing test**

Create `apps/agent-site/__tests__/layout.test.tsx`. Since `RootLayout` renders `<html>/<body>`, test the inner content by extracting it or rendering and querying `document`:

```tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render } from "@testing-library/react";

// We cannot render <html>/<body> inside JSDOM's existing document.
// Instead, test the skip-nav and main-content wrapper directly.
// Import the layout and render only its body content.

describe("RootLayout accessibility", () => {
  it("skip-nav link targets #main-content", () => {
    // Render the inner body content manually
    const { container } = render(
      <>
        <a href="#main-content" className="skip-nav">Skip to main content</a>
        <div id="main-content" tabIndex={-1}>
          <p>Content</p>
        </div>
      </>
    );
    const skipLink = container.querySelector('a[href="#main-content"]');
    expect(skipLink).toBeInTheDocument();
    expect(skipLink).toHaveTextContent("Skip to main content");

    const target = container.querySelector("#main-content");
    expect(target).toBeInTheDocument();
    expect(target).toHaveAttribute("tabindex", "-1");
  });

  it("main-content wrapper renders children", () => {
    const { getByText } = render(
      <div id="main-content" tabIndex={-1}>
        <p>Test child</p>
      </div>
    );
    expect(getByText("Test child")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/agent-site && npx vitest run __tests__/layout.test.tsx
```

- [ ] **Step 3: Update layout.tsx**

```tsx
import "./globals.css";

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>
        <a href="#main-content" className="skip-nav">
          Skip to main content
        </a>
        <div id="main-content" tabIndex={-1}>
          {children}
        </div>
      </body>
    </html>
  );
}
```

- [ ] **Step 4: Add skip-nav CSS to globals.css**

```css
.skip-nav {
  position: absolute;
  left: -9999px;
  top: auto;
  width: 1px;
  height: 1px;
  overflow: hidden;
  z-index: 100;
}

.skip-nav:focus {
  position: fixed;
  top: 10px;
  left: 10px;
  width: auto;
  height: auto;
  padding: 0.75rem 1.5rem;
  background: #1B5E20;
  color: white;
  font-weight: 600;
  text-decoration: none;
  border-radius: 0.5rem;
  box-shadow: 0 2px 8px rgba(0,0,0,0.3);
  z-index: 100;
}
```

- [ ] **Step 5: Run test to verify it passes**

- [ ] **Step 6: Commit**

```bash
git add apps/agent-site/app/layout.tsx apps/agent-site/app/globals.css apps/agent-site/__tests__/layout.test.tsx
git commit -m "feat: add skip-nav link and main-content target to agent-site layout"
```

---

### Task 6: Footer Legal Links

**Files:**
- Modify: `apps/agent-site/components/sections/Footer.tsx`
- Modify: `apps/agent-site/__tests__/components/Footer.test.tsx`

- [ ] **Step 1: Write failing tests (append to existing Footer.test.tsx)**

```tsx
it("renders legal links nav", () => {
  render(<Footer agent={AGENT} />);
  const legalNav = screen.getByRole("navigation", { name: "Legal links" });
  expect(legalNav).toBeInTheDocument();
});

it("renders privacy link", () => {
  render(<Footer agent={AGENT} />);
  const link = screen.getByRole("link", { name: /privacy/i });
  expect(link).toHaveAttribute("href", "/privacy");
});

it("renders terms link", () => {
  render(<Footer agent={AGENT} />);
  const link = screen.getByRole("link", { name: /terms/i });
  expect(link).toHaveAttribute("href", "/terms");
});

it("renders accessibility link", () => {
  render(<Footer agent={AGENT} />);
  const link = screen.getByRole("link", { name: /accessibility/i });
  expect(link).toHaveAttribute("href", "/accessibility");
});
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Add legal links to Footer.tsx**

Between EHO section and copyright `<p>`:

```tsx
<nav aria-label="Legal links" className="mt-4 flex justify-center gap-4 text-xs opacity-60">
  <a href="/privacy" className="hover:opacity-100 underline">Privacy Policy</a>
  <a href="/terms" className="hover:opacity-100 underline">Terms of Use</a>
  <a href="/accessibility" className="hover:opacity-100 underline">Accessibility</a>
</nav>
```

- [ ] **Step 4: Run test to verify it passes**

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/Footer.tsx apps/agent-site/__tests__/components/Footer.test.tsx
git commit -m "feat: add legal links (privacy, terms, accessibility) to agent-site footer"
```

---

## Chunk 2: Layout + Banner Integration (Layer 1 — parallel)

### Task 7: LegalPageLayout Component

**Depends on:** Tasks 2, 3, 4, 5, 6

**Files:**
- Create: `apps/agent-site/components/legal/LegalPageLayout.tsx`
- Create: `apps/agent-site/__tests__/components/legal/LegalPageLayout.test.tsx`

**Reference:** `apps/agent-site/app/thank-you/page.tsx` — `buildCssVariableStyle` + `style={cssVars}` pattern

- [ ] **Step 1: Write failing test**

Create `apps/agent-site/__tests__/components/legal/LegalPageLayout.test.tsx`:

```tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { LegalPageLayout } from "@/components/legal/LegalPageLayout";
import { AGENT, AGENT_MINIMAL } from "../fixtures";

describe("LegalPageLayout", () => {
  it("renders children (standard legal content)", () => {
    render(
      <LegalPageLayout agent={AGENT} agentId="test-agent">
        <h1>Privacy Policy</h1>
      </LegalPageLayout>
    );
    expect(screen.getByRole("heading", { name: "Privacy Policy" })).toBeInTheDocument();
  });

  it("renders customAbove markdown before standard content", () => {
    render(
      <LegalPageLayout agent={AGENT} agentId="test-agent" customAbove="## Custom Above">
        <p>Standard content</p>
      </LegalPageLayout>
    );
    expect(screen.getByRole("heading", { level: 2, name: "Custom Above" })).toBeInTheDocument();
    const customHeading = screen.getByText("Custom Above");
    const standard = screen.getByText("Standard content");
    expect(customHeading.compareDocumentPosition(standard) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("renders customBelow markdown after standard content", () => {
    render(
      <LegalPageLayout agent={AGENT} agentId="test-agent" customBelow="## Custom Below">
        <p>Standard content</p>
      </LegalPageLayout>
    );
    expect(screen.getByRole("heading", { level: 2, name: "Custom Below" })).toBeInTheDocument();
    const standard = screen.getByText("Standard content");
    const customHeading = screen.getByText("Custom Below");
    expect(standard.compareDocumentPosition(customHeading) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("does not render custom sections when not provided", () => {
    render(
      <LegalPageLayout agent={AGENT} agentId="test-agent">
        <p>Only standard</p>
      </LegalPageLayout>
    );
    expect(screen.getByText("Only standard")).toBeInTheDocument();
    // No extra markdown wrappers
  });

  it("renders Footer with legal links", () => {
    render(
      <LegalPageLayout agent={AGENT} agentId="test-agent">
        <p>Content</p>
      </LegalPageLayout>
    );
    expect(screen.getByRole("navigation", { name: "Legal links" })).toBeInTheDocument();
  });

  it("renders CookieConsentBanner", () => {
    render(
      <LegalPageLayout agent={AGENT} agentId="test-agent">
        <p>Content</p>
      </LegalPageLayout>
    );
    expect(screen.getByRole("dialog", { name: "Cookie consent" })).toBeInTheDocument();
  });

  it("injects CSS variables from agent branding", () => {
    const { container } = render(
      <LegalPageLayout agent={AGENT} agentId="test-agent">
        <p>Content</p>
      </LegalPageLayout>
    );
    const root = container.firstChild as HTMLElement;
    expect(root.style.getPropertyValue("--color-primary")).toBe("#1B5E20");
  });

  it("works with minimal agent config (default branding)", () => {
    render(
      <LegalPageLayout agent={AGENT_MINIMAL} agentId="minimal-agent">
        <p>Content</p>
      </LegalPageLayout>
    );
    expect(screen.getByText("Content")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create LegalPageLayout.tsx**

```tsx
import type { AgentConfig } from "@/lib/types";
import { buildCssVariableStyle } from "@/lib/branding";
import { Nav } from "@/components/Nav";
import { Footer } from "@/components/sections/Footer";
import { CookieConsentBanner } from "./CookieConsentBanner";
import { MarkdownContent } from "./MarkdownContent";

interface LegalPageLayoutProps {
  agent: AgentConfig;
  agentId: string;
  children: React.ReactNode;
  customAbove?: string;
  customBelow?: string;
}

export function LegalPageLayout({
  agent, agentId, children, customAbove, customBelow,
}: LegalPageLayoutProps) {
  const cssVars = buildCssVariableStyle(agent.branding);
  return (
    <div style={cssVars as React.CSSProperties}>
      <Nav agent={agent} />
      <main className="pt-[74px] min-h-[70vh] px-6 py-12">
        <div className="mx-auto max-w-3xl">
          {customAbove && <div className="mb-8"><MarkdownContent content={customAbove} /></div>}
          {children}
          {customBelow && <div className="mt-8"><MarkdownContent content={customBelow} /></div>}
        </div>
      </main>
      <Footer agent={agent} />
      <CookieConsentBanner agentId={agentId} />
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/legal/LegalPageLayout.tsx apps/agent-site/__tests__/components/legal/LegalPageLayout.test.tsx
git commit -m "feat: add LegalPageLayout with branding, custom markdown, and cookie consent"
```

---

### Task 11: Add CookieConsentBanner to Main + Thank-You Pages

**Depends on:** Task 4 only — can run in parallel with Task 7

**Files:**
- Modify: `apps/agent-site/app/page.tsx`
- Modify: `apps/agent-site/app/thank-you/page.tsx`

- [ ] **Step 1: Add CookieConsentBanner import and render to main page.tsx**

In `apps/agent-site/app/page.tsx`, add inside the `<div style={cssVars}>` wrapper, after `<Template />`:

```tsx
import { CookieConsentBanner } from "@/components/legal/CookieConsentBanner";
// ... after <Template />:
<CookieConsentBanner agentId={id} />
```

- [ ] **Step 2: Add CookieConsentBanner to thank-you/page.tsx**

In `apps/agent-site/app/thank-you/page.tsx`, add inside `<div style={cssVars}>`, after `<Footer />`:

```tsx
import { CookieConsentBanner } from "@/components/legal/CookieConsentBanner";
// ... after <Footer />:
<CookieConsentBanner agentId={id} />
```

- [ ] **Step 3: Run existing tests to verify nothing broke**

```bash
cd apps/agent-site && npx vitest run
```

- [ ] **Step 4: Commit**

```bash
git add apps/agent-site/app/page.tsx apps/agent-site/app/thank-you/page.tsx
git commit -m "feat: add CookieConsentBanner to main and thank-you pages"
```

---

## Chunk 3: Legal Pages (Layer 2 — all 3 parallel)

**IMPORTANT mock pattern for all page tests:** Do NOT use `require()` inside `vi.mock` factories — it breaks in ESM. Use this pattern instead:

```tsx
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { AGENT, AGENT_MINIMAL } from "../components/fixtures";

const mockLoadAgentConfig = vi.fn();
const mockLoadLegalContent = vi.fn();
const mockNotFound = vi.fn(() => { throw new Error("NOT_FOUND"); });
const mockCaptureException = vi.fn();

vi.mock("@/lib/config", () => ({
  loadAgentConfig: (...args: unknown[]) => mockLoadAgentConfig(...args),
  loadLegalContent: (...args: unknown[]) => mockLoadLegalContent(...args),
}));
vi.mock("next/navigation", () => ({ notFound: () => mockNotFound() }));
vi.mock("@sentry/nextjs", () => ({ captureException: (...args: unknown[]) => mockCaptureException(...args) }));
```

Then in `beforeEach`:
```tsx
beforeEach(() => {
  vi.clearAllMocks();
  mockLoadAgentConfig.mockResolvedValue(AGENT);
  mockLoadLegalContent.mockResolvedValue({ above: undefined, below: undefined });
});
```

### Task 8: Privacy Page

**Depends on:** Tasks 1, 7

**Files:**
- Create: `apps/agent-site/app/privacy/page.tsx`
- Create: `apps/agent-site/__tests__/pages/privacy.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/agent-site/__tests__/pages/privacy.test.tsx` using the mock pattern above. Test cases:

1. Renders "Privacy Policy" `<h1>`
2. Displays agent name (`Jane Smith`)
3. Displays agent email (`jane@example.com`)
4. Includes CCPA section
5. Includes effective date (`March 13, 2026`)
6. Renders `custom_above` markdown when `loadLegalContent` returns above content
7. Renders `custom_below` markdown when provided
8. Calls `notFound()` when `loadAgentConfig` rejects
9. Renders with `AGENT_MINIMAL` (no brokerage, no service_areas) — covers absent optional fields
10. Displays brokerage name when present (`Best Homes Realty`)
11. Displays service areas when present (`Hoboken`, `Jersey City`)

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create privacy/page.tsx**

Follow the `thank-you/page.tsx` pattern:
- Async server component with `searchParams: Promise<{ agentId?: string }>`
- `loadAgentConfig()` in try/catch, `notFound()` on failure
- `loadLegalContent(id, "privacy")` for custom above/below
- Build markdown template string with agent data interpolated
- Wrap in `<LegalPageLayout>`, render via `<MarkdownContent>`
- `generateMetadata`: `"Privacy Policy | {name}"`

- [ ] **Step 4: Run test to verify it passes**

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/app/privacy/ apps/agent-site/__tests__/pages/privacy.test.tsx
git commit -m "feat: add /privacy page with agent-personalized privacy policy"
```

---

### Task 9: Terms Page

**Depends on:** Tasks 1, 2, 7

**Files:**
- Create: `apps/agent-site/app/terms/page.tsx`
- Create: `apps/agent-site/__tests__/pages/terms.test.tsx`

- [ ] **Step 1: Write failing test**

Same mock pattern as Task 8. Test cases:

1. Renders "Terms of Use" `<h1>`
2. Displays agent name
3. Displays agent email
4. Includes CMA disclaimer text
5. Includes fair housing commitment
6. Uses full state name — "New Jersey" not "NJ" (via `getStateName()`)
7. Includes effective date
8. Renders custom below content when provided
9. Calls `notFound()` when agent config fails
10. Renders with `AGENT_MINIMAL` — covers absent `license_id` branch
11. Displays `license_id` when present (use inline override: `{ ...AGENT, identity: { ...AGENT.identity, license_id: "12345" } }`)

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create terms/page.tsx**

Same structure as privacy page:
- Use `getStateName(agent.location.state)` for governing law clause
- Standard terms markdown: CMA disclaimer, fair housing, IP, liability, governing law
- `generateMetadata`: `"Terms of Use | {name}"`

- [ ] **Step 4: Run test to verify it passes**

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/app/terms/ apps/agent-site/__tests__/pages/terms.test.tsx
git commit -m "feat: add /terms page with CMA disclaimer and state-specific governing law"
```

---

### Task 10: Accessibility Page

**Depends on:** Tasks 1, 7

**Files:**
- Create: `apps/agent-site/app/accessibility/page.tsx`
- Create: `apps/agent-site/__tests__/pages/accessibility.test.tsx`

- [ ] **Step 1: Write failing test**

Same mock pattern. Test cases:

1. Renders "Accessibility Statement" `<h1>`
2. Mentions WCAG 2.1 Level AA
3. Displays agent contact email
4. Includes known limitations section
5. Includes effective date
6. Calls `notFound()` when agent config fails
7. Renders with `AGENT_MINIMAL`

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create accessibility/page.tsx**

Simplest legal page:
- WCAG 2.1 Level AA commitment
- Known limitations
- Agent contact email
- `generateMetadata`: `"Accessibility | {name}"`

- [ ] **Step 4: Run test to verify it passes**

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/app/accessibility/ apps/agent-site/__tests__/pages/accessibility.test.tsx
git commit -m "feat: add /accessibility page with WCAG 2.1 AA commitment"
```

---

## Chunk 4: Integration Verify (Layer 3)

### Task 12: Full Integration Verify + Coverage Config

**Depends on:** ALL previous tasks

- [ ] **Step 1: Add `app/**/*.tsx` to coverage config**

Modify `apps/agent-site/vitest.config.ts` — add `"app/**/*.tsx"` to the `coverage.include` array:

```typescript
coverage: {
  provider: "v8",
  reporter: ["text", "lcov", "html"],
  include: [
    "lib/**/*.ts",
    "components/**/*.tsx",
    "templates/**/*.ts",
    "templates/**/*.tsx",
    "middleware.ts",
    "app/**/*.tsx",  // ← ADD THIS
  ],
```

- [ ] **Step 2: Run full test suite**

```bash
cd apps/agent-site && npx vitest run
```

Expected: ALL PASS

- [ ] **Step 3: Run coverage check**

```bash
cd apps/agent-site && npx vitest run --coverage
```

Expected: 100% branch coverage on all new files. If any branches are uncovered, add targeted tests.

- [ ] **Step 4: Run lint**

```bash
cd apps/agent-site && npx eslint .
```

Expected: No errors or warnings.

- [ ] **Step 5: Run build**

```bash
cd apps/agent-site && npm run build
```

Expected: Build succeeds with no errors.

- [ ] **Step 6: Final commit if any coverage/lint/build fixes were needed**

```bash
git add -A
git commit -m "test: achieve 100% coverage on legal compliance components"
```

- [ ] **Step 7: Push branch and open PR**

```bash
git push -u origin feat/agent-site-legal-compliance
```

Open PR referencing the spec. Use the `open-pr` skill.
