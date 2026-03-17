/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { HeroBold } from "@/components/sections/heroes/HeroBold";
import type { HeroData } from "@/lib/types";

const heroData: HeroData = {
  headline: "Find Your NYC Loft.",
  highlight_word: "NYC Loft",
  tagline: "NYC Apartments. No Hassle.",
  body: "We close deals fast and keep it simple.",
  cta_text: "Get Started",
  cta_link: "#cma-form",
};

describe("HeroBold", () => {
  it("renders the headline in an h1", () => {
    render(<HeroBold data={heroData} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1 }).textContent).toContain("Find Your NYC Loft.");
  });

  it("renders the tagline", () => {
    render(<HeroBold data={heroData} />);
    expect(screen.getByText("NYC Apartments. No Hassle.")).toBeInTheDocument();
  });

  it("renders the primary CTA link", () => {
    render(<HeroBold data={heroData} />);
    const link = screen.getByRole("link", { name: /Get Started/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute("href", "#cma-form");
  });

  it("renders agent photo with correct alt when agentPhotoUrl is provided", () => {
    render(<HeroBold data={heroData} agentPhotoUrl="/agents/test/headshot.jpg" agentName="Kai Nakamura" />);
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("alt", "Photo of Kai Nakamura");
  });

  it("omits agent photo when agentPhotoUrl is not provided", () => {
    render(<HeroBold data={heroData} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("has a rainbow gradient accent bar at the top", () => {
    const { container } = render(<HeroBold data={heroData} />);
    const section = container.querySelector("section");
    expect(section).toBeInTheDocument();
    // Find the accent bar as first child div
    const accentBar = section!.querySelector("div:first-child");
    expect(accentBar).toBeInTheDocument();
    expect(accentBar!.style.background).toContain("linear-gradient");
  });

  it("renders headline with bold font weight (800)", () => {
    render(<HeroBold data={heroData} />);
    const h1 = screen.getByRole("heading", { level: 1 });
    expect(h1.style.fontWeight).toBe("800");
  });

  it("has light background (#fafafa)", () => {
    const { container } = render(<HeroBold data={heroData} />);
    const section = container.querySelector("section");
    // jsdom normalizes #fafafa → rgb(250, 250, 250)
    const bg = section!.style.background;
    expect(bg === "#fafafa" || bg === "rgb(250, 250, 250)").toBe(true);
  });

  it("renders agent photo with rounded style (borderRadius 50%)", () => {
    const { container } = render(<HeroBold data={heroData} agentPhotoUrl="/agents/test/headshot.jpg" agentName="Kai" />);
    const photoWrapper = container.querySelector("[data-photo-wrapper]");
    expect(photoWrapper).toBeInTheDocument();
    expect((photoWrapper as HTMLElement).style.borderRadius).toBe("50%");
  });

  it("sanitizes javascript: cta_link to #", () => {
    render(<HeroBold data={{ ...heroData, cta_link: "javascript:alert(1)" }} />);
    expect(screen.getByRole("link", { name: /Get Started/i })).toHaveAttribute("href", "#");
  });

  it("CTA changes style on hover", () => {
    render(<HeroBold data={heroData} />);
    const cta = screen.getByRole("link", { name: /Get Started/i });
    fireEvent.mouseEnter(cta);
    fireEvent.mouseLeave(cta);
    expect(cta).toBeInTheDocument();
  });
});
