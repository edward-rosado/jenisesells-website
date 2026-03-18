/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { HeroCoastal } from "@/components/sections/heroes/HeroCoastal";
import type { HeroData } from "@/lib/types";

const heroData: HeroData = {
  headline: "Find Your Place by the Sea",
  highlight_word: "by the Sea",
  tagline: "Your Outer Banks Dream Home Awaits",
  body: "Maya Torres brings 15 years of coastal expertise to Outer Banks properties.",
  cta_text: "Explore Beach Homes",
  cta_link: "#cma-form",
};

describe("HeroCoastal", () => {
  it("renders the headline in an h1", () => {
    render(<HeroCoastal data={heroData} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1 }).textContent).toContain("Find Your Place");
  });

  it("renders the tagline", () => {
    render(<HeroCoastal data={heroData} />);
    expect(screen.getByText("Your Outer Banks Dream Home Awaits")).toBeInTheDocument();
  });

  it("renders the body text", () => {
    render(<HeroCoastal data={heroData} />);
    expect(screen.getByText(/Maya Torres brings 15 years/)).toBeInTheDocument();
  });

  it("renders the CTA link with correct href", () => {
    render(<HeroCoastal data={heroData} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#cma-form");
    expect(screen.getByRole("link").textContent).toContain("Explore Beach Homes");
  });

  it("highlights the accent word with var(--color-accent)", () => {
    render(<HeroCoastal data={heroData} />);
    const heading = screen.getByRole("heading", { level: 1 });
    const span = heading.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("by the Sea");
    expect(span!.style.color).toBe("var(--color-accent)");
  });

  it("renders agent photo with teal border when agentPhotoUrl is provided", () => {
    render(<HeroCoastal data={heroData} agentPhotoUrl="/agents/test/headshot.jpg" agentName="Maya" />);
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("alt", "Photo of Maya");
  });

  it("omits agent photo when agentPhotoUrl is not provided", () => {
    render(<HeroCoastal data={heroData} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders with sandy white background", () => {
    const { container } = render(<HeroCoastal data={heroData} />);
    const section = container.querySelector("section");
    expect(section).toBeInTheDocument();
    expect(section!.style.background).toMatch(/#fefcf8|rgb\(254, 252, 248\)/i);
  });

  it("CTA button has teal background and 30px borderRadius", () => {
    const { container } = render(<HeroCoastal data={heroData} />);
    const link = container.querySelector("a");
    expect(link).toBeInTheDocument();
    expect(link!.style.borderRadius).toBe("30px");
    expect(link!.style.background).toMatch(/var\(--color-primary|#2c7a7b/);
  });

  it("does not render body text when body is absent", () => {
    const dataNoBody: HeroData = { ...heroData, body: undefined };
    render(<HeroCoastal data={dataNoBody} />);
    expect(screen.queryByText(/Maya Torres brings/)).not.toBeInTheDocument();
  });

  it("sanitizes javascript: cta_link to #", () => {
    render(<HeroCoastal data={{ ...heroData, cta_link: "javascript:alert(1)" }} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("renders a section element with id hero for nav anchor linking", () => {
    const { container } = render(<HeroCoastal data={heroData} />);
    const section = container.querySelector("section");
    expect(section).toBeInTheDocument();
    expect(section!.id).toBe("hero");
  });

  it("agent photo has rounded shape (borderRadius 20px)", () => {
    const { container } = render(
      <HeroCoastal data={heroData} agentPhotoUrl="/headshot.jpg" agentName="Maya" />
    );
    const photoWrapper = container.querySelector("div[style*='border-radius: 20px']");
    expect(photoWrapper).toBeInTheDocument();
  });

  it("applies hover effect on CTA", () => {
    render(<HeroCoastal data={heroData} />);
    const cta = screen.getByRole("link", { name: /Explore Beach Homes/i });
    fireEvent.mouseEnter(cta);
    expect(cta.style.transform).toBe("translateY(-2px)");
    fireEvent.mouseLeave(cta);
    expect(cta.style.transform).toBe("none");
  });

  it("renders agent photo with generic alt when agentName is not provided", () => {
    render(<HeroCoastal data={heroData} agentPhotoUrl="/agents/test/headshot.jpg" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Agent photo");
  });
});
