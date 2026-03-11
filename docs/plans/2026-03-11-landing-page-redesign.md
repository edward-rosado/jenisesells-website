# Landing Page Redesign Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the Real Estate Star landing page with content sections, AI search optimization, geometric star logo, hero-to-chat transition, and bug fixes.

**Architecture:** Server-rendered landing page (page.tsx) with a single client component for the form. GeometricStar SVG component shared between header and chat avatar. View Transitions API for hero-to-chat animation.

**Tech Stack:** Next.js 16, React 19, Tailwind CSS 4, Vitest, Testing Library

**Spec:** `docs/superpowers/specs/2026-03-11-landing-page-redesign-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `apps/platform/components/GeometricStar.tsx` | Create | SVG logo with spin/pulse animation states |
| `apps/platform/components/GetStartedForm.tsx` | Create | Client form component (profile URL input + CTA) |
| `apps/platform/app/page.tsx` | Rewrite | Server component with all sections + JSON-LD |
| `apps/platform/app/layout.tsx` | Modify | Rich metadata (OG, Twitter, keywords), logo in header |
| `apps/platform/app/globals.css` | Modify | Add keyframe animations for star spin/pulse, details/summary styles |
| `apps/platform/app/onboard/page.tsx` | Modify | Full-screen chat layout, view transition target |
| `apps/platform/components/chat/ChatWindow.tsx` | Modify | GeometricStar as AI avatar, fix double-type bug |
| `apps/platform/__tests__/components/GeometricStar.test.tsx` | Create | Tests for logo component |
| `apps/platform/__tests__/components/GetStartedForm.test.tsx` | Create | Tests for form component |
| `apps/platform/__tests__/landing.test.tsx` | Rewrite | Tests for new landing page sections |
| `apps/platform/__tests__/layout.test.tsx` | Modify | Test logo in header |
| `apps/platform/app/favicon.ico` | Replace | Static geometric star favicon |

---

## Chunk 1: GeometricStar Component

### Task 1: GeometricStar SVG Component

**Files:**
- Create: `apps/platform/components/GeometricStar.tsx`
- Create: `apps/platform/__tests__/components/GeometricStar.test.tsx`
- Modify: `apps/platform/app/globals.css`

- [ ] **Step 1: Add keyframe animations to globals.css**

Add to `apps/platform/app/globals.css`:

```css
@keyframes star-spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

@keyframes star-pulse {
  0%, 100% { transform: scale(1); }
  50% { transform: scale(1.05); }
}
```

- [ ] **Step 2: Write failing tests for GeometricStar**

Create `apps/platform/__tests__/components/GeometricStar.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { GeometricStar } from "@/components/GeometricStar";

describe("GeometricStar", () => {
  it("renders an SVG element", () => {
    const { container } = render(<GeometricStar size={24} />);
    expect(container.querySelector("svg")).toBeInTheDocument();
  });

  it("applies the specified size", () => {
    const { container } = render(<GeometricStar size={32} />);
    const svg = container.querySelector("svg")!;
    expect(svg.getAttribute("width")).toBe("32");
    expect(svg.getAttribute("height")).toBe("32");
  });

  it("renders two polygon elements (outer + inner star)", () => {
    const { container } = render(<GeometricStar size={24} />);
    const polygons = container.querySelectorAll("polygon");
    expect(polygons.length).toBe(2);
  });

  it("applies spin animation class when state is thinking", () => {
    const { container } = render(<GeometricStar size={24} state="thinking" />);
    const svg = container.querySelector("svg")!;
    expect(svg.style.animation).toContain("star-spin");
  });

  it("applies pulse animation class when state is idle", () => {
    const { container } = render(<GeometricStar size={24} state="idle" />);
    const svg = container.querySelector("svg")!;
    expect(svg.style.animation).toContain("star-pulse");
  });

  it("has no animation when state is not provided", () => {
    const { container } = render(<GeometricStar size={24} />);
    const svg = container.querySelector("svg")!;
    expect(svg.style.animation).toBe("");
  });
});
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd apps/platform && npx vitest run __tests__/components/GeometricStar.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 4: Implement GeometricStar component**

Create `apps/platform/components/GeometricStar.tsx`:

