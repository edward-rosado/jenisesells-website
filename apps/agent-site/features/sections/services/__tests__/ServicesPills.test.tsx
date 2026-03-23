/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ServicesPills } from "@/features/sections/services/ServicesPills";
import type { ServiceItem } from "@/features/config/types";

const ITEMS: ServiceItem[] = [
  { title: "Market Analysis", description: "Deep market insights to price right." },
  { title: "Expert Negotiation", description: "Get the best deal possible.", category: "Buyer" },
  { title: "Loft Hunting", description: "Find the perfect loft for you.", category: "Specialty" },
];

describe("ServicesPills", () => {
  it("renders section with id=features", () => {
    const { container } = render(<ServicesPills items={ITEMS} />);
    expect(container.querySelector("section#features")).toBeInTheDocument();
  });

  it("renders the default heading when no title provided", () => {
    render(<ServicesPills items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("What I Offer");
  });

  it("renders custom title when provided", () => {
    render(<ServicesPills items={ITEMS} title="My Services" />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("My Services");
  });

  it("renders subtitle when provided", () => {
    render(<ServicesPills items={ITEMS} subtitle="Full-service brokerage" />);
    expect(screen.getByText("Full-service brokerage")).toBeInTheDocument();
  });

  it("renders all item titles", () => {
    render(<ServicesPills items={ITEMS} />);
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Expert Negotiation")).toBeInTheDocument();
    expect(screen.getByText("Loft Hunting")).toBeInTheDocument();
  });

  it("renders all item descriptions", () => {
    render(<ServicesPills items={ITEMS} />);
    expect(screen.getByText("Deep market insights to price right.")).toBeInTheDocument();
    expect(screen.getByText("Get the best deal possible.")).toBeInTheDocument();
    expect(screen.getByText("Find the perfect loft for you.")).toBeInTheDocument();
  });

  it("renders category pill when category is provided", () => {
    render(<ServicesPills items={ITEMS} />);
    expect(screen.getByText("Buyer")).toBeInTheDocument();
    expect(screen.getByText("Specialty")).toBeInTheDocument();
  });

  it("does not render category pill when category is absent", () => {
    const { container } = render(<ServicesPills items={[{ title: "X", description: "Y" }]} />);
    expect(container.querySelector("[data-category-pill]")).not.toBeInTheDocument();
  });

  it("renders article elements for each service card", () => {
    const { container } = render(<ServicesPills items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
  });

  it("applies hover lift on mouse enter", () => {
    const { container } = render(<ServicesPills items={ITEMS} />);
    const card = container.querySelectorAll("article")[0] as HTMLElement;
    fireEvent.mouseEnter(card);
    expect(card.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(card);
    expect(card.style.transform).toBe("none");
  });

  it("renders empty section gracefully when items is empty", () => {
    const { container } = render(<ServicesPills items={[]} />);
    expect(container.querySelector("section#features")).toBeInTheDocument();
  });
});
