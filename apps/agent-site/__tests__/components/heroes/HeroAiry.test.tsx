/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { HeroAiry } from "@/components/sections/heroes/HeroAiry";
import type { HeroData } from "@/features/config/types";

const heroData: HeroData = {
  headline: "Gracious Living in Fairfield County",
  highlight_word: "Gracious Living",
  tagline: "Refined Living in Fairfield County",
  body: "Isabelle Fontaine brings 25 years of expertise to Connecticut's finest properties.",
  cta_text: "Request a Consultation",
  cta_link: "#cma-form",
};

describe("HeroAiry", () => {
  it("renders the headline in an h1", () => {
    render(<HeroAiry data={heroData} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1 }).textContent).toContain("Gracious Living");
  });

  it("renders the tagline", () => {
    render(<HeroAiry data={heroData} />);
    expect(screen.getByText("Refined Living in Fairfield County")).toBeInTheDocument();
  });

  it("renders the body text", () => {
    render(<HeroAiry data={heroData} />);
    expect(screen.getByText(/Isabelle Fontaine brings 25 years/)).toBeInTheDocument();
  });

  it("renders the CTA link with correct href", () => {
    render(<HeroAiry data={heroData} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#cma-form");
    expect(screen.getByRole("link").textContent).toContain("Request a Consultation");
  });

  it("highlights the accent word with var(--color-accent)", () => {
    render(<HeroAiry data={heroData} />);
    const heading = screen.getByRole("heading", { level: 1 });
    const span = heading.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("Gracious Living");
    expect(span!.style.color).toBe("var(--color-accent)");
  });

  it("renders agent photo with correct alt when agentPhotoUrl is provided", () => {
    render(<HeroAiry data={heroData} agentPhotoUrl="/agents/test/headshot.jpg" agentName="Isabelle" />);
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("alt", "Photo of Isabelle");
  });

  it("omits agent photo when agentPhotoUrl is not provided", () => {
    render(<HeroAiry data={heroData} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders with white background (rgb(255, 255, 255))", () => {
    const { container } = render(<HeroAiry data={heroData} />);
    const section = container.querySelector("section");
    expect(section).toBeInTheDocument();
    // jsdom normalizes #ffffff to rgb(255, 255, 255)
    expect(section!.style.background).toMatch(/255, 255, 255|#ffffff/i);
  });

  it("CTA button has champagne accent background and borderRadius", () => {
    const { container } = render(<HeroAiry data={heroData} />);
    const link = container.querySelector("a");
    expect(link).toBeInTheDocument();
    expect(link!.style.borderRadius).toBeTruthy();
  });

  it("headline has light font weight (300)", () => {
    render(<HeroAiry data={heroData} />);
    const h1 = screen.getByRole("heading", { level: 1 });
    expect(h1.style.fontWeight).toBe("300");
  });

  it("does not render body text when body is absent", () => {
    const dataNoBody: HeroData = { ...heroData, body: undefined };
    render(<HeroAiry data={dataNoBody} />);
    expect(screen.queryByText(/Isabelle Fontaine brings/)).not.toBeInTheDocument();
  });

  it("sanitizes javascript: cta_link to #", () => {
    render(<HeroAiry data={{ ...heroData, cta_link: "javascript:alert(1)" }} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("renders a section element", () => {
    const { container } = render(<HeroAiry data={heroData} />);
    expect(container.querySelector("section")).toBeInTheDocument();
  });

  it("applies hover effect on CTA", () => {
    render(<HeroAiry data={heroData} />);
    const cta = screen.getByRole("link", { name: /Request a Consultation/i });
    fireEvent.mouseEnter(cta);
    expect(cta.style.transform).toBe("translateY(-2px)");
    fireEvent.mouseLeave(cta);
    expect(cta.style.transform).toBe("none");
  });

  it("renders agent photo with generic alt when agentName is not provided", () => {
    render(<HeroAiry data={heroData} agentPhotoUrl="/agents/test/headshot.jpg" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Agent photo");
  });
});
