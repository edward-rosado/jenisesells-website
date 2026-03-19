# Premium Visual Components Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 5 premium section variants, 3 shared animation hooks, and scroll-reveal retrofitting to all 10 agent site templates.

**Architecture:** New hooks (`useReducedMotion`, `useScrollReveal`, `useParallax`) in `apps/agent-site/hooks/` provide shared animation primitives. Five new section components join the existing variant pool. Three templates (LightLuxury, LuxuryEstate, Commercial) swap to premium variants. All 10 templates get scroll-reveal wrappers.

**Tech Stack:** React 19, Next.js 16, `useSyncExternalStore`, `IntersectionObserver`, `requestAnimationFrame`, inline styles, Vitest + React Testing Library.

---

## File Structure

### New files

| File | Responsibility |
|------|---------------|
| `apps/agent-site/hooks/useReducedMotion.ts` | `prefers-reduced-motion` via `useSyncExternalStore` |
| `apps/agent-site/hooks/useScrollReveal.ts` | IntersectionObserver scroll-triggered visibility |
| `apps/agent-site/hooks/useParallax.ts` | rAF-throttled parallax scroll transform |
| `apps/agent-site/hooks/index.ts` | Barrel export |
| `apps/agent-site/__tests__/hooks/useReducedMotion.test.ts` | Hook tests |
| `apps/agent-site/__tests__/hooks/useScrollReveal.test.ts` | Hook tests |
| `apps/agent-site/__tests__/hooks/useParallax.test.ts` | Hook tests |
| `apps/agent-site/components/sections/marquee/MarqueeBanner.tsx` | Continuous-scroll logo/award banner |
| `apps/agent-site/components/sections/marquee/index.ts` | Barrel export |
| `apps/agent-site/__tests__/components/marquee/MarqueeBanner.test.tsx` | Component tests |
| `apps/agent-site/components/sections/testimonials/TestimonialsSpotlight.tsx` | Auto-rotating single-review spotlight |
| `apps/agent-site/__tests__/components/testimonials/TestimonialsSpotlight.test.tsx` | Component tests |
| `apps/agent-site/components/sections/services/ServicesPremium.tsx` | Full-bleed scroll-revealed feature blocks |
| `apps/agent-site/__tests__/components/services/ServicesPremium.test.tsx` | Component tests |
| `apps/agent-site/components/sections/heroes/HeroParallax.tsx` | Parallax zoom hero |
| `apps/agent-site/__tests__/components/heroes/HeroParallax.test.tsx` | Component tests |
| `apps/agent-site/components/sections/about/AboutParallax.tsx` | Parallax about section |
| `apps/agent-site/__tests__/components/about/AboutParallax.test.tsx` | Component tests |

### Modified files

| File | Change |
|------|--------|
| `apps/agent-site/lib/types.ts` | Add `MarqueeData`, `MarqueeItem`, `marquee` to `PageSections`, optional fields on `FeatureItem` and `HeroData` |
| `apps/agent-site/components/sections/types.ts` | Add `MarqueeProps` |
| `apps/agent-site/components/sections/index.ts` | Export 5 new components |
| `apps/agent-site/templates/light-luxury.tsx` | Swap to premium variants + scroll-reveal wrappers |
| `apps/agent-site/templates/luxury-estate.tsx` | Swap to premium variants + scroll-reveal wrappers |
| `apps/agent-site/templates/commercial.tsx` | Swap to premium variants + scroll-reveal wrappers |
| `apps/agent-site/templates/emerald-classic.tsx` | Add scroll-reveal wrappers |
| `apps/agent-site/templates/modern-minimal.tsx` | Add scroll-reveal wrappers |
| `apps/agent-site/templates/warm-community.tsx` | Add scroll-reveal wrappers |
| `apps/agent-site/templates/urban-loft.tsx` | Add scroll-reveal wrappers |
| `apps/agent-site/templates/new-beginnings.tsx` | Add scroll-reveal wrappers |
| `apps/agent-site/templates/country-estate.tsx` | Add scroll-reveal wrappers |
| `apps/agent-site/templates/coastal-living.tsx` | Add scroll-reveal wrappers |
| `apps/agent-site/__tests__/components/fixtures.ts` | Add marquee data to CONTENT fixture |
| `apps/agent-site/__tests__/templates/light-luxury.test.tsx` | Update for premium variants |
| `apps/agent-site/__tests__/templates/luxury-estate.test.tsx` | Update for premium variants |
| `apps/agent-site/__tests__/templates/commercial.test.tsx` | Update for premium variants |

---

## Chunk 1: Foundation Hooks

### Task 1: useReducedMotion Hook

**Files:**
- Create: `apps/agent-site/hooks/useReducedMotion.ts`
- Create: `apps/agent-site/__tests__/hooks/useReducedMotion.test.ts`

- [ ] **Step 1: Write the failing test**

Create `apps/agent-site/__tests__/hooks/useReducedMotion.test.ts`:

```typescript
// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useReducedMotion } from "@/hooks/useReducedMotion";

describe("useReducedMotion", () => {
  let listeners: Array<(e: { matches: boolean }) => void>;
  let currentMatches: boolean;

  beforeEach(() => {
    listeners = [];
    currentMatches = false;
    vi.stubGlobal("matchMedia", vi.fn((query: string) => ({
      matches: currentMatches,
      media: query,
      addEventListener: (_: string, cb: (e: { matches: boolean }) => void) => { listeners.push(cb); },
      removeEventListener: (_: string, cb: (e: { matches: boolean }) => void) => {
        listeners = listeners.filter((l) => l !== cb);
      },
    })));
  });

  afterEach(() => { vi.restoreAllMocks(); });

  it("returns false when motion is not reduced", () => {
    const { result } = renderHook(() => useReducedMotion());
    expect(result.current).toBe(false);
  });

  it("returns true when motion is reduced", () => {
    currentMatches = true;
    const { result } = renderHook(() => useReducedMotion());
    expect(result.current).toBe(true);
  });

  it("updates when preference changes", () => {
    const { result } = renderHook(() => useReducedMotion());
    expect(result.current).toBe(false);
    currentMatches = true;
    act(() => { listeners.forEach((cb) => cb({ matches: true })); });
    expect(result.current).toBe(true);
  });

  it("cleans up listener on unmount", () => {
    const { unmount } = renderHook(() => useReducedMotion());
    expect(listeners).toHaveLength(1);
    unmount();
    expect(listeners).toHaveLength(0);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/hooks/useReducedMotion.test.ts`
Expected: FAIL — module `@/hooks/useReducedMotion` not found

- [ ] **Step 3: Write minimal implementation**

Create `apps/agent-site/hooks/useReducedMotion.ts`:

```typescript
"use client";

import { useSyncExternalStore } from "react";

const QUERY = "(prefers-reduced-motion: reduce)";

function subscribe(callback: () => void): () => void {
  const mq = window.matchMedia(QUERY);
  mq.addEventListener("change", callback);
  return () => mq.removeEventListener("change", callback);
}

function getSnapshot(): boolean {
  return window.matchMedia(QUERY).matches;
}

function getServerSnapshot(): boolean {
  return false;
}

export function useReducedMotion(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/hooks/useReducedMotion.test.ts`
Expected: 4 passing

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/hooks/useReducedMotion.ts apps/agent-site/__tests__/hooks/useReducedMotion.test.ts
git commit -m "feat: add useReducedMotion hook with useSyncExternalStore"
```

---

### Task 2: useScrollReveal Hook

**Files:**
- Create: `apps/agent-site/hooks/useScrollReveal.ts`
- Create: `apps/agent-site/__tests__/hooks/useScrollReveal.test.ts`

- [ ] **Step 1: Write the failing test**

Create `apps/agent-site/__tests__/hooks/useScrollReveal.test.ts`:

```typescript
// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useScrollReveal } from "@/hooks/useScrollReveal";
import { useRef } from "react";

// Mock IntersectionObserver
let observerCallback: IntersectionObserverCallback;
let observerOptions: IntersectionObserverInit | undefined;
let disconnectSpy: ReturnType<typeof vi.fn>;

beforeEach(() => {
  disconnectSpy = vi.fn();
  vi.stubGlobal("IntersectionObserver", vi.fn((cb: IntersectionObserverCallback, opts?: IntersectionObserverInit) => {
    observerCallback = cb;
    observerOptions = opts;
    return { observe: vi.fn(), unobserve: vi.fn(), disconnect: disconnectSpy };
  }));
});

afterEach(() => { vi.restoreAllMocks(); });

// Helper: render hook with a real ref attached to a div
function renderScrollReveal(options?: Parameters<typeof useScrollReveal>[1]) {
  const div = document.createElement("div");
  return renderHook(() => {
    const ref = useRef<HTMLDivElement>(div);
    const isVisible = useScrollReveal(ref, options);
    return { isVisible, ref };
  });
}

