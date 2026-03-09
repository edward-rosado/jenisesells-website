/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { Hero } from "@/components/sections/Hero";
import type { HeroData } from "@/lib/types";

const BASE_DATA: HeroData = {
  headline: "Sell Your Home Fast",
  tagline: "Expert guidance every step",
  cta_text: "Get Free Report",
  cta_link: "#cma-form",
};

describe("Hero", () => {
  it("renders the headline", () => {
    render(<Hero data={BASE_DATA} />);
    expect(screen.getByRole("heading", { level: 1, name: "Sell Your Home Fast" })).toBeInTheDocument();
  });

  it("renders the tagline", () => {
    render(<Hero data={BASE_DATA} />);
    expect(screen.getByText("Expert guidance every step")).toBeInTheDocument();
  });

  it("renders the CTA button text", () => {
    render(<Hero data={BASE_DATA} />);
    // The arrow entity is rendered as part of the link text
    expect(screen.getByRole("link")).toBeInTheDocument();
    expect(screen.getByRole("link").textContent).toContain("Get Free Report");
  });

  it("uses the cta_link as the href for anchor-style links", () => {
    render(<Hero data={BASE_DATA} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#cma-form");
  });

  it("allows root-relative links", () => {
    const data = { ...BASE_DATA, cta_link: "/contact" };
    render(<Hero data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "/contact");
  });

  it("allows https links", () => {
    const data = { ...BASE_DATA, cta_link: "https://example.com" };
    render(<Hero data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "https://example.com");
  });

  it("allows http links", () => {
    const data = { ...BASE_DATA, cta_link: "http://example.com" };
    render(<Hero data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "http://example.com");
  });

  it("sanitizes javascript: links to #", () => {
    const data = { ...BASE_DATA, cta_link: "javascript:alert(1)" };
    render(<Hero data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("sanitizes data: links to #", () => {
    const data = { ...BASE_DATA, cta_link: "data:text/html,<h1>xss</h1>" };
    render(<Hero data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("sanitizes a garbage string to #", () => {
    const data = { ...BASE_DATA, cta_link: "not-a-url" };
    render(<Hero data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("renders a section element", () => {
    const { container } = render(<Hero data={BASE_DATA} />);
    expect(container.querySelector("section")).toBeInTheDocument();
  });

  it("does not render agent photo when agentPhotoUrl is not provided", () => {
    render(<Hero data={BASE_DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders agent photo with next/image when agentPhotoUrl is provided", () => {
    render(<Hero data={BASE_DATA} agentPhotoUrl="/photos/agent.jpg" agentName="Jane Smith" />);
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("alt", "Photo of Jane Smith");
  });

  it("uses fallback alt text when agentName is not provided", () => {
    render(<Hero data={BASE_DATA} agentPhotoUrl="/photos/agent.jpg" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Agent photo");
  });
});