```tsx
interface GeometricStarProps {
  size: number;
  state?: "idle" | "thinking";
}

const ANIMATION: Record<string, string> = {
  thinking: "star-spin 8s linear infinite",
  idle: "star-pulse 2s ease-in-out infinite",
};

export function GeometricStar({ size, state }: GeometricStarProps) {
  const animation = state ? ANIMATION[state] : "";

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 100 100"
      style={{ animation }}
      aria-hidden="true"
    >
      <polygon
        points="50,8 61,36 92,36 67,55 76,84 50,67 24,84 33,55 8,36 39,36"
        fill="none"
        stroke="#10b981"
        strokeWidth="1.5"
      />
      <polygon
        points="50,22 57,40 76,40 61,51 67,70 50,59 33,70 39,51 24,40 43,40"
        fill="#10b981"
      />
    </svg>
  );
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/platform && npx vitest run __tests__/components/GeometricStar.test.tsx`
Expected: All 6 tests PASS

- [ ] **Step 6: Commit**

```bash
git add apps/platform/components/GeometricStar.tsx apps/platform/__tests__/components/GeometricStar.test.tsx apps/platform/app/globals.css
git commit -m "feat: add GeometricStar SVG logo component with spin/pulse states"
```

---

## Chunk 2: GetStartedForm Component

### Task 2: Extract GetStartedForm Client Component

**Files:**
- Create: `apps/platform/components/GetStartedForm.tsx`
- Create: `apps/platform/__tests__/components/GetStartedForm.test.tsx`

- [ ] **Step 1: Write failing tests for GetStartedForm**

Create `apps/platform/__tests__/components/GetStartedForm.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
import GetStartedForm from "@/components/GetStartedForm";

const mockPush = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush }),
}));

describe("GetStartedForm", () => {
  beforeEach(() => {
    mockPush.mockClear();
  });

  it("renders a URL input and submit button", () => {
    render(<GetStartedForm />);
    expect(
      screen.getByPlaceholderText(/paste your zillow or realtor\.com/i)
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /get started free/i })
    ).toBeInTheDocument();
  });

  it("navigates to /onboard with profileUrl when submitted with a URL", async () => {
    const user = userEvent.setup();
    render(<GetStartedForm />);
    const input = screen.getByPlaceholderText(/paste your zillow/i);
    await user.type(input, "https://zillow.com/profile/test");
    await user.click(screen.getByRole("button", { name: /get started free/i }));
    expect(mockPush).toHaveBeenCalledWith(
      "/onboard?profileUrl=https%3A%2F%2Fzillow.com%2Fprofile%2Ftest"
    );
  });

  it("navigates to /onboard without query params when submitted empty", async () => {
    const user = userEvent.setup();
    render(<GetStartedForm />);
    await user.click(screen.getByRole("button", { name: /get started free/i }));
    expect(mockPush).toHaveBeenCalledWith("/onboard");
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/platform && npx vitest run __tests__/components/GetStartedForm.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement GetStartedForm component**

Create `apps/platform/components/GetStartedForm.tsx`:

```tsx
"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

export default function GetStartedForm() {
  const [profileUrl, setProfileUrl] = useState("");
  const router = useRouter();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const params = profileUrl
      ? `?profileUrl=${encodeURIComponent(profileUrl)}`
      : "";

    const navigate = () => router.push(`/onboard${params}`);

    if (document.startViewTransition) {
      document.startViewTransition(navigate);
    } else {
      navigate();
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <input
        type="text"
        value={profileUrl}
        onChange={(e) => setProfileUrl(e.target.value)}
        placeholder="Paste your Zillow or Realtor.com URL"
        className="w-full px-4 py-3 rounded-lg bg-gray-800 border border-gray-700 text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-emerald-500"
      />
      <button
        type="submit"
        className="w-full px-6 py-3 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-lg transition-colors"
      >
        Get Started Free
      </button>
    </form>
  );
}
```

Note: `document.startViewTransition` does not exist in jsdom. Tests pass because the else branch runs. View Transition is progressive enhancement.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/platform && npx vitest run __tests__/components/GetStartedForm.test.tsx`
Expected: All 3 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/platform/components/GetStartedForm.tsx apps/platform/__tests__/components/GetStartedForm.test.tsx
git commit -m "feat: extract GetStartedForm client component with View Transition support"
```

---

## Chunk 3: Landing Page Rewrite

### Task 3: Rewrite page.tsx as Server Component

**Files:**
- Rewrite: `apps/platform/app/page.tsx`
- Rewrite: `apps/platform/__tests__/landing.test.tsx`

- [ ] **Step 1: Write tests for new landing page sections**

Rewrite `apps/platform/__tests__/landing.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import LandingPage from "@/app/page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock("@/components/GetStartedForm", () => ({
  default: () => <div data-testid="get-started-form">MockForm</div>,
}));

