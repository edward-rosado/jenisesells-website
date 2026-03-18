/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ServicesCommercial } from "@/components/sections/services/ServicesCommercial";
import type { ServiceItem } from "@/lib/types";

const ITEMS_FLAT: ServiceItem[] = [
  { title: "Acquisition Advisory", description: "Find the right asset." },
  { title: "Tenant Representation", description: "Secure the best lease terms." },
  { title: "Site Selection", description: "Identify prime locations." },
];

const ITEMS_CATEGORIZED: ServiceItem[] = [
  { title: "Acquisition Advisory", description: "Find the right asset.", category: "Investment" },
  { title: "Portfolio Management", description: "Optimize your holdings.", category: "Investment" },
  { title: "Tenant Representation", description: "Secure the best lease terms.", category: "Leasing" },
  { title: "Landlord Representation", description: "Maximize your NOI.", category: "Leasing" },
  { title: "Site Selection", description: "Identify prime locations.", category: "Development" },
  { title: "Due Diligence", description: "Know before you commit.", category: "Development" },
];

describe("ServicesCommercial", () => {
  it("renders the default section heading", () => {
    render(<ServicesCommercial items={ITEMS_FLAT} />);
    expect(screen.getByRole("heading", { level: 2, name: "Our Services" })).toBeInTheDocument();
  });

  it("renders a custom heading when title is provided", () => {
    render(<ServicesCommercial items={ITEMS_FLAT} title="What We Do" />);
    expect(screen.getByRole("heading", { level: 2, name: "What We Do" })).toBeInTheDocument();
  });

  it("renders all service titles", () => {
    render(<ServicesCommercial items={ITEMS_FLAT} />);
    expect(screen.getByText("Acquisition Advisory")).toBeInTheDocument();
    expect(screen.getByText("Tenant Representation")).toBeInTheDocument();
    expect(screen.getByText("Site Selection")).toBeInTheDocument();
  });

  it("renders all service descriptions", () => {
    render(<ServicesCommercial items={ITEMS_FLAT} />);
    expect(screen.getByText("Find the right asset.")).toBeInTheDocument();
    expect(screen.getByText("Secure the best lease terms.")).toBeInTheDocument();
  });

  it("uses id=features for anchor linking", () => {
    const { container } = render(<ServicesCommercial items={ITEMS_FLAT} />);
    expect(container.querySelector("#features")).toBeInTheDocument();
  });

  it("renders article elements for each service item (flat)", () => {
    const { container } = render(<ServicesCommercial items={ITEMS_FLAT} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
  });

  it("groups services by category when category is provided", () => {
    render(<ServicesCommercial items={ITEMS_CATEGORIZED} />);
    expect(screen.getByRole("heading", { name: "Investment" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Leasing" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Development" })).toBeInTheDocument();
  });

  it("renders all items when grouped by category", () => {
    render(<ServicesCommercial items={ITEMS_CATEGORIZED} />);
    expect(screen.getByText("Acquisition Advisory")).toBeInTheDocument();
    expect(screen.getByText("Portfolio Management")).toBeInTheDocument();
    expect(screen.getByText("Tenant Representation")).toBeInTheDocument();
    expect(screen.getByText("Landlord Representation")).toBeInTheDocument();
    expect(screen.getByText("Site Selection")).toBeInTheDocument();
    expect(screen.getByText("Due Diligence")).toBeInTheDocument();
  });

  it("renders article elements for each service item (categorized)", () => {
    const { container } = render(<ServicesCommercial items={ITEMS_CATEGORIZED} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(6);
  });

  it("falls back to flat list when no categories are provided", () => {
    render(<ServicesCommercial items={ITEMS_FLAT} />);
    // No category headings should appear
    expect(screen.queryByRole("heading", { name: "Investment" })).not.toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<ServicesCommercial items={ITEMS_FLAT} subtitle="Full-service commercial brokerage." />);
    expect(screen.getByText("Full-service commercial brokerage.")).toBeInTheDocument();
  });

  it("applies hover lift on mouse enter", () => {
    const { container } = render(<ServicesCommercial items={ITEMS_FLAT} />);
    const card = container.querySelectorAll("article")[0] as HTMLElement;
    fireEvent.mouseEnter(card);
    expect(card.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(card);
    expect(card.style.transform).toBe("none");
  });

  it("renders items without category in categorized list (no category heading for that group)", () => {
    const mixed: ServiceItem[] = [
      { title: "Tenant Rep", description: "Lease terms.", category: "Leasing" },
      { title: "Consulting", description: "General advice." },
    ];
    render(<ServicesCommercial items={mixed} />);
    expect(screen.getByRole("heading", { name: "Leasing" })).toBeInTheDocument();
    expect(screen.getByText("Consulting")).toBeInTheDocument();
    // The uncategorized item should NOT produce a category heading
    expect(screen.queryByRole("heading", { name: "uncategorized" })).not.toBeInTheDocument();
  });
});
