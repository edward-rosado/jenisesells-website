// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render } from "@testing-library/react";
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
  afterEach(() => { vi.resetAllMocks(); });

  it("renders nothing when items is empty", () => {
    const { container } = render(<MarqueeBanner items={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it("renders the title when provided", () => {
    const { getByText } = render(<MarqueeBanner items={ITEMS} title="As Featured In" />);
    expect(getByText("As Featured In")).toBeTruthy();
  });

  it("renders all item texts duplicated for seamless loop (set-a + set-b)", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    // Each item appears twice (set-a + set-b)
    const texts = container.querySelectorAll("[data-marquee-item]");
    expect(texts.length).toBe(6); // 3 items x 2 sets
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

  it("runs animation continuously without pause", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const track = container.querySelector("[data-marquee-track]") as HTMLElement;
    expect(track.style.animation).toContain("marquee-scroll");
    expect(track.style.animation).toContain("linear infinite");
  });

  it("uses slower duration based on item count", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    const track = container.querySelector("[data-marquee-track]") as HTMLElement;
    // 3 items * 8 = 24s, but min is 20, so 24s
    expect(track.style.animation).toContain("24s");
  });

  it("includes trailing separators for seamless seam between sets", () => {
    const { container } = render(<MarqueeBanner items={ITEMS} />);
    // Each item in each set has a trailing separator (◆)
    // 3 items × 2 sets = 6 separators
    const separators = container.querySelectorAll("span");
    const diamonds = Array.from(separators).filter(s => s.textContent === "◆");
    expect(diamonds.length).toBe(6);
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
});