describe("useScrollReveal", () => {
  it("returns false initially", () => {
    const { result } = renderScrollReveal();
    expect(result.current.isVisible).toBe(false);
  });

  it("returns true after element intersects", () => {
    const { result } = renderScrollReveal();
    act(() => {
      observerCallback(
        [{ isIntersecting: true }] as IntersectionObserverEntry[],
        {} as IntersectionObserver,
      );
    });
    expect(result.current.isVisible).toBe(true);
  });

  it("uses default threshold of 0.15", () => {
    renderScrollReveal();
    expect(observerOptions?.threshold).toBe(0.15);
  });

  it("accepts custom threshold", () => {
    renderScrollReveal({ threshold: 0.5 });
    expect(observerOptions?.threshold).toBe(0.5);
  });

  it("disconnects observer after first trigger when once=true (default)", () => {
    renderScrollReveal();
    act(() => {
      observerCallback(
        [{ isIntersecting: true }] as IntersectionObserverEntry[],
        {} as IntersectionObserver,
      );
    });
    expect(disconnectSpy).toHaveBeenCalled();
  });

  it("does not disconnect when once=false", () => {
    renderScrollReveal({ once: false });
    act(() => {
      observerCallback(
        [{ isIntersecting: true }] as IntersectionObserverEntry[],
        {} as IntersectionObserver,
      );
    });
    expect(disconnectSpy).not.toHaveBeenCalled();
  });

  it("disconnects on unmount", () => {
    const { unmount } = renderScrollReveal();
    unmount();
    expect(disconnectSpy).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/hooks/useScrollReveal.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Write minimal implementation**

Create `apps/agent-site/hooks/useScrollReveal.ts`:

```typescript
"use client";

import { useState, useEffect, type RefObject } from "react";
import { useReducedMotion } from "./useReducedMotion";

interface ScrollRevealOptions {
  threshold?: number;
  once?: boolean;
}

export function useScrollReveal(
  ref: RefObject<HTMLElement | null>,
  options?: ScrollRevealOptions,
): boolean {
  const { threshold = 0.15, once = true } = options ?? {};
  const reducedMotion = useReducedMotion();
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    if (reducedMotion) {
      setIsVisible(true);
      return;
    }

    const el = ref.current;
    if (!el) return;

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            setIsVisible(true);
            if (once) observer.disconnect();
          }
        }
      },
      { threshold },
    );

    observer.observe(el);
    return () => observer.disconnect();
  }, [ref, threshold, once, reducedMotion]);

  return isVisible;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/hooks/useScrollReveal.test.ts`
Expected: All passing

- [ ] **Step 5: Add reduced-motion test**

Add to the same test file:

```typescript
describe("useScrollReveal with reduced motion", () => {
  beforeEach(() => {
    vi.stubGlobal("matchMedia", vi.fn((query: string) => ({
      matches: query === "(prefers-reduced-motion: reduce)",
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })));
  });

  it("returns true immediately when reduced motion is enabled", () => {
    const { result } = renderScrollReveal();
    expect(result.current.isVisible).toBe(true);
  });
});
```

- [ ] **Step 6: Run all hook tests**

Run: `cd apps/agent-site && npx vitest run __tests__/hooks/`
Expected: All passing

- [ ] **Step 7: Commit**

```bash
git add apps/agent-site/hooks/useScrollReveal.ts apps/agent-site/__tests__/hooks/useScrollReveal.test.ts
git commit -m "feat: add useScrollReveal hook with IntersectionObserver"
```

---

### Task 3: useParallax Hook

**Files:**
- Create: `apps/agent-site/hooks/useParallax.ts`
- Create: `apps/agent-site/__tests__/hooks/useParallax.test.ts`

- [ ] **Step 1: Write the failing test**

Create `apps/agent-site/__tests__/hooks/useParallax.test.ts`:

```typescript
// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useParallax } from "@/hooks/useParallax";
import { useRef } from "react";

let rafCallback: FrameRequestCallback | null = null;

beforeEach(() => {
  rafCallback = null;
  vi.stubGlobal("requestAnimationFrame", vi.fn((cb: FrameRequestCallback) => { rafCallback = cb; return 1; }));
  vi.stubGlobal("cancelAnimationFrame", vi.fn());
  // Default: motion OK
  vi.stubGlobal("matchMedia", vi.fn((query: string) => ({
    matches: false,
    media: query,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
  })));
});

afterEach(() => { vi.restoreAllMocks(); });

function renderParallax(options?: Parameters<typeof useParallax>[2]) {
  const container = document.createElement("div");
  const bg = document.createElement("div");
  // Mock getBoundingClientRect
  container.getBoundingClientRect = () => ({
    top: 0, bottom: 600, left: 0, right: 800, width: 800, height: 600, x: 0, y: 0, toJSON: () => "",
  });
  Object.defineProperty(window, "innerHeight", { value: 800, writable: true });

  return renderHook(() => {
    const containerRef = useRef<HTMLDivElement>(container);
    const bgRef = useRef<HTMLDivElement>(bg);
    useParallax(containerRef, bgRef, options);
    return { container, bg };
  });
}

describe("useParallax", () => {
  it("attaches scroll listener on mount", () => {
    const addSpy = vi.spyOn(window, "addEventListener");
    renderParallax();
    expect(addSpy).toHaveBeenCalledWith("scroll", expect.any(Function), expect.anything());
  });

  it("removes scroll listener on unmount", () => {
    const removeSpy = vi.spyOn(window, "removeEventListener");
    const { unmount } = renderParallax();
    unmount();
    expect(removeSpy).toHaveBeenCalledWith("scroll", expect.any(Function), expect.anything());
  });

  it("applies transform to background element on scroll", () => {
    const { result } = renderParallax();
    // Simulate scroll
    window.dispatchEvent(new Event("scroll"));
    if (rafCallback) act(() => { rafCallback!(0); });
    // bg should have transform set
    expect(result.current.bg.style.transform).toBeTruthy();
  });

  it("does not attach listener when reduced motion is enabled", () => {
    vi.stubGlobal("matchMedia", vi.fn((query: string) => ({
      matches: query === "(prefers-reduced-motion: reduce)",
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })));
    const addSpy = vi.spyOn(window, "addEventListener");
    renderParallax();
    const scrollCalls = addSpy.mock.calls.filter(([event]) => event === "scroll");
    expect(scrollCalls).toHaveLength(0);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/hooks/useParallax.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Write minimal implementation**

Create `apps/agent-site/hooks/useParallax.ts`:

```typescript
"use client";

import { useEffect, type RefObject } from "react";
import { useReducedMotion } from "./useReducedMotion";

interface ParallaxOptions {
  maxScale?: number;
  maxTranslateY?: number;
}

export function useParallax(
  ref: RefObject<HTMLElement | null>,
  bgRef: RefObject<HTMLElement | null>,
  options?: ParallaxOptions,
): void {
  const { maxScale = 1.15, maxTranslateY = -20 } = options ?? {};
  const reducedMotion = useReducedMotion();

  useEffect(() => {
    if (reducedMotion) return;

    const el = ref.current;
    const bg = bgRef.current;
    if (!el || !bg) return;

    let ticking = false;

    function onScroll() {
      if (ticking) return;
      ticking = true;
      requestAnimationFrame(() => {
        const rect = el!.getBoundingClientRect();
        const windowHeight = window.innerHeight;
        // progress: 0 when element top is at viewport bottom, 1 when element bottom is at viewport top
        const progress = Math.max(0, Math.min(1,
          (windowHeight - rect.top) / (windowHeight + rect.height),
        ));
        const scale = 1 + progress * (maxScale - 1);
        const translateY = progress * maxTranslateY;
        bg!.style.transform = `scale(${scale}) translateY(${translateY}px)`;
        ticking = false;
      });
    }

    window.addEventListener("scroll", onScroll, { passive: true });
    onScroll(); // initial position
    return () => window.removeEventListener("scroll", onScroll);
  }, [ref, bgRef, maxScale, maxTranslateY, reducedMotion]);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/hooks/useParallax.test.ts`
Expected: All passing

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/hooks/useParallax.ts apps/agent-site/__tests__/hooks/useParallax.test.ts
git commit -m "feat: add useParallax hook with rAF-throttled scroll transform"
```

---

### Task 4: Hooks Barrel Export

**Files:**
- Create: `apps/agent-site/hooks/index.ts`

- [ ] **Step 1: Create barrel export**

Create `apps/agent-site/hooks/index.ts`:

```typescript
export { useReducedMotion } from "./useReducedMotion";
export { useScrollReveal } from "./useScrollReveal";
export { useParallax } from "./useParallax";
```

- [ ] **Step 2: Run all hook tests**

Run: `cd apps/agent-site && npx vitest run __tests__/hooks/`
Expected: All passing

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/hooks/index.ts
git commit -m "chore: add hooks barrel export"
```

---

## Chunk 2: Schema Updates

### Task 5: Type Definitions

**Files:**
- Modify: `apps/agent-site/lib/types.ts`
- Modify: `apps/agent-site/components/sections/types.ts`
- Modify: `apps/agent-site/__tests__/components/fixtures.ts`

- [ ] **Step 1: Add MarqueeData and MarqueeItem to lib/types.ts**

After the `CityPagesData` type (~line 243), add:

```typescript
// Marquee section
export interface MarqueeItem {
  text: string;
  logo_url?: string;
  link?: string;
}
export type MarqueeData = { title?: string; items: MarqueeItem[] };
```

- [ ] **Step 2: Add marquee to PageSections**

In the `PageSections` interface (~line 125-136), add after `city_pages`:

```typescript
  marquee?: SectionConfig<MarqueeData>;
```

- [ ] **Step 3: Add optional fields to FeatureItem**

In `FeatureItem` (~line 155-160), add:

```typescript
  background_color?: string;
  image_url?: string;
```

- [ ] **Step 4: Add background_image to HeroData**

In `HeroData` (~line 140-147), add:

```typescript
  background_image?: string;
```

- [ ] **Step 5: Add MarqueeProps to sections/types.ts**

After the `AboutProps` interface (~line 58), add:

```typescript
export interface MarqueeProps {
  title?: string;
  items: MarqueeItem[];
}
```

Also add `MarqueeItem` to the existing imports at the top of `components/sections/types.ts`:

```typescript
import type { ..., MarqueeItem } from "@/lib/types";
```

- [ ] **Step 6: Add marquee to test fixtures**

In `apps/agent-site/__tests__/components/fixtures.ts`, add to the `CONTENT` fixture's `sections` object (after `city_pages`):

```typescript
        marquee: {
          enabled: false,
          data: {
            title: "As Featured In",
            items: [
              { text: "LUXURY HOMES MAGAZINE" },
              { text: "WALL STREET JOURNAL", link: "https://example.com" },
              { text: "ARCHITECTURAL DIGEST" },
            ],
          },
        },
```

And add to `CONTENT_ALL_DISABLED`:

```typescript
        marquee: { enabled: false, data: { items: [] } },
```

- [ ] **Step 7: Run existing tests to verify no regressions**

Run: `cd apps/agent-site && npx vitest run`
Expected: All existing tests pass (schema additions are backward-compatible)

- [ ] **Step 8: Commit**

```bash
git add apps/agent-site/lib/types.ts apps/agent-site/components/sections/types.ts apps/agent-site/__tests__/components/fixtures.ts
git commit -m "feat: add MarqueeData, MarqueeProps, and optional fields to FeatureItem/HeroData"
```

---

## Chunk 3: Premium Components

### Task 6: MarqueeBanner Component

**Files:**
- Create: `apps/agent-site/components/sections/marquee/MarqueeBanner.tsx`
- Create: `apps/agent-site/components/sections/marquee/index.ts`
- Create: `apps/agent-site/__tests__/components/marquee/MarqueeBanner.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/agent-site/__tests__/components/marquee/MarqueeBanner.test.tsx`:

```tsx
// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, fireEvent } from "@testing-library/react";
import { MarqueeBanner } from "@/components/sections/marquee/MarqueeBanner";
import type { MarqueeItem } from "@/lib/types";

// Mock useReducedMotion
vi.mock("@/hooks/useReducedMotion", () => ({
  useReducedMotion: vi.fn(() => false),
}));
import { useReducedMotion } from "@/hooks/useReducedMotion";
const mockUseReducedMotion = useReducedMotion as unknown as ReturnType<typeof vi.fn>;

const ITEMS: MarqueeItem[] = [
  { text: "LUXURY HOMES MAGAZINE" },
  { text: "WALL STREET JOURNAL", link: "https://wsj.com" },
  { text: "ARCHITECTURAL DIGEST" },
];

describe("MarqueeBanner", () => {
  afterEach(() => { vi.restoreAllMocks(); });

  it("renders nothing when items is empty", () => {
    const { container } = render(<MarqueeBanner items={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it("renders the title when provided", () => {
    const { getByText } = render(<MarqueeBanner items={ITEMS} title="As Featured In" />);
    expect(getByText("As Featured In")).toBeTruthy();
  });

  it("renders all item texts (duplicated for seamless loop)", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    // Each item appears twice (original + duplicate for infinite scroll)
    const texts = container.querySelectorAll("[data-marquee-item]");
    expect(texts.length).toBe(6); // 3 items x 2
  });

  it("has aria-hidden on the banner container", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const section = container.querySelector("[aria-hidden='true']");
    expect(section).toBeTruthy();
  });

  it("renders links with tabindex=-1 and aria-hidden", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const links = container.querySelectorAll("a");
    links.forEach((link) => {
      expect(link.getAttribute("tabindex")).toBe("-1");
      expect(link.getAttribute("aria-hidden")).toBe("true");
    });
  });

  it("renders static row when reduced motion is enabled", () => {
    mockUseReducedMotion.mockReturnValue(true);
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    // Should NOT have duplicated items
    const texts = container.querySelectorAll("[data-marquee-item]");
    expect(texts.length).toBe(3); // No duplication
  });

  it("renders single item as static display", () => {
    const { container } = render(<MarqueeBanner items={[{ text: "ONLY ONE" }]} />);
    const texts = container.querySelectorAll("[data-marquee-item]");
    expect(texts.length).toBe(1);
  });

  it("injects a style tag for keyframes", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const styleTag = container.querySelector("style");
    expect(styleTag?.textContent).toContain("@keyframes");
  });

  it("pauses animation on hover", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const track = container.querySelector("[data-marquee-track]") as HTMLElement;
    expect(track.style.animationPlayState).not.toBe("paused");
    fireEvent.mouseEnter(track);
    expect(track.style.animationPlayState).toBe("paused");
    fireEvent.mouseLeave(track);
    expect(track.style.animationPlayState).not.toBe("paused");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/marquee/MarqueeBanner.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write implementation**

Create `apps/agent-site/components/sections/marquee/MarqueeBanner.tsx`:

```tsx
"use client";

import { useState } from "react";
import { useReducedMotion } from "@/hooks/useReducedMotion";
import type { MarqueeProps } from "@/components/sections/types";

export function MarqueeBanner({ items, title }: MarqueeProps) {
  const reducedMotion = useReducedMotion();
  const [hovered, setHovered] = useState(false);

  if (items.length === 0) return null;

  const isStatic = reducedMotion || items.length === 1;
  const displayItems = isStatic ? items : [...items, ...items];
  const duration = items.length * 3;

  const renderItem = (item: (typeof items)[number], index: number) => {
    const content = (
      <span
        data-marquee-item
        key={index}
        style={{
          color: "rgba(0,0,0,0.35)",
          fontSize: "14px",
          fontWeight: 600,
          letterSpacing: "3px",
          textTransform: "uppercase" as const,
          whiteSpace: "nowrap" as const,
        }}
      >
        {item.text}
      </span>
    );

    if (item.link) {
      return (
        <a
          key={index}
          href={item.link}
          tabIndex={-1}
          aria-hidden="true"
          style={{ textDecoration: "none" }}
        >
          {content}
        </a>
      );
    }

    return content;
  };

  const separator = (key: string) => (
    <span key={key} style={{ color: "rgba(0,0,0,0.15)", fontSize: "8px", margin: "0 24px" }}>
      ◆
    </span>
  );

  const interleaved: React.ReactNode[] = [];
  displayItems.forEach((item, i) => {
    if (i > 0) interleaved.push(separator(`sep-${i}`));
    interleaved.push(renderItem(item, i));
  });

  return (
    <div
      aria-hidden="true"
      style={{
        background: "var(--color-bg, #fafaf8)",
        padding: "20px 0",
        overflow: "hidden",
        position: "relative" as const,
      }}
    >
      {!isStatic && (
        <style>{`
          @keyframes marquee-scroll {
            0% { transform: translateX(0); }
            100% { transform: translateX(-50%); }
          }
        `}</style>
      )}
      {title && (
        <div style={{
          textAlign: "center" as const,
          fontSize: "11px",
          textTransform: "uppercase" as const,
          letterSpacing: "3px",
          color: "rgba(0,0,0,0.4)",
          marginBottom: "12px",
          fontWeight: 500,
        }}>
          {title}
        </div>
      )}
      <div
        data-marquee-track
        onMouseEnter={() => setHovered(true)}
        onMouseLeave={() => setHovered(false)}
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: isStatic ? "center" : undefined,
          gap: isStatic ? "24px" : undefined,
          ...(isStatic
            ? { flexWrap: "wrap" as const }
            : {
                animation: `marquee-scroll ${duration}s linear infinite`,
                animationPlayState: hovered ? "paused" : "running",
                width: "max-content",
              }),
        }}
      >
        {interleaved}
      </div>
    </div>
  );
}
```

Create `apps/agent-site/components/sections/marquee/index.ts`:

```typescript
export { MarqueeBanner } from "./MarqueeBanner";
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/components/marquee/MarqueeBanner.test.tsx`
Expected: All passing

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/marquee/ apps/agent-site/__tests__/components/marquee/
git commit -m "feat: add MarqueeBanner component with continuous scroll and a11y"
```

---

### Task 7: TestimonialsSpotlight Component

**Files:**
- Create: `apps/agent-site/components/sections/testimonials/TestimonialsSpotlight.tsx`
- Create: `apps/agent-site/__tests__/components/testimonials/TestimonialsSpotlight.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/agent-site/__tests__/components/testimonials/TestimonialsSpotlight.test.tsx`:

```tsx
// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, fireEvent, act } from "@testing-library/react";
import { TestimonialsSpotlight } from "@/components/sections/testimonials/TestimonialsSpotlight";
import type { TestimonialItem } from "@/lib/types";

vi.mock("@/hooks/useReducedMotion", () => ({
  useReducedMotion: vi.fn(() => false),
}));
import { useReducedMotion } from "@/hooks/useReducedMotion";
const mockUseReducedMotion = useReducedMotion as unknown as ReturnType<typeof vi.fn>;

const ITEMS: TestimonialItem[] = [
  { text: "Amazing service!", reviewer: "Alice B.", rating: 5, source: "Zillow" },
  { text: "Would recommend.", reviewer: "Tom C.", rating: 4 },
  { text: "Smooth process.", reviewer: "Sara D.", rating: 3 },
];

describe("TestimonialsSpotlight", () => {
  beforeEach(() => { vi.useFakeTimers(); });
  afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); });

  it("renders nothing when items is empty", () => {
    const { container } = render(<TestimonialsSpotlight items={[]} />);
    expect(container.querySelector("section")).toBeNull();
  });

  it("renders single item without dots or arrows", () => {
    const { getByText, container } = render(<TestimonialsSpotlight items={[ITEMS[0]]} />);
    expect(getByText("Amazing service!")).toBeTruthy();
    expect(container.querySelectorAll("[role='tab']")).toHaveLength(0);
  });

  it("renders first testimonial by default", () => {
    const { getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    expect(getByText("Amazing service!")).toBeTruthy();
    expect(getByText("Alice B.")).toBeTruthy();
  });

  it("renders dot indicators for multiple items", () => {
    const { container } = render(<TestimonialsSpotlight items={ITEMS} />);
    const dots = container.querySelectorAll("[role='tab']");
    expect(dots).toHaveLength(3);
  });

  it("navigates to specific review when dot is clicked", () => {
    const { container, getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    const dots = container.querySelectorAll("[role='tab']");
    fireEvent.click(dots[1]);
    expect(getByText("Would recommend.")).toBeTruthy();
  });

  it("auto-rotates after 5 seconds", () => {
    const { getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    expect(getByText("Amazing service!")).toBeTruthy();
    act(() => { vi.advanceTimersByTime(5000); });
    expect(getByText("Would recommend.")).toBeTruthy();
  });

  it("does not auto-rotate when reduced motion is enabled", () => {
    mockUseReducedMotion.mockReturnValue(true);
    const { getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    act(() => { vi.advanceTimersByTime(15000); });
    expect(getByText("Amazing service!")).toBeTruthy(); // still on first
  });

  it("has aria-live='polite' on review container", () => {
    const { container } = render(<TestimonialsSpotlight items={ITEMS} />);
    expect(container.querySelector("[aria-live='polite']")).toBeTruthy();
  });

  it("renders FTC disclaimer", () => {
    const { getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    expect(getByText(/Real reviews from real clients/)).toBeTruthy();
  });

  it("renders star rating", () => {
    const { container } = render(<TestimonialsSpotlight items={ITEMS} />);
    // Should have 5 star elements (filled based on rating)
    const stars = container.querySelectorAll("[data-star]");
    expect(stars.length).toBeGreaterThanOrEqual(5);
  });

  it("renders title when provided", () => {
    const { getByText } = render(<TestimonialsSpotlight items={ITEMS} title="What Clients Say" />);
    expect(getByText("What Clients Say")).toBeTruthy();
  });

  it("renders source when available", () => {
    const { getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    expect(getByText(/Zillow/)).toBeTruthy();
  });

  it("navigates with arrow keys on tablist", () => {
    const { container, getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    const tablist = container.querySelector("[role='tablist']") as HTMLElement;
    fireEvent.keyDown(tablist, { key: "ArrowRight" });
    expect(getByText("Would recommend.")).toBeTruthy();
    fireEvent.keyDown(tablist, { key: "ArrowLeft" });
    expect(getByText("Amazing service!")).toBeTruthy();
  });

  it("pauses auto-rotation on focus within section", () => {
    const { container, getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    const section = container.querySelector("section") as HTMLElement;
    fireEvent.focus(section);
    act(() => { vi.advanceTimersByTime(15000); });
    expect(getByText("Amazing service!")).toBeTruthy(); // still on first
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/testimonials/TestimonialsSpotlight.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write implementation**

Create `apps/agent-site/components/sections/testimonials/TestimonialsSpotlight.tsx`:

```tsx
"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { useReducedMotion } from "@/hooks/useReducedMotion";
import type { TestimonialsProps } from "@/components/sections/types";
import { clampRating, FTC_DISCLAIMER } from "@/components/sections/types";

const ROTATION_INTERVAL = 5000;

export function TestimonialsSpotlight({ items, title }: TestimonialsProps) {
  const [current, setCurrent] = useState(0);
  const [paused, setPaused] = useState(false);
  const reducedMotion = useReducedMotion();
  const sectionRef = useRef<HTMLElement>(null);

  const goTo = useCallback((index: number) => {
    setCurrent(((index % items.length) + items.length) % items.length);
  }, [items.length]);

  // Auto-rotate
  useEffect(() => {
    if (reducedMotion || paused || items.length <= 1) return;
    const id = setInterval(() => goTo(current + 1), ROTATION_INTERVAL);
    return () => clearInterval(id);
  }, [current, paused, reducedMotion, items.length, goTo]);

  // Pause on focus within
  const handleFocus = useCallback(() => setPaused(true), []);
  const handleBlur = useCallback((e: React.FocusEvent) => {
    if (!sectionRef.current?.contains(e.relatedTarget)) setPaused(false);
  }, []);

  if (items.length === 0) return null;

  const item = items[current];
  const rating = clampRating(item.rating);
  const showNav = items.length > 1;

  const stars = Array.from({ length: 5 }, (_, i) => (
    <span
      key={i}
      data-star
      style={{ color: i < rating ? "#F9A825" : "#E0E0E0", fontSize: "18px" }}
    >
      ★
    </span>
  ));

  return (
    <section
      id="testimonials"
      ref={sectionRef}
      onFocus={handleFocus}
      onBlur={handleBlur}
      style={{
        padding: "80px 24px",
        background: "var(--color-bg, #fafaf8)",
        textAlign: "center" as const,
      }}
    >
      {title && (
        <h2 style={{
          fontSize: "28px",
          fontWeight: 700,
          marginBottom: "40px",
          color: "var(--color-text, #1a1a1a)",
          fontFamily: "var(--font-family, inherit)",
        }}>
          {title}
        </h2>
      )}

      <div
        style={{
          fontSize: "72px",
          color: "rgba(0,0,0,0.06)",
          fontFamily: "Georgia, serif",
          lineHeight: 1,
          marginBottom: "8px",
        }}
      >
        {"\u201C"}
      </div>

      <div aria-live="polite" style={{ minHeight: "160px" }}>
        <p style={{
          fontSize: "20px",
          lineHeight: 1.7,
          fontStyle: "italic" as const,
          color: "var(--color-text, #333)",
          maxWidth: "600px",
          margin: "0 auto 24px",
          overflowWrap: "break-word" as const,
          transition: "opacity 0.6s ease",
        }}>
          {item.text}
        </p>

        <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: "12px" }}>
          <div style={{
            width: "48px",
            height: "48px",
            borderRadius: "50%",
            background: "#e0e0e0",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontSize: "16px",
            color: "#999",
            fontWeight: 600,
          }}>
            {item.reviewer.split(" ").map((w) => w[0]).join("").slice(0, 2).toUpperCase()}
          </div>
          <div style={{ textAlign: "left" as const }}>
            <div style={{ fontWeight: 600, fontSize: "15px", color: "var(--color-text, #333)" }}>
              {item.reviewer}
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
              {item.source && (
                <span style={{ color: "#999", fontSize: "12px" }}>{item.source} · </span>
              )}
              <span>{stars}</span>
            </div>
          </div>
        </div>
      </div>

      {showNav && (
        <div
          role="tablist"
          onKeyDown={(e) => {
            if (e.key === "ArrowRight") goTo(current + 1);
            if (e.key === "ArrowLeft") goTo(current - 1);
          }}
          style={{
            display: "flex",
            justifyContent: "center",
            gap: "8px",
            marginTop: "24px",
          }}
        >
          {items.map((_, i) => (
            <button
              key={i}
              role="tab"
              aria-selected={i === current}
              onClick={() => goTo(i)}
              style={{
                width: "10px",
                height: "10px",
                borderRadius: "50%",
                border: "none",
                background: i === current ? "var(--color-primary, #333)" : "#ddd",
                cursor: "pointer",
                padding: 0,
                transition: "background 0.3s",
              }}
            />
          ))}
        </div>
      )}

      <p style={{
        fontSize: "11px",
        color: "rgba(0,0,0,0.4)",
        maxWidth: "500px",
        margin: "24px auto 0",
        lineHeight: 1.6,
      }}>
        {FTC_DISCLAIMER}
      </p>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/components/testimonials/TestimonialsSpotlight.test.tsx`
Expected: All passing

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/testimonials/TestimonialsSpotlight.tsx apps/agent-site/__tests__/components/testimonials/TestimonialsSpotlight.test.tsx
git commit -m "feat: add TestimonialsSpotlight with auto-rotation and a11y"
```

---

### Task 8: ServicesPremium Component

**Files:**
- Create: `apps/agent-site/components/sections/services/ServicesPremium.tsx`
- Create: `apps/agent-site/__tests__/components/services/ServicesPremium.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/agent-site/__tests__/components/services/ServicesPremium.test.tsx`:

```tsx
// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render } from "@testing-library/react";
import { ServicesPremium } from "@/components/sections/services/ServicesPremium";
import type { FeatureItem } from "@/lib/types";

// Mock hooks
vi.mock("@/hooks/useScrollReveal", () => ({
  useScrollReveal: vi.fn(() => true), // Always visible in tests
}));
vi.mock("@/hooks/useReducedMotion", () => ({
  useReducedMotion: vi.fn(() => false),
}));

const ITEMS: FeatureItem[] = [
  { title: "Market Analysis", description: "Deep market insights", category: "Expert" },
  { title: "Negotiation", description: "Data-driven offers", icon: "📊" },
  { title: "Digital Access", description: "Virtual tours" },
];

describe("ServicesPremium", () => {
  afterEach(() => { vi.restoreAllMocks(); });

  it("renders nothing when items is empty", () => {
    const { container } = render(<ServicesPremium items={[]} />);
    expect(container.querySelector("section")).toBeNull();
  });

  it("renders all feature items as full-bleed blocks", () => {
    const { getAllByRole } = render(<ServicesPremium items={ITEMS} />);
    // Each block has heading h3
    const headings = getAllByRole("heading", { level: 3 });
    expect(headings).toHaveLength(3);
    expect(headings[0].textContent).toBe("Market Analysis");
  });

  it("renders title and subtitle when provided", () => {
    const { getByText } = render(
      <ServicesPremium items={ITEMS} title="Our Services" subtitle="Full package" />,
    );
    expect(getByText("Our Services")).toBeTruthy();
    expect(getByText("Full package")).toBeTruthy();
  });

  it("renders category as label when available", () => {
    const { getByText } = render(<ServicesPremium items={ITEMS} />);
    expect(getByText("Expert")).toBeTruthy();
  });

  it("alternates layout direction (text-left/right)", () => {
    const { container } = render(<ServicesPremium items={ITEMS} />);
    const blocks = container.querySelectorAll("[data-feature-block]");
    // First block: normal (text left), second: reversed
    expect(blocks[0]?.getAttribute("data-direction")).toBe("normal");
    expect(blocks[1]?.getAttribute("data-direction")).toBe("reversed");
  });

  it("alternates background colors", () => {
    const { container } = render(<ServicesPremium items={ITEMS} />);
    const blocks = container.querySelectorAll("[data-feature-block]");
    // First: light, second: dark, third: light
    const bg0 = (blocks[0] as HTMLElement).style.background;
    const bg1 = (blocks[1] as HTMLElement).style.background;
    expect(bg0).not.toBe(bg1);
  });

  it("renders icon/emoji in visual area when provided", () => {
    const { getByText } = render(<ServicesPremium items={ITEMS} />);
    expect(getByText("📊")).toBeTruthy();
  });

  it("renders description text", () => {
    const { getByText } = render(<ServicesPremium items={ITEMS} />);
    expect(getByText("Deep market insights")).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/services/ServicesPremium.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write implementation**

Create `apps/agent-site/components/sections/services/ServicesPremium.tsx`:

```tsx
"use client";

import { useRef } from "react";
import { useScrollReveal } from "@/hooks/useScrollReveal";
import type { FeaturesProps } from "@/components/sections/types";

const LIGHT_BG = ["#faf7f2", "#f0f7ff", "#faf7f2"];
const DARK_BG = "#1a1a2e";

export function ServicesPremium({ items, title, subtitle }: FeaturesProps) {
  if (items.length === 0) return null;

  return (
    <section id="features">
      <style>{`
        @media (max-width: 768px) {
          [data-feature-block] {
            flex-direction: column !important;
            padding: 48px 24px !important;
            min-height: auto !important;
            gap: 32px !important;
          }
          [data-feature-block] h3 { font-size: 28px !important; }
          [data-feature-block] .visual-shape { width: 240px !important; height: 240px !important; }
        }
      `}</style>
      {(title || subtitle) && (
        <div style={{ textAlign: "center" as const, padding: "60px 24px 0" }}>
          {title && (
            <h2 style={{
              fontSize: "32px",
              fontWeight: 700,
              color: "var(--color-text, #1a1a1a)",
              fontFamily: "var(--font-family, inherit)",
              marginBottom: subtitle ? "12px" : "0",
            }}>
              {title}
            </h2>
          )}
          {subtitle && (
            <p style={{ fontSize: "16px", color: "#666", maxWidth: "600px", margin: "0 auto" }}>
              {subtitle}
            </p>
          )}
        </div>
      )}
      {items.map((item, i) => (
        <FeatureBlock key={i} item={item} index={i} />
      ))}
    </section>
  );
}

interface FeatureBlockProps {
  item: FeaturesProps["items"][number];
  index: number;
}

function FeatureBlock({ item, index }: FeatureBlockProps) {
  const ref = useRef<HTMLDivElement>(null);
  const isVisible = useScrollReveal(ref);
  const isDark = index % 2 === 1;
  const isReversed = index % 2 === 1;
  const bg = item.background_color ?? (isDark ? DARK_BG : LIGHT_BG[index % LIGHT_BG.length]);

  return (
    <div
      ref={ref}
      data-feature-block
      data-direction={isReversed ? "reversed" : "normal"}
      style={{
        width: "100%",
        minHeight: "70vh",
        display: "flex",
        alignItems: "center",
        padding: "80px 60px",
        gap: "60px",
        flexDirection: isReversed ? "row-reverse" : "row",
        background: bg,
        overflow: "hidden",
      }}
    >
      <div style={{
        maxWidth: "560px",
        opacity: isVisible ? 1 : 0,
        transform: isVisible ? "translateY(0)" : "translateY(40px)",
        transition: "opacity 0.8s ease, transform 0.8s ease",
      }}>
        {item.category && (
          <div style={{
            fontSize: "12px",
            textTransform: "uppercase" as const,
            letterSpacing: "3px",
            marginBottom: "12px",
            fontWeight: 600,
            color: isDark ? "#90caf9" : "var(--color-primary, #81C784)",
          }}>
            {item.category}
          </div>
        )}
        <h3 style={{
          fontSize: "36px",
          fontWeight: 700,
          marginBottom: "16px",
          lineHeight: 1.2,
          color: isDark ? "#fff" : "var(--color-primary, #1B5E20)",
          fontFamily: "var(--font-family, inherit)",
        }}>
          {item.title}
        </h3>
        <p style={{
          fontSize: "17px",
          lineHeight: 1.7,
          color: isDark ? "rgba(255,255,255,0.65)" : "#555",
        }}>
          {item.description}
        </p>
      </div>

      <div style={{
        flex: 1,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        opacity: isVisible ? 1 : 0,
        transform: isVisible ? "scale(1)" : "scale(0.92)",
        transition: "opacity 1s ease 0.2s, transform 1s ease 0.2s",
      }}>
        <div style={{
          width: "320px",
          height: "320px",
          borderRadius: "24px",
          background: isDark
            ? "linear-gradient(135deg, rgba(255,255,255,0.06), rgba(255,255,255,0.02))"
            : `linear-gradient(135deg, ${bg === "#f0f7ff" ? "#e3f2fd, #bbdefb" : "#e8f5e9, #c8e6c9"})`,
          border: isDark ? "1px solid rgba(255,255,255,0.08)" : undefined,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: "80px",
        }}>
          {item.icon ?? ""}
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/components/services/ServicesPremium.test.tsx`
Expected: All passing

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/services/ServicesPremium.tsx apps/agent-site/__tests__/components/services/ServicesPremium.test.tsx
git commit -m "feat: add ServicesPremium full-bleed feature blocks with scroll reveal"
```

---

### Task 9: HeroParallax Component

**Files:**
- Create: `apps/agent-site/components/sections/heroes/HeroParallax.tsx`
- Create: `apps/agent-site/__tests__/components/heroes/HeroParallax.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/agent-site/__tests__/components/heroes/HeroParallax.test.tsx`:

```tsx
// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render } from "@testing-library/react";
import { HeroParallax } from "@/components/sections/heroes/HeroParallax";
import type { HeroData } from "@/lib/types";

// Mock hooks
vi.mock("@/hooks/useParallax", () => ({
  useParallax: vi.fn(),
}));
vi.mock("@/hooks/useReducedMotion", () => ({
  useReducedMotion: vi.fn(() => false),
}));

const DATA: HeroData = {
  headline: "Sell Your Home with Confidence",
  highlight_word: "Confidence",
  tagline: "Expert guidance",
  body: "Test body text",
  cta_text: "Get Report",
  cta_link: "#contact_form",
  background_image: "https://example.com/house.jpg",
};

describe("HeroParallax", () => {
  afterEach(() => { vi.restoreAllMocks(); });

  it("renders headline with highlight word", () => {
    const { getByText } = render(<HeroParallax data={DATA} />);
    expect(getByText("Confidence")).toBeTruthy();
  });

  it("renders body text", () => {
    const { getByText } = render(<HeroParallax data={DATA} />);
    expect(getByText("Test body text")).toBeTruthy();
  });

  it("renders CTA link", () => {
    const { getByText } = render(<HeroParallax data={DATA} />);
    const cta = getByText("Get Report");
    expect(cta.closest("a")?.getAttribute("href")).toBe("#contact_form");
  });

  it("renders background image when provided", () => {
    const { container } = render(<HeroParallax data={DATA} />);
    const bg = container.querySelector("[data-parallax-bg]") as HTMLElement;
    expect(bg?.style.backgroundImage).toContain("house.jpg");
  });

  it("falls back to agent photo when no background_image", () => {
    const dataNoImg = { ...DATA, background_image: undefined };
    const { container } = render(<HeroParallax data={dataNoImg} agentPhotoUrl="https://example.com/agent.jpg" />);
    const bg = container.querySelector("[data-parallax-bg]") as HTMLElement;
    expect(bg?.style.backgroundImage).toContain("agent.jpg");
  });

  it("renders dark overlay", () => {
    const { container } = render(<HeroParallax data={DATA} />);
    const overlay = container.querySelector("[data-overlay]");
    expect(overlay).toBeTruthy();
  });

  it("renders agent name as label when provided", () => {
    const { getByText } = render(<HeroParallax data={DATA} agentName="Jane Smith" />);
    expect(getByText(/Jane Smith/)).toBeTruthy();
  });

  it("renders tagline", () => {
    const { getByText } = render(<HeroParallax data={DATA} />);
    expect(getByText("Expert guidance")).toBeTruthy();
  });

  it("renders at full viewport height", () => {
    const { container } = render(<HeroParallax data={DATA} />);
    const hero = container.firstChild as HTMLElement;
    expect(hero.style.height).toBe("100vh");
  });

  it("background has inset -10% for zoom buffer", () => {
    const { container } = render(<HeroParallax data={DATA} />);
    const bg = container.querySelector("[data-parallax-bg]") as HTMLElement;
    expect(bg?.style.inset).toBe("-10%");
  });

  it("renders scroll indicator", () => {
    const { container } = render(<HeroParallax data={DATA} />);
    const indicator = container.querySelector("[data-scroll-indicator]");
    expect(indicator).toBeTruthy();
    expect(indicator?.textContent).toContain("↓");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/heroes/HeroParallax.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write implementation**

Create `apps/agent-site/components/sections/heroes/HeroParallax.tsx`:

```tsx
"use client";

import { useRef } from "react";
import { useParallax } from "@/hooks/useParallax";
import type { HeroProps } from "@/components/sections/types";

export function HeroParallax({ data, agentPhotoUrl, agentName }: HeroProps) {
  const heroRef = useRef<HTMLDivElement>(null);
  const bgRef = useRef<HTMLDivElement>(null);

  useParallax(heroRef, bgRef);

  const bgImage = data.background_image ?? agentPhotoUrl;

  // Split headline by highlight_word
  let headlineParts: React.ReactNode = data.headline;
  if (data.highlight_word && data.headline.includes(data.highlight_word)) {
    const idx = data.headline.indexOf(data.highlight_word);
    headlineParts = (
      <>
        {data.headline.slice(0, idx)}
        <span style={{ color: "var(--color-accent, #81C784)" }}>{data.highlight_word}</span>
        {data.headline.slice(idx + data.highlight_word.length)}
      </>
    );
  }

  return (
    <div
      ref={heroRef}
      data-hero-parallax
      style={{
        position: "relative" as const,
        height: "100vh",
        overflow: "hidden",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      {/* Parallax background */}
      <div
        ref={bgRef}
        data-parallax-bg
        style={{
          position: "absolute" as const,
          inset: "-10%",
          backgroundImage: bgImage ? `url('${bgImage}')` : undefined,
          backgroundColor: bgImage ? undefined : "var(--color-primary, #1a1a2e)",
          backgroundSize: "cover",
          backgroundPosition: "center",
          willChange: "transform",
        }}
      />

      {/* Dark overlay */}
      <div
        data-overlay
        style={{
          position: "absolute" as const,
          inset: "0",
          background: "rgba(0,0,0,0.45)",
        }}
      />

      {/* Content */}
      <div style={{
        position: "relative" as const,
        zIndex: 2,
        textAlign: "center" as const,
        color: "white",
        maxWidth: "700px",
        padding: "0 24px",
      }}>
        {agentName && (
          <div style={{
            fontSize: "12px",
            textTransform: "uppercase" as const,
            letterSpacing: "4px",
            color: "rgba(255,255,255,0.7)",
            marginBottom: "16px",
          }}>
            {agentName}
          </div>
        )}

        {data.tagline && (
          <div style={{
            fontSize: "14px",
            textTransform: "uppercase" as const,
            letterSpacing: "3px",
            color: "rgba(255,255,255,0.6)",
            marginBottom: "12px",
          }}>
            {data.tagline}
          </div>
        )}

        <h1 style={{
          fontSize: "56px",
          fontWeight: 700,
          lineHeight: 1.1,
          marginBottom: "20px",
          fontFamily: "var(--font-family, inherit)",
        }}>
          {headlineParts}
        </h1>

        {data.body && (
          <p style={{
            fontSize: "18px",
            lineHeight: 1.7,
            color: "rgba(255,255,255,0.85)",
            marginBottom: "32px",
          }}>
            {data.body}
          </p>
        )}

        <a
          href={data.cta_link}
          style={{
            display: "inline-block",
            background: "var(--color-accent, #81C784)",
            color: "var(--color-primary, #1B5E20)",
            padding: "16px 36px",
            borderRadius: "8px",
            fontWeight: 700,
            fontSize: "16px",
            textDecoration: "none",
          }}
        >
          {data.cta_text}
        </a>
      </div>

      {/* Responsive + scroll indicator */}
      <style>{`
        @media (max-width: 768px) {
          [data-hero-parallax] h1 { font-size: 36px !important; }
          [data-hero-parallax] p { font-size: 16px !important; }
        }
        @keyframes hero-bounce {
          0%, 100% { transform: translateX(-50%) translateY(0); }
          50% { transform: translateX(-50%) translateY(8px); }
        }
      `}</style>
      <div
        data-scroll-indicator
        style={{
          position: "absolute" as const,
          bottom: "32px",
          left: "50%",
          transform: "translateX(-50%)",
          zIndex: 2,
          color: "rgba(255,255,255,0.5)",
          fontSize: "12px",
          letterSpacing: "2px",
          textTransform: "uppercase" as const,
          animation: "hero-bounce 2s infinite",
        }}
      >
        ↓
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/components/heroes/HeroParallax.test.tsx`
Expected: All passing

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/heroes/HeroParallax.tsx apps/agent-site/__tests__/components/heroes/HeroParallax.test.tsx
git commit -m "feat: add HeroParallax with background zoom effect"
```

---

### Task 10: AboutParallax Component

**Files:**
- Create: `apps/agent-site/components/sections/about/AboutParallax.tsx`
- Create: `apps/agent-site/__tests__/components/about/AboutParallax.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/agent-site/__tests__/components/about/AboutParallax.test.tsx`:

```tsx
// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render } from "@testing-library/react";
import { AboutParallax } from "@/components/sections/about/AboutParallax";
import { ACCOUNT, AGENT_PROP } from "@/tests/components/fixtures";
import type { AboutData } from "@/lib/types";

vi.mock("@/hooks/useParallax", () => ({
  useParallax: vi.fn(),
}));
vi.mock("@/hooks/useReducedMotion", () => ({
  useReducedMotion: vi.fn(() => false),
}));

const BIO_DATA: AboutData = {
  bio: ["First paragraph.", "Second paragraph."],
  credentials: ["ABR", "CRS"],
  image_url: "https://example.com/agent-photo.jpg",
};

describe("AboutParallax", () => {
  afterEach(() => { vi.restoreAllMocks(); });

  it("renders agent name", () => {
    const { getByText } = render(<AboutParallax agent={ACCOUNT} data={BIO_DATA} />);
    expect(getByText("Jane Smith")).toBeTruthy();
  });

  it("renders bio as multiple paragraphs when array", () => {
    const { getByText } = render(<AboutParallax agent={ACCOUNT} data={BIO_DATA} />);
    expect(getByText("First paragraph.")).toBeTruthy();
    expect(getByText("Second paragraph.")).toBeTruthy();
  });

  it("renders bio as single paragraph when string", () => {
    const data = { ...BIO_DATA, bio: "Single bio." };
    const { getByText } = render(<AboutParallax agent={ACCOUNT} data={data} />);
    expect(getByText("Single bio.")).toBeTruthy();
  });

  it("renders credentials as badges", () => {
    const { getByText } = render(<AboutParallax agent={ACCOUNT} data={BIO_DATA} />);
    expect(getByText("ABR")).toBeTruthy();
    expect(getByText("CRS")).toBeTruthy();
  });

  it("renders parallax background from data.image_url", () => {
    const { container } = render(<AboutParallax agent={ACCOUNT} data={BIO_DATA} />);
    const bg = container.querySelector("[data-parallax-bg]") as HTMLElement;
    expect(bg?.style.backgroundImage).toContain("agent-photo.jpg");
  });

  it("falls back to headshot_url when no image_url", () => {
    const data = { ...BIO_DATA, image_url: undefined };
    const agentWithPhoto = { ...AGENT_PROP, headshot_url: "https://example.com/headshot.jpg" };
    const { container } = render(<AboutParallax agent={agentWithPhoto} data={data} />);
    const bg = container.querySelector("[data-parallax-bg]") as HTMLElement;
    expect(bg?.style.backgroundImage).toContain("headshot.jpg");
  });

  it("renders solid background when no image available", () => {
    const data = { ...BIO_DATA, image_url: undefined };
    const { container } = render(<AboutParallax agent={ACCOUNT} data={data} />);
    const bg = container.querySelector("[data-parallax-bg]") as HTMLElement;
    // No backgroundImage, should have backgroundColor
    expect(bg?.style.backgroundImage).toBeFalsy();
  });

  it("renders content overlay card", () => {
    const { container } = render(<AboutParallax agent={ACCOUNT} data={BIO_DATA} />);
    const card = container.querySelector("[data-about-card]");
    expect(card).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/components/about/AboutParallax.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write implementation**

Create `apps/agent-site/components/sections/about/AboutParallax.tsx`:

```tsx
"use client";

import { useRef } from "react";
import { useParallax } from "@/hooks/useParallax";
import type { AboutProps } from "@/components/sections/types";
import { getDisplayName, getHeadshotUrl } from "@/components/sections/types";

export function AboutParallax({ agent, data }: AboutProps) {
  const sectionRef = useRef<HTMLDivElement>(null);
  const bgRef = useRef<HTMLDivElement>(null);

  useParallax(sectionRef, bgRef);

  const name = getDisplayName(agent);
  const headshot = getHeadshotUrl(agent);
  const bgImage = data.image_url ?? headshot;
  const bioArray = Array.isArray(data.bio) ? data.bio : [data.bio];

  return (
    <section id="about">
      <style>{`
        @media (max-width: 768px) {
          [data-about-card] {
            max-width: 100% !important;
            background: rgba(255,255,255,0.96) !important;
          }
        }
      `}</style>
      <div
        ref={sectionRef}
        style={{
          position: "relative" as const,
          minHeight: "80vh",
          overflow: "hidden",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          padding: "80px 24px",
        }}
      >
        {/* Parallax background */}
        <div
          ref={bgRef}
          data-parallax-bg
          style={{
            position: "absolute" as const,
            inset: "-10%",
            backgroundImage: bgImage ? `url('${bgImage}')` : undefined,
            backgroundColor: bgImage ? undefined : "var(--color-primary, #2E7D32)",
            backgroundSize: "cover",
            backgroundPosition: "center",
            willChange: "transform",
          }}
        />

        {/* Overlay */}
        <div style={{
          position: "absolute" as const,
          inset: "0",
          background: "rgba(0,0,0,0.3)",
        }} />

        {/* Content card */}
        <div
          data-about-card
          style={{
            position: "relative" as const,
            zIndex: 2,
            background: "rgba(255,255,255,0.92)",
            backdropFilter: "blur(12px)",
            borderRadius: "16px",
            maxWidth: "560px",
            padding: "48px 40px",
          }}
        >
          <h2 style={{
            fontSize: "28px",
            fontWeight: 700,
            color: "var(--color-primary, #1B5E20)",
            fontFamily: "var(--font-family, inherit)",
            marginBottom: "20px",
          }}>
            {data.title ?? name}
          </h2>

          {bioArray.map((p, i) => (
            <p key={i} style={{
              fontSize: "16px",
              lineHeight: 1.8,
              color: "#444",
              marginBottom: i < bioArray.length - 1 ? "16px" : "0",
            }}>
              {p}
            </p>
          ))}

          {data.credentials && data.credentials.length > 0 && (
            <div style={{
              display: "flex",
              gap: "8px",
              flexWrap: "wrap" as const,
              marginTop: "20px",
            }}>
              {data.credentials.map((cred, i) => (
                <span key={i} style={{
                  background: "var(--color-primary, #1B5E20)",
                  color: "#fff",
                  fontSize: "12px",
                  fontWeight: 600,
                  padding: "4px 12px",
                  borderRadius: "20px",
                  letterSpacing: "1px",
                }}>
                  {cred}
                </span>
              ))}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/components/about/AboutParallax.test.tsx`
Expected: All passing

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/about/AboutParallax.tsx apps/agent-site/__tests__/components/about/AboutParallax.test.tsx
git commit -m "feat: add AboutParallax with bio card over parallax background"
```

---

## Chunk 4: Section Index & Template Upgrades

### Task 11: Export New Components

**Files:**
- Modify: `apps/agent-site/components/sections/index.ts`

- [ ] **Step 1: Add exports to sections index**

Add to `apps/agent-site/components/sections/index.ts` after the Commercial variants section (~line 93):

```typescript

// Premium variants (shared across upgraded templates)
export { MarqueeBanner } from "./marquee/MarqueeBanner";
export { TestimonialsSpotlight } from "./testimonials/TestimonialsSpotlight";
export { ServicesPremium } from "./services/ServicesPremium";
export { HeroParallax } from "./heroes/HeroParallax";
export { AboutParallax } from "./about/AboutParallax";
```

- [ ] **Step 2: Run build check**

Run: `cd apps/agent-site && npx tsc --noEmit`
Expected: No type errors

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/components/sections/index.ts
git commit -m "chore: export premium section components from index"
```

---

### Task 12: Upgrade LightLuxury Template

**Files:**
- Modify: `apps/agent-site/templates/light-luxury.tsx`
- Modify or create: `apps/agent-site/__tests__/templates/light-luxury.test.tsx`

- [ ] **Step 1: Write/update the test**

Create `apps/agent-site/__tests__/templates/light-luxury.test.tsx`:

```tsx
// @vitest-environment jsdom
import { describe, it, expect, vi } from "vitest";
import { render } from "@testing-library/react";
import { LightLuxury } from "@/templates/light-luxury";
import { ACCOUNT, CONTENT } from "@/tests/components/fixtures";

// Mock all premium components
vi.mock("@/hooks/useParallax", () => ({ useParallax: vi.fn() }));
vi.mock("@/hooks/useScrollReveal", () => ({ useScrollReveal: vi.fn(() => true) }));
vi.mock("@/hooks/useReducedMotion", () => ({ useReducedMotion: vi.fn(() => false) }));

describe("LightLuxury template", () => {
  it("renders without errors", () => {
    const { container } = render(<LightLuxury account={ACCOUNT} content={CONTENT} />);
    expect(container.firstChild).toBeTruthy();
  });

  it("renders HeroParallax (not HeroAiry)", () => {
    const { container } = render(<LightLuxury account={ACCOUNT} content={CONTENT} />);
    // HeroParallax renders 100vh div with parallax bg
    const hero = container.querySelector("[data-parallax-bg]");
    expect(hero).toBeTruthy();
  });

  it("renders marquee when enabled", () => {
    const contentWithMarquee = {
      ...CONTENT,
      pages: {
        ...CONTENT.pages,
        home: {
          sections: {
            ...CONTENT.pages.home.sections,
            marquee: {
              enabled: true,
              data: {
                title: "Featured In",
                items: [{ text: "LUXURY MAG" }],
              },
            },
          },
        },
      },
    };
    const { getByText } = render(<LightLuxury account={ACCOUNT} content={contentWithMarquee} />);
    expect(getByText("LUXURY MAG")).toBeTruthy();
  });

  it("does not render marquee when disabled", () => {
    const { container } = render(<LightLuxury account={ACCOUNT} content={CONTENT} />);
    // CONTENT fixture has marquee.enabled: false
    expect(container.querySelector("[aria-hidden='true']")).toBeNull();
  });

  it("renders TestimonialsSpotlight with FTC disclaimer", () => {
    const { getByText } = render(<LightLuxury account={ACCOUNT} content={CONTENT} />);
    expect(getByText(/Real reviews from real clients/)).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/templates/light-luxury.test.tsx`
Expected: FAIL (still using old components)

- [ ] **Step 3: Update the template**

Replace `apps/agent-site/templates/light-luxury.tsx` with:

```tsx
import { Nav } from "@/components/Nav";
import {
  HeroParallax,
  MarqueeBanner,
  StatsElegant,
  ServicesPremium,
  StepsRefined,
  SoldElegant,
  TestimonialsSpotlight,
  ProfilesClean,
  CmaSection,
  AboutParallax,
  Footer,
} from "@/components/sections";
import { type TemplateProps, getEnabledSections } from "./types";

export function LightLuxury({ account, content, agent }: TemplateProps) {
  const s = content.pages.home.sections;
  const identity = agent ?? account.agent ?? { id: account.handle, name: account.broker?.name ?? account.brokerage.name, title: account.broker?.title ?? "", phone: "", email: "" };
  const enabledSections = getEnabledSections(s);
  return (
    <>
      <Nav account={account} navigation={content.navigation} enabledSections={enabledSections} />
      <div style={{ paddingTop: "0" }}>
        {s.hero?.enabled && (
          <HeroParallax
            data={s.hero.data}
            agentPhotoUrl={identity.headshot_url ?? account.agent?.headshot_url}
            agentName={identity.name}
          />
        )}
        {s.marquee?.enabled && s.marquee.data.items.length > 0 && (
          <MarqueeBanner
            items={s.marquee.data.items}
            title={s.marquee.data.title}
          />
        )}
        {s.stats?.enabled && s.stats.data.items.length > 0 && (
          <StatsElegant items={s.stats.data.items} sourceDisclaimer="Based on MLS data. Individual results may vary." />
        )}
        {s.features?.enabled && (
          <ServicesPremium
            items={s.features.data.items}
            title={s.features.data.title}
            subtitle={s.features.data.subtitle}
          />
        )}
        {s.steps?.enabled && (
          <StepsRefined
            steps={s.steps.data.steps}
            title={s.steps.data.title}
            subtitle={s.steps.data.subtitle}
          />
        )}
        {s.gallery?.enabled && s.gallery.data.items.length > 0 && (
          <SoldElegant
            items={s.gallery.data.items}
            title={s.gallery.data.title}
            subtitle={s.gallery.data.subtitle}
          />
        )}
        {s.testimonials?.enabled && s.testimonials.data.items.length > 0 && (
          <TestimonialsSpotlight
            items={s.testimonials.data.items}
            title={s.testimonials.data.title}
          />
        )}
        {s.profiles?.enabled && s.profiles.data.items.length > 0 && (
          <ProfilesClean
            items={s.profiles.data.items}
            title={s.profiles.data.title}
            subtitle={s.profiles.data.subtitle}
            accountId={account.handle}
          />
        )}
        {s.contact_form?.enabled && (
          <CmaSection
            accountId={identity.id}
            agentName={identity.name}
            defaultState={account.location.state}
            tracking={account.integrations?.tracking}
            data={s.contact_form.data}
            serviceAreas={account.location.service_areas}
          />
        )}
        {s.about?.enabled && <AboutParallax agent={identity} data={s.about.data} />}
        <Footer agent={account} accountId={identity.id} />
      </div>
    </>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/agent-site && npx vitest run __tests__/templates/light-luxury.test.tsx`
Expected: All passing

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/templates/light-luxury.tsx apps/agent-site/__tests__/templates/light-luxury.test.tsx
git commit -m "feat: upgrade LightLuxury to premium variants"
```

---

### Task 13: Upgrade LuxuryEstate Template

**Files:**
- Modify: `apps/agent-site/templates/luxury-estate.tsx`
- Modify or create: `apps/agent-site/__tests__/templates/luxury-estate.test.tsx`

- [ ] **Step 1: Write the test**

Create `apps/agent-site/__tests__/templates/luxury-estate.test.tsx`:

```tsx
// @vitest-environment jsdom
import { describe, it, expect, vi } from "vitest";
import { render } from "@testing-library/react";
import { LuxuryEstate } from "@/templates/luxury-estate";
import { ACCOUNT, CONTENT } from "@/tests/components/fixtures";

vi.mock("@/hooks/useParallax", () => ({ useParallax: vi.fn() }));
vi.mock("@/hooks/useScrollReveal", () => ({ useScrollReveal: vi.fn(() => true) }));
vi.mock("@/hooks/useReducedMotion", () => ({ useReducedMotion: vi.fn(() => false) }));

describe("LuxuryEstate template", () => {
  it("renders without errors", () => {
    const { container } = render(<LuxuryEstate account={ACCOUNT} content={CONTENT} />);
    expect(container.firstChild).toBeTruthy();
  });

  it("renders HeroParallax (not HeroDark)", () => {
    const { container } = render(<LuxuryEstate account={ACCOUNT} content={CONTENT} />);
    expect(container.querySelector("[data-parallax-bg]")).toBeTruthy();
  });

  it("renders marquee when enabled", () => {
    const contentWithMarquee = {
      ...CONTENT,
      pages: {
        ...CONTENT.pages,
        home: {
          sections: {
            ...CONTENT.pages.home.sections,
            marquee: {
              enabled: true,
              data: { title: "Awards", items: [{ text: "TOP PRODUCER" }] },
            },
          },
        },
      },
    };
    const { getByText } = render(<LuxuryEstate account={ACCOUNT} content={contentWithMarquee} />);
    expect(getByText("TOP PRODUCER")).toBeTruthy();
  });

  it("renders TestimonialsSpotlight with FTC disclaimer", () => {
    const { getByText } = render(<LuxuryEstate account={ACCOUNT} content={CONTENT} />);
    expect(getByText(/Real reviews from real clients/)).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/templates/luxury-estate.test.tsx`
Expected: FAIL (still using old components)

- [ ] **Step 3: Update the template**

Update `apps/agent-site/templates/luxury-estate.tsx`:
- Replace imports: `HeroDark` → `HeroParallax`, `ServicesElegant` → `ServicesPremium`, `TestimonialsMinimal` → `TestimonialsSpotlight`, `AboutEditorial` → `AboutParallax`
- Add `MarqueeBanner` import and render between hero and stats
- Keep `SoldCarousel`, `StepsElegant`, `StatsOverlay` (not swapped per spec)

The template structure should mirror `light-luxury.tsx` from Task 12, with these differences:
- Uses `StatsOverlay` (not `StatsElegant`)
- Uses `StepsElegant` (not `StepsRefined`)
- Uses `SoldCarousel` (not `SoldElegant`)
- Uses `ProfilesClean` (same)

- [ ] **Step 4: Run tests**

Run: `cd apps/agent-site && npx vitest run __tests__/templates/luxury-estate.test.tsx`
Expected: All passing

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/templates/luxury-estate.tsx apps/agent-site/__tests__/templates/luxury-estate.test.tsx
git commit -m "feat: upgrade LuxuryEstate to premium variants"
```

---

### Task 14: Upgrade Commercial Template

**Files:**
- Modify: `apps/agent-site/templates/commercial.tsx`
- Modify or create: `apps/agent-site/__tests__/templates/commercial.test.tsx`

- [ ] **Step 1: Write the test**

Create `apps/agent-site/__tests__/templates/commercial.test.tsx`:

```tsx
// @vitest-environment jsdom
import { describe, it, expect, vi } from "vitest";
import { render } from "@testing-library/react";
import { Commercial } from "@/templates/commercial";
import { ACCOUNT, CONTENT } from "@/tests/components/fixtures";

vi.mock("@/hooks/useParallax", () => ({ useParallax: vi.fn() }));
vi.mock("@/hooks/useScrollReveal", () => ({ useScrollReveal: vi.fn(() => true) }));
vi.mock("@/hooks/useReducedMotion", () => ({ useReducedMotion: vi.fn(() => false) }));

describe("Commercial template", () => {
  it("renders without errors", () => {
    const { container } = render(<Commercial account={ACCOUNT} content={CONTENT} />);
    expect(container.firstChild).toBeTruthy();
  });

  it("renders HeroCorporate (NOT HeroParallax — spec keeps corporate hero)", () => {
    const { container } = render(<Commercial account={ACCOUNT} content={CONTENT} />);
    // HeroCorporate does NOT have data-parallax-bg
    expect(container.querySelector("[data-parallax-bg]")).toBeNull();
  });

  it("renders marquee when enabled", () => {
    const contentWithMarquee = {
      ...CONTENT,
      pages: {
        ...CONTENT.pages,
        home: {
          sections: {
            ...CONTENT.pages.home.sections,
            marquee: {
              enabled: true,
              data: { title: "Certifications", items: [{ text: "CCIM" }, { text: "SIOR" }] },
            },
          },
        },
      },
    };
    const { getByText } = render(<Commercial account={ACCOUNT} content={contentWithMarquee} />);
    expect(getByText("CCIM")).toBeTruthy();
  });

  it("renders TestimonialsSpotlight with FTC disclaimer", () => {
    const { getByText } = render(<Commercial account={ACCOUNT} content={CONTENT} />);
    expect(getByText(/Real reviews from real clients/)).toBeTruthy();
  });

  it("renders AboutProfessional (NOT AboutParallax — spec keeps professional about)", () => {
    const { container } = render(<Commercial account={ACCOUNT} content={CONTENT} />);
    // AboutProfessional does NOT have data-about-card
    expect(container.querySelector("[data-about-card]")).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/agent-site && npx vitest run __tests__/templates/commercial.test.tsx`
Expected: FAIL (still using old components)

- [ ] **Step 3: Update the template**

Update `apps/agent-site/templates/commercial.tsx`:
- Replace: `ServicesCommercial` → `ServicesPremium`, `TestimonialsCorporate` → `TestimonialsSpotlight`
- Add: `MarqueeBanner` import and render between hero and stats
- Keep: `HeroCorporate`, `AboutProfessional`, `StepsCorporate`, `SoldMetrics`, `StatsMetrics`, `ProfilesGrid`

- [ ] **Step 4: Run tests**

Run: `cd apps/agent-site && npx vitest run __tests__/templates/commercial.test.tsx`
Expected: All passing

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/templates/commercial.tsx apps/agent-site/__tests__/templates/commercial.test.tsx
git commit -m "feat: upgrade Commercial to premium variants"
```

---

## Chunk 5: Scroll-Reveal Retrofit

### Task 15: Create ScrollRevealSection Wrapper

To avoid modifying 70+ section components individually, create a thin `"use client"` wrapper component that applies scroll-reveal to any child section.

**Files:**
- Create: `apps/agent-site/components/sections/shared/ScrollRevealSection.tsx`
- Create: `apps/agent-site/__tests__/components/shared/ScrollRevealSection.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/agent-site/__tests__/components/shared/ScrollRevealSection.test.tsx`:

```tsx
// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render } from "@testing-library/react";
import { ScrollRevealSection } from "@/components/sections/shared/ScrollRevealSection";

vi.mock("@/hooks/useScrollReveal", () => ({
  useScrollReveal: vi.fn(() => false),
}));
vi.mock("@/hooks/useReducedMotion", () => ({
  useReducedMotion: vi.fn(() => false),
}));
import { useScrollReveal } from "@/hooks/useScrollReveal";
const mockUseScrollReveal = useScrollReveal as unknown as ReturnType<typeof vi.fn>;

describe("ScrollRevealSection", () => {
  afterEach(() => { vi.restoreAllMocks(); });

  it("renders children", () => {
    mockUseScrollReveal.mockReturnValue(true);
    const { getByText } = render(
      <ScrollRevealSection><div>Test Content</div></ScrollRevealSection>,
    );
    expect(getByText("Test Content")).toBeTruthy();
  });

  it("applies hidden styles when not visible", () => {
    mockUseScrollReveal.mockReturnValue(false);
    const { container } = render(
      <ScrollRevealSection><div>Test</div></ScrollRevealSection>,
    );
    const wrapper = container.firstChild as HTMLElement;
    expect(wrapper.style.opacity).toBe("0");
  });

  it("applies visible styles when visible", () => {
    mockUseScrollReveal.mockReturnValue(true);
    const { container } = render(
      <ScrollRevealSection><div>Test</div></ScrollRevealSection>,
    );
    const wrapper = container.firstChild as HTMLElement;
    expect(wrapper.style.opacity).toBe("1");
  });
});
```

- [ ] **Step 2: Write implementation**

Create `apps/agent-site/components/sections/shared/ScrollRevealSection.tsx`:

```tsx
"use client";

import { useRef, type ReactNode } from "react";
import { useScrollReveal } from "@/hooks/useScrollReveal";

interface ScrollRevealSectionProps {
  children: ReactNode;
  delay?: number;
}

export function ScrollRevealSection({ children, delay = 0 }: ScrollRevealSectionProps) {
  const ref = useRef<HTMLDivElement>(null);
  const isVisible = useScrollReveal(ref);

  return (
    <div
      ref={ref}
      style={{
        opacity: isVisible ? 1 : 0,
        transform: isVisible ? "translateY(0)" : "translateY(24px)",
        transition: `opacity 0.6s ease ${delay}ms, transform 0.6s ease ${delay}ms`,
      }}
    >
      {children}
    </div>
  );
}
```

- [ ] **Step 3: Export from index**

Add to `apps/agent-site/components/sections/index.ts`:

```typescript
export { ScrollRevealSection } from "./shared/ScrollRevealSection";
```

- [ ] **Step 4: Run tests**

Run: `cd apps/agent-site && npx vitest run __tests__/components/shared/ScrollRevealSection.test.tsx`
Expected: All passing

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/components/sections/shared/ScrollRevealSection.tsx apps/agent-site/__tests__/components/shared/ScrollRevealSection.test.tsx apps/agent-site/components/sections/index.ts
git commit -m "feat: add ScrollRevealSection wrapper component"
```

---

### Task 16: Retrofit All 10 Templates with ScrollRevealSection

**Files:**
- Modify: all 10 template files in `apps/agent-site/templates/`

The pattern is to wrap each non-hero section in `<ScrollRevealSection>`:

```tsx
// Before:
{s.stats?.enabled && s.stats.data.items.length > 0 && (
  <StatsBar items={s.stats.data.items} />
)}

// After:
{s.stats?.enabled && s.stats.data.items.length > 0 && (
  <ScrollRevealSection>
    <StatsBar items={s.stats.data.items} />
  </ScrollRevealSection>
)}
```

**Do NOT wrap**:
- Hero sections (always visible on page load)
- Footer (always visible)
- Nav (always visible)
- Sections in premium templates that already use `useScrollReveal` internally (`ServicesPremium`)

- [ ] **Step 1: Add import and wrap sections in each template**

For each of the 10 templates:
1. Add `ScrollRevealSection` to the import from `@/components/sections`
2. Wrap stats, features/services, steps, gallery/sold, testimonials, profiles, contact_form, about sections in `<ScrollRevealSection>`
3. Skip hero, footer, nav
4. For LightLuxury/LuxuryEstate/Commercial: skip `ServicesPremium` (has built-in reveal) and marquee (decorative, always visible)

- [ ] **Step 2: Run full test suite**

Run: `cd apps/agent-site && npx vitest run`
Expected: All tests pass

- [ ] **Step 3: Commit**

```bash
git add apps/agent-site/templates/
git commit -m "feat: retrofit all 10 templates with ScrollRevealSection wrappers"
```

---

## Chunk 6: Test Account Content & Full Verification

### Task 17: Update Test Account Content

**Context:** Test accounts may or may not exist for all three upgraded templates. Only `test-light-luxury` is guaranteed to exist. For any missing account (`test-luxury-estate`, `test-commercial`), skip — their content will be set up when those accounts are onboarded. The primary goal here is to add marquee data to whichever accounts already exist with these templates.

**Files:**
- Check and update relevant content.json files in `config/accounts/` to include marquee data

- [ ] **Step 1: Find accounts using upgraded templates**

Run: `grep -r '"template"' config/accounts/ | grep -E '(light-luxury|luxury-estate|commercial)'`

If an account is found, proceed to Step 2 for that account. If none found, add marquee data to the test fixture's `CONTENT` object in `apps/agent-site/__tests__/components/fixtures.ts` (this was done in Task 5 — verify `marquee.enabled` is `false` by default).

- [ ] **Step 2: Add marquee data to matching accounts**

For each matching account's content.json, add a `marquee` section within `pages.home.sections`:

```json
"marquee": {
  "enabled": true,
  "data": {
    "title": "As Featured In",
    "items": [
      { "text": "LUXURY HOMES MAGAZINE" },
      { "text": "NATIONAL ASSOCIATION OF REALTORS" },
      { "text": "WALL STREET JOURNAL" }
    ]
  }
}
```

- [ ] **Step 3: Commit (only if files were changed)**

```bash
git add config/accounts/
git commit -m "feat: add marquee content to premium template test accounts"
```

---

### Task 18: Full Test Suite Verification

- [ ] **Step 1: Run complete test suite**

Run: `cd apps/agent-site && npx vitest run`
Expected: All tests pass (existing + new)

- [ ] **Step 2: Run type check**

Run: `cd apps/agent-site && npx tsc --noEmit`
Expected: No type errors

- [ ] **Step 3: Run lint**

Run: `cd apps/agent-site && npx next lint`
Expected: No errors

- [ ] **Step 4: Final commit (if any fixes needed)**

```bash
git add -A && git commit -m "fix: address test/lint issues from premium components"
```

---

## Summary

| Chunk | Tasks | What it delivers |
|-------|-------|-----------------|
| 1: Foundation Hooks | 1-4 | `useReducedMotion`, `useScrollReveal`, `useParallax` |
| 2: Schema Updates | 5 | Type definitions + test fixtures |
| 3: Premium Components | 6-10 | MarqueeBanner, TestimonialsSpotlight, ServicesPremium, HeroParallax, AboutParallax |
| 4: Template Upgrades | 11-14 | LightLuxury, LuxuryEstate, Commercial swap to premium variants |
| 5: Scroll-Reveal Retrofit | 15-16 | ScrollRevealSection wrapper + all 10 templates retrofitted |
| 6: Verification | 17-18 | Test content + full suite green |

**Total: 18 tasks, ~6 chunks**
