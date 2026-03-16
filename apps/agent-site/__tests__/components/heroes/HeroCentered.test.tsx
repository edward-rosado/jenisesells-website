/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { HeroCentered } from "@/components/sections/heroes/HeroCentered";
import type { HeroData } from "@/lib/types";

const BASE_DATA: HeroData = {
  headline: "Welcome Home",
  tagline: "Your neighborhood agent",
  cta_text: "Let's Talk",
  cta_link: "#cma-form",
};

describe("HeroCentered", () => {
  it("renders the headline", () => {
    render(<HeroCentered data={BASE_DATA} />);
    expect(screen.getByRole("heading", { level: 1, name: "Welcome Home" })).toBeInTheDocument();
  });

  it("renders the tagline", () => {
    render(<HeroCentered data={BASE_DATA} />);
    expect(screen.getByText("Your neighborhood agent")).toBeInTheDocument();
  });

  it("renders the CTA link", () => {
    render(<HeroCentered data={BASE_DATA} />);
    const link = screen.getByRole("link");
    expect(link.textContent).toContain("Let's Talk");
    expect(link).toHaveAttribute("href", "#cma-form");
  });

  it("renders a section element", () => {
    const { container } = render(<HeroCentered data={BASE_DATA} />);
    expect(container.querySelector("section")).toBeInTheDocument();
  });

  it("uses centered layout with warm background", () => {
    const { container } = render(<HeroCentered data={BASE_DATA} />);
    const section = container.querySelector("section");
    expect(section?.style.textAlign).toBe("center");
    expect(section?.style.background).toBe("rgb(255, 248, 240)");
  });

  it("does not render agent photo when not provided", () => {
    render(<HeroCentered data={BASE_DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders agent photo when provided", () => {
    render(<HeroCentered data={BASE_DATA} agentPhotoUrl="/photos/agent.jpg" agentName="Jane Smith" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Photo of Jane Smith");
  });

  it("sanitizes javascript: links to #", () => {
    const data = { ...BASE_DATA, cta_link: "javascript:alert(1)" };
    render(<HeroCentered data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("sanitizes data: URLs to #", () => {
    const data = { ...BASE_DATA, cta_link: "data:text/html,<h1>xss</h1>" };
    render(<HeroCentered data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("renders body text when provided", () => {
    const data = { ...BASE_DATA, body: "We treat every client like family." };
    render(<HeroCentered data={data} />);
    expect(screen.getByText("We treat every client like family.")).toBeInTheDocument();
  });

  it("highlights the highlight_word in the headline", () => {
    const data = { ...BASE_DATA, headline: "Welcome Home", highlight_word: "Home" };
    render(<HeroCentered data={data} />);
    const heading = screen.getByRole("heading", { level: 1 });
    const span = heading.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("Home");
  });

  it("changes style on CTA hover (mouseEnter/mouseLeave)", () => {
    render(<HeroCentered data={BASE_DATA} />);
    const link = screen.getByRole("link");
    fireEvent.mouseEnter(link);
    expect(link.style.transform).toBe("translateY(-2px)");
    fireEvent.mouseLeave(link);
    expect(link.style.transform).toBe("none");
  });

  it("changes style on CTA focus/blur", () => {
    render(<HeroCentered data={BASE_DATA} />);
    const link = screen.getByRole("link");
    fireEvent.focus(link);
    expect(link.style.transform).toBe("translateY(-2px)");
    fireEvent.blur(link);
    expect(link.style.transform).toBe("none");
  });

  it("falls back to generic alt text when agentName is not provided", () => {
    render(<HeroCentered data={BASE_DATA} agentPhotoUrl="/photos/agent.jpg" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Agent photo");
  });
});