describe("Landing Page", () => {
  it('displays the headline "Stop paying monthly."', () => {
    render(<LandingPage />);
    expect(
      screen.getByRole("heading", { level: 1, name: /stop paying monthly/i })
    ).toBeInTheDocument();
  });

  it('displays the price "$900. Everything."', () => {
    render(<LandingPage />);
    expect(screen.getByText(/\$900\. everything\./i)).toBeInTheDocument();
  });

  it("renders two GetStartedForm instances (hero + bottom CTA)", () => {
    render(<LandingPage />);
    const forms = screen.getAllByTestId("get-started-form");
    expect(forms.length).toBe(2);
  });

  it('renders the "What is Real Estate Star?" section', () => {
    render(<LandingPage />);
    expect(
      screen.getByRole("heading", { name: /what is real estate star/i })
    ).toBeInTheDocument();
  });

  it("renders all 8 feature cards", () => {
    render(<LandingPage />);
    expect(screen.getByText("Professional Website")).toBeInTheDocument();
    expect(screen.getByText("CMA Automation")).toBeInTheDocument();
    expect(screen.getByText("Lead Capture")).toBeInTheDocument();
    expect(screen.getByText("Auto-Replies")).toBeInTheDocument();
    expect(screen.getByText("DocuSign Integration")).toBeInTheDocument();
    expect(screen.getByText("Contract Drafting")).toBeInTheDocument();
    expect(screen.getByText("Scheduling")).toBeInTheDocument();
    expect(screen.getByText("Email Automation")).toBeInTheDocument();
  });

  it('renders "Coming Soon" badges for planned features', () => {
    render(<LandingPage />);
    const badges = screen.getAllByText("Coming Soon");
    expect(badges.length).toBe(3);
  });

  it("renders the comparison table with competitor names", () => {
    render(<LandingPage />);
    expect(screen.getByText("KVCore")).toBeInTheDocument();
    expect(screen.getByText("BoomTown")).toBeInTheDocument();
    expect(screen.getByText("Sierra")).toBeInTheDocument();
  });

  it("renders the 2-year cost row", () => {
    render(<LandingPage />);
    expect(screen.getByText("$900")).toBeInTheDocument();
    expect(screen.getByText("$12,000+")).toBeInTheDocument();
    expect(screen.getByText("$24,000+")).toBeInTheDocument();
    expect(screen.getByText("$9,600+")).toBeInTheDocument();
  });

  it("renders 5 FAQ items as collapsible details elements", () => {
    const { container } = render(<LandingPage />);
    const details = container.querySelectorAll("details");
    expect(details.length).toBe(5);
  });

  it("renders FAQ questions in summary elements", () => {
    render(<LandingPage />);
    expect(screen.getByText("Who is Real Estate Star for?")).toBeInTheDocument();
    expect(screen.getByText("How much does it cost?")).toBeInTheDocument();
    expect(screen.getByText("Do I need technical skills?")).toBeInTheDocument();
  });

  it("renders JSON-LD structured data script tag", () => {
    const { container } = render(<LandingPage />);
    const script = container.querySelector('script[type="application/ld+json"]');
    expect(script).toBeInTheDocument();
    const data = JSON.parse(script!.textContent!);
    expect(data["@context"]).toBe("https://schema.org");
    expect(data["@graph"]).toHaveLength(3);
  });

  it("displays trial disclaimer", () => {
    render(<LandingPage />);
    expect(
      screen.getByText(/7-day free trial\. no credit card required\./i)
    ).toBeInTheDocument();
  });

  it("renders the comparison footnote", () => {
    render(<LandingPage />);
    expect(
      screen.getByText(/built for agents who need a website with automation/i)
    ).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/platform && npx vitest run __tests__/landing.test.tsx`
Expected: FAIL — page still has old structure

- [ ] **Step 3: Rewrite page.tsx as server component**

Rewrite `apps/platform/app/page.tsx` with:
- Remove `"use client"` directive
- Import `GetStartedForm` from `@/components/GetStartedForm`
- Define `jsonLd` constant with `SoftwareApplication`, `Organization`, and `FAQPage` schemas (see spec for full content)
- Define `features` array (8 items, 3 with `comingSoon: true`)
- Define `comparisonRows` array with feature names and per-platform check/dash/soon values
- Define `faqItems` array with 5 question/answer pairs
- Render sections in order: Hero, What is, Features, Comparison, FAQ, Bottom CTA
- Use `<script type="application/ld+json">` for JSON-LD (static source-code data, not user input — safe)
- Use `<section>` for each section, `<article>` for feature cards
- Use `<details>`/`<summary>` for FAQ items
- Use `<table>` for comparison with semantic `<thead>`/`<tbody>`
- Two `<GetStartedForm />` instances — one in hero, one in bottom CTA

Key content from spec:
- Hero subtitle: "A professional website, CMA automation, lead management, contract drafting, and auto-replies — one payment, no subscriptions."
- Trial note: "7-day free trial. No credit card required."
- Comparison footnote: "Real Estate Star is built for agents who need a website with automation — not enterprise teams managing 50 agents. If you need IDX search or team management, those platforms exist for a reason."

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/platform && npx vitest run __tests__/landing.test.tsx`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/platform/app/page.tsx apps/platform/__tests__/landing.test.tsx
git commit -m "feat: rewrite landing page as server component with features, comparison, FAQ sections"
```

---

## Chunk 4: Layout Metadata and Logo

### Task 4: Update layout.tsx with Rich Metadata and Logo

**Files:**
- Modify: `apps/platform/app/layout.tsx`
- Modify: `apps/platform/__tests__/layout.test.tsx`

- [ ] **Step 1: Update layout tests**

Add to `apps/platform/__tests__/layout.test.tsx`:

```tsx
it("renders the GeometricStar logo in the header", () => {
  const { container } = render(
    <RootLayout><div>child</div></RootLayout>
  );
  const svg = container.querySelector("header svg");
  expect(svg).toBeInTheDocument();
});
```

- [ ] **Step 2: Run tests to verify new test fails**

Run: `cd apps/platform && npx vitest run __tests__/layout.test.tsx`
Expected: New test FAILS (no SVG in header yet)

- [ ] **Step 3: Update layout.tsx**

Modify `apps/platform/app/layout.tsx`:
- Update `Metadata` export with:
  - title: "Real Estate Star — Website, CMA & Lead Tools for Real Estate Agents"
  - description: "All-in-one platform for real estate agents. Professional website, CMA automation, lead management, contract drafting, and auto-replies — one-time $900 fee, no monthly subscriptions."
  - openGraph: title, description, type: "website", url: "https://realestatestar.com", siteName: "Real Estate Star"
  - twitter: card: "summary_large_image", title, description
  - keywords: ["real estate agent website", "CMA automation", "real estate lead management", "agent website builder", "real estate tools", "real estate automation"]
- Import `GeometricStar` from `@/components/GeometricStar`
- Replace the `<span aria-hidden="true">★ </span>` with `<GeometricStar size={24} state="idle" />`

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/platform && npx vitest run __tests__/layout.test.tsx`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/platform/app/layout.tsx apps/platform/__tests__/layout.test.tsx
git commit -m "feat: add rich metadata (OG, Twitter, keywords) and GeometricStar logo to layout"
```

---

## Chunk 5: Onboard Full-Screen Chat and View Transition

### Task 5: Full-Screen Chat Layout on /onboard

**Files:**
- Modify: `apps/platform/app/onboard/page.tsx`
- Modify: `apps/platform/app/globals.css`

- [ ] **Step 1: Add view transition CSS to globals.css**

Add to `apps/platform/app/globals.css`:

```css
@view-transition {
  navigation: auto;
}

::view-transition-old(chat-transition),
::view-transition-new(chat-transition) {
  animation-duration: 0.3s;
}
```

- [ ] **Step 2: Update onboard page for full-screen chat**

**Approach:** Use a `data-onboard` attribute on the onboard page wrapper + CSS `:has()` selector to hide the header. This keeps the root layout intact (html/body tags, metadata) while removing the header visually.

Modify `apps/platform/app/onboard/page.tsx`:
- Add `data-onboard` attribute to the outermost `<main>` wrapper in `OnboardContent` (and all fallback `<main>` elements)
- Add `style={{ viewTransitionName: "chat-transition" }}` to the same wrapper

Add to `apps/platform/app/globals.css`:

```css
body:has([data-onboard]) header { display: none; }
```

Add `view-transition-name` to the hero form wrapper in `apps/platform/components/GetStartedForm.tsx` so the morph animation connects both sides:

```tsx
<form onSubmit={handleSubmit} className="space-y-4" style={{ viewTransitionName: "chat-transition" }}>
```

- [ ] **Step 3: Verify the build succeeds**

Run: `cd apps/platform && npx next build`
Expected: Build succeeds with no errors

- [ ] **Step 4: Commit**

```bash
git add apps/platform/app/onboard/page.tsx apps/platform/app/globals.css
git commit -m "feat: full-screen chat layout on /onboard with view transition CSS"
```

---

## Chunk 6: Chat Avatar and Double-Type Bug Fix

### Task 6: Add GeometricStar Avatar to ChatWindow

**Files:**
- Modify: `apps/platform/components/chat/ChatWindow.tsx`
- Modify: `apps/platform/__tests__/components/ChatWindow.test.tsx` (create if not exists)

- [ ] **Step 0: Write failing test for double-type bug**

Create or update `apps/platform/__tests__/components/ChatWindow.test.tsx` with a test that verifies the bug:

```tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { ChatWindow } from "@/components/chat/ChatWindow";

// Mock fetch to return a streaming response
const mockFetch = vi.fn();
global.fetch = mockFetch;

vi.mock("@/components/chat/MessageRenderer", () => ({
  MessageRenderer: ({ message }: { message: { content: string } }) => (
    <div data-testid="message">{message.content}</div>
  ),
}));

describe("ChatWindow", () => {
  beforeEach(() => {
    mockFetch.mockReset();
  });

  it("does not block second send after first completes", async () => {
    // Mock a simple JSON response (not streaming)
    mockFetch.mockResolvedValue({
      ok: true,
      headers: new Headers({ "content-type": "application/json" }),
      json: () => Promise.resolve({ response: "Hello!" }),
    });

    const user = userEvent.setup();
    render(<ChatWindow sessionId="test" token="tok" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/type a message/i);
    const button = screen.getByRole("button", { name: /send/i });

    // First message
    await user.type(input, "hello");
    await user.click(button);

    // Wait for response to complete
    await screen.findByText("Hello!");

    // Second message should NOT be blocked
    await user.type(input, "world");
    await user.click(button);

    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it("renders GeometricStar avatar for assistant messages", async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      headers: new Headers({ "content-type": "application/json" }),
      json: () => Promise.resolve({ response: "Hi there!" }),
    });

    const user = userEvent.setup();
    const { container } = render(
      <ChatWindow sessionId="test" token="tok" initialMessages={[]} />
    );

    await user.type(screen.getByPlaceholderText(/type a message/i), "hi");
    await user.click(screen.getByRole("button", { name: /send/i }));

    await screen.findByText("Hi there!");

    // GeometricStar renders an SVG with aria-hidden
    const avatarSvgs = container.querySelectorAll("svg[aria-hidden]");
    expect(avatarSvgs.length).toBeGreaterThan(0);
  });
});
```

- [ ] **Step 1: Investigate the double-type bug**

Read `ChatWindow.tsx` lines 88-191 carefully. The `sending` guard is at line 99. When a streaming response completes:
1. Lines 169-176: `setMessages` replaces the streaming placeholder with parsed content
2. Line 185: `setSending(false)` in the `finally` block

Both are synchronous React state updates. React 19 batches these. But the issue may be that after stream completion, the input appears enabled (cursor returns) before `setSending(false)` has committed — meaning the next keypress + Enter fires `handleSend` which hits `if (!text || sending) return` because `sending` is still `true`.

Alternative: the issue could be that the `onSubmit` handler fires but the `input` value is empty string because the previous send already called `setInput("")`. The user types, presses Enter, but the state update from typing hasn't committed yet.

Test both hypotheses. The fix is to move the `sending` check into the `handleSend` function and ensure `input` is read directly, or use a ref for the `sending` flag.

- [ ] **Step 2: Fix the double-type bug**

Apply the fix based on root cause. If it's a `sending` timing issue, use `useRef` for the sending flag:

```tsx
const sendingRef = useRef(false);

async function sendMessage(text: string) {
  if (!text || sendingRef.current) return;
  sendingRef.current = true;
  setSending(true); // keep for UI disabled state
  // ... rest of function
  // in finally:
  sendingRef.current = false;
  setSending(false);
}
```

- [ ] **Step 3: Add GeometricStar as AI avatar**

In `ChatWindow.tsx`:

Import: `import { GeometricStar } from "../GeometricStar";`

Update the message rendering (around line 200):

```tsx
{messages.map((msg, i) => (
  <div key={i} className={msg.role === "assistant" ? "flex items-start gap-3" : ""}>
    {msg.role === "assistant" && (
      <div className="shrink-0 mt-1">
        <GeometricStar
          size={32}
          state={sending && i === messages.length - 1 ? "thinking" : "idle"}
        />
      </div>
    )}
    <MessageRenderer message={msg} onAction={handleAction} />
  </div>
))}
```

Update the "Thinking..." placeholder (around line 204):

```tsx
{sending && messages[messages.length - 1]?.role !== "assistant" && (
  <div className="flex items-start gap-3">
    <div className="shrink-0 mt-1">
      <GeometricStar size={32} state="thinking" />
    </div>
    <div className="bg-gray-800 rounded-2xl px-4 py-2 text-gray-400">
      <span className="animate-pulse">Thinking...</span>
    </div>
  </div>
)}
```

- [ ] **Step 4: Build and verify**

Run: `cd apps/platform && npx next build`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add apps/platform/components/chat/ChatWindow.tsx
git commit -m "feat: add GeometricStar avatar to chat, fix double-type bug"
```

---

## Chunk 7: Favicon

### Task 7: Replace Favicon with Geometric Star

**Files:**
- Replace: `apps/platform/app/favicon.ico`

- [ ] **Step 1: Generate a static geometric star favicon**

Create a simple SVG of the geometric star, then convert to .ico format. Use the same nested-star shape from `GeometricStar.tsx` with emerald fill on transparent background. Save as `apps/platform/app/favicon.ico` (16x16 and 32x32 sizes).

Alternatively, create `apps/platform/app/icon.svg` (Next.js 16 supports SVG favicons natively):

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
  <polygon points="50,8 61,36 92,36 67,55 76,84 50,67 24,84 33,55 8,36 39,36" fill="none" stroke="#10b981" stroke-width="3"/>
  <polygon points="50,22 57,40 76,40 61,51 67,70 50,59 33,70 39,51 24,40 43,40" fill="#10b981"/>
</svg>
```

- [ ] **Step 2: Remove old favicon.ico if using SVG approach**

If using `icon.svg`, delete the old `favicon.ico` to avoid conflicts. Next.js prioritizes `icon.svg` over `favicon.ico`.

- [ ] **Step 3: Commit**

```bash
git add apps/platform/app/icon.svg
git rm apps/platform/app/favicon.ico 2>/dev/null || true
git commit -m "feat: replace favicon with geometric star SVG"
```

---

## Chunk 8: Final Verification

### Task 8: Full Build and Test Suite

- [ ] **Step 1: Run full test suite**

Run: `cd apps/platform && npx vitest run`
Expected: All tests pass

- [ ] **Step 2: Run build**

Run: `cd apps/platform && npx next build`
Expected: Build succeeds, all routes compile

- [ ] **Step 3: Run coverage check**

Run: `cd apps/platform && npx vitest run --coverage`
Expected: Coverage meets thresholds (100% branches/functions/lines/statements for changed files)

- [ ] **Step 4: Visual check**

Run: `cd apps/platform && npx next dev`
Open http://localhost:3000 and verify:
- Header has spinning geometric star logo
- Hero section renders with form
- Scroll down: "What is Real Estate Star?" section visible
- Features grid: 8 cards, 3 with "Coming Soon" badge
- Comparison table: 4 platforms, cost row at bottom, footnote
- FAQ: 5 collapsible items using native details/summary
- Bottom CTA: working form that navigates to /onboard
- Click "Get Started Free": smooth view transition to full-screen chat
- Chat: geometric star avatar (pulsing at idle, spinning when AI responds)
- Double-type bug: verify single Enter submits the message

- [ ] **Step 5: Final commit if any fixes needed**

Stage only the specific files that were fixed, then commit:

```bash
git add <specific-files-that-changed>
git commit -m "fix: address visual/test issues from final verification"
```
