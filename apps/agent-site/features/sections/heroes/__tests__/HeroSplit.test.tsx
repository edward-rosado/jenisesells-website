/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { HeroSplit } from "@/features/sections/heroes/HeroSplit";
import type { HeroData } from "@/features/config/types";

const BASE_DATA: HeroData = {
  headline: "Find Your Dream Home",
  tagline: "Modern approach to real estate",
  cta_text: "Get Started",
  cta_link: "#cma-form",
};

describe("HeroSplit", () => {
  it("renders the headline", () => {
    render(<HeroSplit data={BASE_DATA} />);
    expect(screen.getByRole("heading", { level: 1, name: "Find Your Dream Home" })).toBeInTheDocument();
  });

  it("renders the tagline", () => {
    render(<HeroSplit data={BASE_DATA} />);
    expect(screen.getByText("Modern approach to real estate")).toBeInTheDocument();
  });

  it("renders the CTA link", () => {
    render(<HeroSplit data={BASE_DATA} />);
    const link = screen.getByRole("link");
    expect(link.textContent).toContain("Get Started");
    expect(link).toHaveAttribute("href", "#cma-form");
  });

  it("renders a section element", () => {
    const { container } = render(<HeroSplit data={BASE_DATA} />);
    expect(container.querySelector("section")).toBeInTheDocument();
  });

  it("does not render agent photo when not provided", () => {
    render(<HeroSplit data={BASE_DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders agent photo when provided", () => {
    render(<HeroSplit data={BASE_DATA} agentPhotoUrl="/photos/agent.jpg" agentName="Jane Smith" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Photo of Jane Smith");
  });

  it("uses split layout — white/light background, not gradient", () => {
    const { container } = render(<HeroSplit data={BASE_DATA} />);
    const section = container.querySelector("section");
    expect(section?.style.background).not.toContain("gradient");
  });

  it("sanitizes javascript: links to #", () => {
    const data = { ...BASE_DATA, cta_link: "javascript:alert(1)" };
    render(<HeroSplit data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("renders body text when provided", () => {
    const data = { ...BASE_DATA, body: "A modern real estate experience." };
    render(<HeroSplit data={data} />);
    expect(screen.getByText("A modern real estate experience.")).toBeInTheDocument();
  });

  it("highlights the highlight_word in the headline", () => {
    const data = { ...BASE_DATA, headline: "Find Your Dream Home", highlight_word: "Dream" };
    render(<HeroSplit data={data} />);
    const heading = screen.getByRole("heading", { level: 1 });
    const span = heading.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("Dream");
  });

  it("changes style on CTA hover (mouseEnter/mouseLeave)", () => {
    render(<HeroSplit data={BASE_DATA} />);
    const link = screen.getByRole("link");
    fireEvent.mouseEnter(link);
    expect(link.style.transform).toBe("translateY(-2px)");
    fireEvent.mouseLeave(link);
    expect(link.style.transform).toBe("none");
  });

  it("changes style on CTA focus/blur", () => {
    render(<HeroSplit data={BASE_DATA} />);
    const link = screen.getByRole("link");
    fireEvent.focus(link);
    expect(link.style.transform).toBe("translateY(-2px)");
    fireEvent.blur(link);
    expect(link.style.transform).toBe("none");
  });

  it("falls back to generic alt text when agentName is not provided", () => {
    render(<HeroSplit data={BASE_DATA} agentPhotoUrl="/photos/agent.jpg" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Agent photo");
  });
});
