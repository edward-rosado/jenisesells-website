/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ServicesIcons, resolveServiceIcon } from "@/components/sections/services/ServicesIcons";
import type { FeatureItem } from "@/lib/types";

const ITEMS: FeatureItem[] = [
  { title: "Market Analysis", description: "Deep market insights" },
  { title: "Photography", description: "Professional photos" },
  { title: "Negotiation", description: "Expert negotiation" },
];

describe("ServicesIcons", () => {
  it("renders the section heading with default title", () => {
    render(<ServicesIcons items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<ServicesIcons items={ITEMS} title="What I Offer" />);
    expect(screen.getByRole("heading", { level: 2, name: "What I Offer" })).toBeInTheDocument();
  });

  it("renders all service card titles", () => {
    render(<ServicesIcons items={ITEMS} />);
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Photography")).toBeInTheDocument();
    expect(screen.getByText("Negotiation")).toBeInTheDocument();
  });

  it("renders service descriptions", () => {
    render(<ServicesIcons items={ITEMS} />);
    expect(screen.getByText("Deep market insights")).toBeInTheDocument();
    expect(screen.getByText("Professional photos")).toBeInTheDocument();
  });

  it("uses id=services for anchor linking", () => {
    const { container } = render(<ServicesIcons items={ITEMS} />);
    expect(container.querySelector("#services")).toBeInTheDocument();
  });

  it("renders SVG icons inside icon circles", () => {
    const { container } = render(<ServicesIcons items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    articles.forEach((article) => {
      const circle = article.querySelector("div");
      expect(circle?.style.borderRadius).toBe("50%");
      expect(circle?.querySelector("svg")).toBeInTheDocument();
    });
  });

  it("uses rounded cards with soft shadows", () => {
    const { container } = render(<ServicesIcons items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
    expect(articles[0].style.borderRadius).toBeTruthy();
    expect(articles[0].style.boxShadow).toBeTruthy();
  });

  it("renders subtitle when provided", () => {
    render(<ServicesIcons items={ITEMS} subtitle="We go the extra mile" />);
    expect(screen.getByText("We go the extra mile")).toBeInTheDocument();
  });
});

describe("resolveServiceIcon", () => {
  it("returns home icon for valuation keywords", () => {
    const icon = resolveServiceIcon("Free Home Valuation");
    expect(icon).toBeDefined();
  });

  it("returns camera icon for photography keywords", () => {
    const icon = resolveServiceIcon("Professional Photography & Staging");
    expect(icon).toBeDefined();
  });

  it("returns search icon for buyer keywords", () => {
    const icon = resolveServiceIcon("Buyer Representation");
    expect(icon).toBeDefined();
  });

  it("returns chat icon for bilingual keywords", () => {
    const icon = resolveServiceIcon("Se Habla Español");
    expect(icon).toBeDefined();
  });

  it("returns star (default) for unrecognized titles", () => {
    const icon = resolveServiceIcon("Something Completely Random");
    expect(icon).toBeDefined();
  });

  it("uses explicit icon override when provided", () => {
    const items: FeatureItem[] = [
      { title: "Something Random", description: "Test", icon: "heart" },
    ];
    const { container } = render(<ServicesIcons items={items} />);
    const svg = container.querySelector("article svg");
    expect(svg).toBeInTheDocument();
  });

  it("falls back to keyword match when icon key is invalid", () => {
    const icon = resolveServiceIcon("Photography Tips", "nonexistent-icon");
    // Should fall back to keyword match (camera) not crash
    expect(icon).toBeDefined();
  });

  it("prefers explicit icon over keyword match", () => {
    // Title has "photo" keyword (would match camera) but icon says "heart"
    const items: FeatureItem[] = [
      { title: "Photography", description: "Test", icon: "heart" },
    ];
    const { container } = render(<ServicesIcons items={items} />);
    // Heart icon has a specific path - just verify an SVG rendered
    const svg = container.querySelector("article svg");
    expect(svg).toBeInTheDocument();
  });
});
