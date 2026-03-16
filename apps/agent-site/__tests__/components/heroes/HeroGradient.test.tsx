/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { HeroGradient } from "@/components/sections/heroes/HeroGradient";
import type { HeroData } from "@/lib/types";

const BASE_DATA: HeroData = {
  headline: "Sell Your Home Fast",
  tagline: "Expert guidance every step",
  cta_text: "Get Free Report",
  cta_link: "#cma-form",
};

describe("HeroGradient", () => {
  it("renders the headline", () => {
    render(<HeroGradient data={BASE_DATA} />);
    expect(screen.getByRole("heading", { level: 1, name: "Sell Your Home Fast" })).toBeInTheDocument();
  });

  it("renders the tagline", () => {
    render(<HeroGradient data={BASE_DATA} />);
    expect(screen.getByText("Expert guidance every step")).toBeInTheDocument();
  });

  it("renders the CTA button text", () => {
    render(<HeroGradient data={BASE_DATA} />);
    // The arrow entity is rendered as part of the link text
    expect(screen.getByRole("link")).toBeInTheDocument();
    expect(screen.getByRole("link").textContent).toContain("Get Free Report");
  });

  it("uses the cta_link as the href for anchor-style links", () => {
    render(<HeroGradient data={BASE_DATA} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#cma-form");
  });

  it("allows root-relative links", () => {
    const data = { ...BASE_DATA, cta_link: "/contact" };
    render(<HeroGradient data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "/contact");
  });

  it("allows https links", () => {
    const data = { ...BASE_DATA, cta_link: "https://example.com" };
    render(<HeroGradient data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "https://example.com");
  });

  it("allows http links", () => {
    const data = { ...BASE_DATA, cta_link: "http://example.com" };
    render(<HeroGradient data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "http://example.com");
  });

  it("sanitizes javascript: links to #", () => {
    const data = { ...BASE_DATA, cta_link: "javascript:alert(1)" };
    render(<HeroGradient data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("sanitizes data: links to #", () => {
    const data = { ...BASE_DATA, cta_link: "data:text/html,<h1>xss</h1>" };
    render(<HeroGradient data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("sanitizes a garbage string to #", () => {
    const data = { ...BASE_DATA, cta_link: "not-a-url" };
    render(<HeroGradient data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("renders a section element", () => {
    const { container } = render(<HeroGradient data={BASE_DATA} />);
    expect(container.querySelector("section")).toBeInTheDocument();
  });

  it("does not render agent photo when agentPhotoUrl is not provided", () => {
    render(<HeroGradient data={BASE_DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders agent photo with next/image when agentPhotoUrl is provided", () => {
    render(<HeroGradient data={BASE_DATA} agentPhotoUrl="/photos/agent.jpg" agentName="Jane Smith" />);
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("alt", "Photo of Jane Smith");
  });

  it("uses fallback alt text when agentName is not provided", () => {
    render(<HeroGradient data={BASE_DATA} agentPhotoUrl="/photos/agent.jpg" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Agent photo");
  });

  it("highlights the highlight_word in the headline with accent color", () => {
    const data = { ...BASE_DATA, headline: "Sell with Confidence", highlight_word: "Confidence" };
    render(<HeroGradient data={data} />);
    const heading = screen.getByRole("heading", { level: 1 });
    const span = heading.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("Confidence");
  });

  it("renders headline without highlight when highlight_word is absent", () => {
    render(<HeroGradient data={BASE_DATA} />);
    const heading = screen.getByRole("heading", { level: 1 });
    expect(heading.querySelector("span")).toBeNull();
  });

  it("renders headline without highlight when highlight_word does not match", () => {
    const data = { ...BASE_DATA, highlight_word: "Nonexistent" };
    render(<HeroGradient data={data} />);
    const heading = screen.getByRole("heading", { level: 1 });
    expect(heading.querySelector("span")).toBeNull();
    expect(heading.textContent).toBe("Sell Your Home Fast");
  });

  it("renders body text when provided", () => {
    const data = { ...BASE_DATA, body: "I am a professional agent." };
    render(<HeroGradient data={data} />);
    expect(screen.getByText("I am a professional agent.")).toBeInTheDocument();
  });

  it("CTA changes style on hover", () => {
    render(<HeroGradient data={BASE_DATA} />);
    const cta = screen.getByRole("link");

    // Before hover — accent color background
    expect(cta.style.background).toContain("var(--color-accent)");

    // Hover
    fireEvent.mouseEnter(cta);
    expect(cta.style.background).toBe("white");
    expect(cta.style.transform).toContain("translateY");

    // Leave
    fireEvent.mouseLeave(cta);
    expect(cta.style.background).toContain("var(--color-accent)");
    expect(cta.style.transform).toBe("none");
  });

  it("CTA changes style on focus and reverts on blur", () => {
    render(<HeroGradient data={BASE_DATA} />);
    const cta = screen.getByRole("link");

    // Focus
    fireEvent.focus(cta);
    expect(cta.style.background).toBe("white");
    expect(cta.style.transform).toContain("translateY");

    // Blur
    fireEvent.blur(cta);
    expect(cta.style.background).toContain("var(--color-accent)");
    expect(cta.style.transform).toBe("none");
  });
});
