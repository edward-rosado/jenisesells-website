// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render } from "@testing-library/react";
import { MarqueeBanner } from "@/features/sections/marquee/MarqueeBanner";
import type { MarqueeItem } from "@/features/config/types";

// Mock useReducedMotion
vi.mock("@/features/shared/useReducedMotion", () => ({
  useReducedMotion: vi.fn(() => false),
}));
import { useReducedMotion } from "@/features/shared/useReducedMotion";
const mockUseReducedMotion = useReducedMotion as unknown as ReturnType<typeof vi.fn>;

const ITEMS: MarqueeItem[] = [
  { text: "LUXURY HOMES MAGAZINE" },
  { text: "WALL STREET JOURNAL", link: "https://wsj.com" },
  { text: "ARCHITECTURAL DIGEST" },
];

// Helper: compute how many times items are repeated to fill the viewport
function expectedRepeats(count: number): number {
  return Math.max(1, Math.ceil(2000 / (count * 200)));
}

describe("MarqueeBanner", () => {
  afterEach(() => { vi.resetAllMocks(); });

  it("renders nothing when items is empty", () => {
    const { container } = render(<MarqueeBanner items={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it("renders the title when provided", () => {
    const { getByText } = render(<MarqueeBanner items={ITEMS} title="As Featured In" />);
    expect(getByText("As Featured In")).toBeTruthy();
  });

  it("repeats items to fill viewport width, doubled for seamless loop", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const texts = container.querySelectorAll("[data-marquee-item]");
    const reps = expectedRepeats(ITEMS.length);
    // Each set has reps * items.length items, and there are 2 sets
    expect(texts.length).toBe(reps * ITEMS.length * 2);
  });

  it("has aria-hidden on the banner container", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const section = container.querySelector("[aria-hidden='true']");
    expect(section).toBeTruthy();
  });

  it("renders links with tabindex=-1 and aria-hidden", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const links = container.querySelectorAll("a");
    expect(links.length).toBeGreaterThanOrEqual(1);
    links.forEach((link) => {
      expect(link.getAttribute("tabindex")).toBe("-1");
      expect(link.getAttribute("aria-hidden")).toBe("true");
    });
  });

  it("renders static row when reduced motion is enabled", () => {
    mockUseReducedMotion.mockReturnValue(true);
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    // Should NOT have duplicated items — static shows original items only
    const texts = container.querySelectorAll("[data-marquee-item]");
    expect(texts.length).toBe(3);
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

  it("runs animation continuously without pause", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const track = container.querySelector("[data-marquee-track]") as HTMLElement;
    expect(track.style.animation).toContain("marquee-scroll");
    expect(track.style.animation).toContain("linear infinite");
  });

  it("calculates duration from filled item count", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const track = container.querySelector("[data-marquee-track]") as HTMLElement;
    const reps = expectedRepeats(ITEMS.length);
    const filledCount = reps * ITEMS.length;
    const expectedDuration = Math.max(20, filledCount * 4);
    expect(track.style.animation).toContain(`${expectedDuration}s`);
  });

  it("includes trailing separators for seamless seam between sets", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const separators = container.querySelectorAll("span");
    const diamonds = Array.from(separators).filter(s => s.textContent === "◆");
    const reps = expectedRepeats(ITEMS.length);
    // Each filled item in each set has one trailing separator
    expect(diamonds.length).toBe(reps * ITEMS.length * 2);
  });

  it("does not inject keyframes style for static display", () => {
    mockUseReducedMotion.mockReturnValue(true);
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const styleTag = container.querySelector("style");
    expect(styleTag).toBeNull();
  });

  it("renders static items without separators in static mode", () => {
    mockUseReducedMotion.mockReturnValue(true);
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const track = container.querySelector("[data-marquee-track]") as HTMLElement;
    expect(track.style.justifyContent).toBe("center");
    expect(track.style.gap).toBe("24px");
  });

  it("uses minimum 1 repeat even for many items", () => {
    // 20 items → repeats = ceil(2000/4000) = 1
    const manyItems: MarqueeItem[] = Array.from({ length: 20 }, (_, i) => ({
      text: `ITEM ${i}`,
    }));
    const { container } = render(<MarqueeBanner items={manyItems} />);
    const texts = container.querySelectorAll("[data-marquee-item]");
    // 1 repeat × 20 items × 2 sets = 40
    expect(texts.length).toBe(40);
  });
});
