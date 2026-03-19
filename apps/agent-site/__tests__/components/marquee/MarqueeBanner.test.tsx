// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
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
  afterEach(() => { vi.resetAllMocks(); });

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
