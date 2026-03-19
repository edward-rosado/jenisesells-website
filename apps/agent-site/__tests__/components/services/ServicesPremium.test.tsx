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
