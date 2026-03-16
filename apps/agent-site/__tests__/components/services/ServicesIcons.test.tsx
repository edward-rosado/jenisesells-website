/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ServicesIcons } from "@/components/sections/services/ServicesIcons";
import type { ServiceItem } from "@/lib/types";

const ITEMS: ServiceItem[] = [
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

  it("renders icon circles for each service", () => {
    const { container } = render(<ServicesIcons items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    articles.forEach((article) => {
      const circle = article.querySelector("div");
      expect(circle?.style.borderRadius).toBe("50%");
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
