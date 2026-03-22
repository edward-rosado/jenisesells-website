// apps/agent-site/__tests__/components/services/ServicesClean.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ServicesClean } from "@/components/sections/services/ServicesClean";
import type { FeatureItem } from "@/features/config/types";

const ITEMS: FeatureItem[] = [
  { title: "Market Analysis", description: "Deep market insights" },
  { title: "Photography", description: "Professional photos" },
  { title: "Negotiation", description: "Expert negotiation" },
];

describe("ServicesClean", () => {
  it("renders the section heading with default title", () => {
    render(<ServicesClean items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<ServicesClean items={ITEMS} title="My Services" />);
    expect(screen.getByRole("heading", { level: 2, name: "My Services" })).toBeInTheDocument();
  });

  it("renders all service card titles", () => {
    render(<ServicesClean items={ITEMS} />);
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Photography")).toBeInTheDocument();
    expect(screen.getByText("Negotiation")).toBeInTheDocument();
  });

  it("renders all service descriptions", () => {
    render(<ServicesClean items={ITEMS} />);
    expect(screen.getByText("Deep market insights")).toBeInTheDocument();
    expect(screen.getByText("Professional photos")).toBeInTheDocument();
    expect(screen.getByText("Expert negotiation")).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<ServicesClean items={ITEMS} subtitle="Full-service representation" />);
    expect(screen.getByText("Full-service representation")).toBeInTheDocument();
  });

  it("has features section id for anchor linking", () => {
    const { container } = render(<ServicesClean items={ITEMS} />);
    expect(container.querySelector("#features")).toBeInTheDocument();
  });
});
