// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render } from "@testing-library/react";
import { HeroParallax } from "../HeroParallax";
import type { HeroData } from "@/features/config/types";

// Mock hooks
vi.mock("@/features/shared/useParallax", () => ({
  useParallax: vi.fn(),
}));
vi.mock("@/features/shared/useReducedMotion", () => ({
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
