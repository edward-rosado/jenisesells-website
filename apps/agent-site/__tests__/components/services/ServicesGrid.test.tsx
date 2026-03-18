/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ServicesGrid } from "@/components/sections/services/ServicesGrid";
import type { FeatureItem } from "@/lib/types";

const ITEMS: FeatureItem[] = [
  { title: "Market Analysis", description: "Deep market insights" },
  { title: "Professional Photography", description: "Showcase your home" },
  { title: "Expert Negotiation", description: "Get the best price" },
];

describe("ServicesGrid", () => {
  it("renders the section heading", () => {
    render(<ServicesGrid items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "What I Do for You" })).toBeInTheDocument();
  });

  it("renders all service card titles", () => {
    render(<ServicesGrid items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 3, name: "Market Analysis" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 3, name: "Professional Photography" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 3, name: "Expert Negotiation" })).toBeInTheDocument();
  });

  it("renders all service descriptions", () => {
    render(<ServicesGrid items={ITEMS} />);
    expect(screen.getByText("Deep market insights")).toBeInTheDocument();
    expect(screen.getByText("Showcase your home")).toBeInTheDocument();
    expect(screen.getByText("Get the best price")).toBeInTheDocument();
  });

  it("renders no service cards when items is empty", () => {
    render(<ServicesGrid items={[]} />);
    // Heading still present
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
    // No h3 cards
    expect(screen.queryAllByRole("heading", { level: 3 })).toHaveLength(0);
  });

  it("renders exactly the right number of cards", () => {
    render(<ServicesGrid items={ITEMS} />);
    expect(screen.getAllByRole("heading", { level: 3 })).toHaveLength(3);
  });

  it("renders a single service correctly", () => {
    render(<ServicesGrid items={[{ title: "One Service", description: "One desc" }]} />);
    expect(screen.getByRole("heading", { level: 3, name: "One Service" })).toBeInTheDocument();
    expect(screen.getByText("One desc")).toBeInTheDocument();
  });

  it("renders subtitle paragraph when subtitle is provided", () => {
    render(<ServicesGrid items={ITEMS} subtitle="Full-service real estate representation" />);
    expect(screen.getByText("Full-service real estate representation")).toBeInTheDocument();
  });

  it("does not render subtitle paragraph when subtitle is absent", () => {
    render(<ServicesGrid items={ITEMS} />);
    expect(screen.queryByText("Full-service real estate representation")).not.toBeInTheDocument();
  });
});
