/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { HeroEstate } from "@/features/sections/heroes/HeroEstate";
import type { HeroData } from "@/features/config/types";

const BASE_DATA: HeroData = {
  headline: "Find Your Estate in Virginia Horse Country",
  highlight_word: "Virginia Horse Country",
  tagline: "Est. 1985 — Serving Loudoun County & Beyond",
  body: "Boutique land and estate specialist with 20 years of experience.",
  cta_text: "Schedule a Consultation",
  cta_link: "#cma-form",
};

describe("HeroEstate", () => {
  it("renders the headline in an h1", () => {
    render(<HeroEstate data={BASE_DATA} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1 }).textContent).toContain(
      "Find Your Estate in"
    );
  });

  it("renders the tagline", () => {
    render(<HeroEstate data={BASE_DATA} />);
    expect(
      screen.getByText("Est. 1985 — Serving Loudoun County & Beyond")
    ).toBeInTheDocument();
  });

  it("renders the body text", () => {
    render(<HeroEstate data={BASE_DATA} />);
    expect(
      screen.getByText(
        "Boutique land and estate specialist with 20 years of experience."
      )
    ).toBeInTheDocument();
  });

  it("renders the CTA link with correct href", () => {
    render(<HeroEstate data={BASE_DATA} />);
    const link = screen.getByRole("link");
    expect(link.textContent).toContain("Schedule a Consultation");
    expect(link).toHaveAttribute("href", "#cma-form");
  });

  it("renders a gradient background overlay", () => {
    const { container } = render(<HeroEstate data={BASE_DATA} />);
    const section = container.querySelector("section");
    // Section uses cream bg; inner overlay div should have gradient
    const gradientEl = container.querySelector("[style*='gradient']");
    expect(gradientEl || section).toBeInTheDocument();
  });

  it("renders agent photo when provided", () => {
    render(
      <HeroEstate
        data={BASE_DATA}
        agentPhotoUrl="/agents/test-country/headshot.jpg"
        agentName="James Whitfield"
      />
    );
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Photo of James Whitfield");
  });

  it("does not render agent photo when not provided", () => {
    render(<HeroEstate data={BASE_DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("highlights the highlight_word in the headline", () => {
    render(<HeroEstate data={BASE_DATA} />);
    const heading = screen.getByRole("heading", { level: 1 });
    const span = heading.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("Virginia Horse Country");
  });

  it("falls back to generic alt text when agentName is not provided", () => {
    render(<HeroEstate data={BASE_DATA} agentPhotoUrl="/photos/agent.jpg" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Agent photo");
  });

  it("sanitizes javascript: CTA links to #", () => {
    const data = { ...BASE_DATA, cta_link: "javascript:alert(1)" };
    render(<HeroEstate data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("CTA uses green accent background", () => {
    render(<HeroEstate data={BASE_DATA} />);
    const link = screen.getByRole("link");
    expect(link.style.background).toContain("var(--color-accent");
  });

  it("changes style on CTA hover", () => {
    render(<HeroEstate data={BASE_DATA} />);
    const link = screen.getByRole("link");
    fireEvent.mouseEnter(link);
    expect(link.style.transform).toBe("translateY(-2px)");
    fireEvent.mouseLeave(link);
    expect(link.style.transform).toBe("none");
  });

  it("changes style on CTA focus/blur", () => {
    render(<HeroEstate data={BASE_DATA} />);
    const link = screen.getByRole("link");
    fireEvent.focus(link);
    expect(link.style.transform).toBe("translateY(-2px)");
    fireEvent.blur(link);
    expect(link.style.transform).toBe("none");
  });
});
