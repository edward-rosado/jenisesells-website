/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ServicesRefined } from "@/components/sections/services/ServicesRefined";
import type { ServiceItem } from "@/lib/types";

const ITEMS: ServiceItem[] = [
  { title: "Market Valuation", description: "Precise pricing rooted in deep local expertise." },
  { title: "Buyer Representation", description: "Dedicated guidance for discerning buyers." },
  { title: "Listing Strategy", description: "Tailored marketing to attract qualified buyers." },
  { title: "Relocation Services", description: "Seamless transition to Fairfield County." },
  { title: "Estate Sales", description: "Discreet handling of estate property transactions." },
];

describe("ServicesRefined", () => {
  it("renders section with id=features", () => {
    const { container } = render(<ServicesRefined items={ITEMS} />);
    expect(container.querySelector("section#features")).toBeInTheDocument();
  });

  it("renders default heading 'Our Services'", () => {
    render(<ServicesRefined items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "Our Services" })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<ServicesRefined items={ITEMS} title="What We Offer" />);
    expect(screen.getByRole("heading", { level: 2, name: "What We Offer" })).toBeInTheDocument();
  });

  it("renders all service titles", () => {
    render(<ServicesRefined items={ITEMS} />);
    expect(screen.getByText("Market Valuation")).toBeInTheDocument();
    expect(screen.getByText("Buyer Representation")).toBeInTheDocument();
    expect(screen.getByText("Listing Strategy")).toBeInTheDocument();
  });

  it("renders all service descriptions", () => {
    render(<ServicesRefined items={ITEMS} />);
    expect(screen.getByText("Precise pricing rooted in deep local expertise.")).toBeInTheDocument();
    expect(screen.getByText("Dedicated guidance for discerning buyers.")).toBeInTheDocument();
  });

  it("renders each item as an article element", () => {
    const { container } = render(<ServicesRefined items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(5);
  });

  it("alternates background colors on items", () => {
    const { container } = render(<ServicesRefined items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    // Even items (#ffffff) and odd items (#f8f6f3) should differ
    expect(articles[0].style.background).not.toBe(articles[1].style.background);
  });

  it("each article has a champagne left border", () => {
    const { container } = render(<ServicesRefined items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles[0].style.borderLeft).toContain("color-accent");
  });

  it("renders subtitle when provided", () => {
    render(<ServicesRefined items={ITEMS} subtitle="Exceptional service at every step." />);
    expect(screen.getByText("Exceptional service at every step.")).toBeInTheDocument();
  });
});
