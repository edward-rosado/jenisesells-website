/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ServicesElegant } from "@/components/sections/services/ServicesElegant";
import type { ServiceItem } from "@/lib/types";

const ITEMS: ServiceItem[] = [
  { title: "Private Portfolio Access", description: "Exclusive access to off-market properties." },
  { title: "White-Glove Service", description: "Concierge-level support throughout your journey." },
  { title: "Investment Advisory", description: "Strategic investment guidance for high-net-worth clients." },
];

describe("ServicesElegant", () => {
  it("renders section with id=services", () => {
    const { container } = render(<ServicesElegant items={ITEMS} />);
    expect(container.querySelector("section#services")).toBeInTheDocument();
  });

  it("renders default heading 'Our Services' when title is not provided", () => {
    render(<ServicesElegant items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "Our Services" })).toBeInTheDocument();
  });

  it("renders custom heading when title is provided", () => {
    render(<ServicesElegant items={ITEMS} title="Bespoke Services" />);
    expect(screen.getByRole("heading", { level: 2, name: "Bespoke Services" })).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<ServicesElegant items={ITEMS} subtitle="Tailored for you" />);
    expect(screen.getByText("Tailored for you")).toBeInTheDocument();
  });

  it("renders all service titles", () => {
    render(<ServicesElegant items={ITEMS} />);
    expect(screen.getByText("Private Portfolio Access")).toBeInTheDocument();
    expect(screen.getByText("White-Glove Service")).toBeInTheDocument();
    expect(screen.getByText("Investment Advisory")).toBeInTheDocument();
  });

  it("renders all service descriptions", () => {
    render(<ServicesElegant items={ITEMS} />);
    expect(screen.getByText("Exclusive access to off-market properties.")).toBeInTheDocument();
    expect(screen.getByText("Concierge-level support throughout your journey.")).toBeInTheDocument();
  });

  it("renders each service as an article element", () => {
    const { container } = render(<ServicesElegant items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles).toHaveLength(3);
  });

  it("uses dark background via CSS variable on section", () => {
    const { container } = render(<ServicesElegant items={ITEMS} />);
    const section = container.querySelector("section#services");
    expect(section!.style.background).toContain("color-primary");
  });

  it("renders accent left border on each article", () => {
    const { container } = render(<ServicesElegant items={ITEMS} />);
    const article = container.querySelector("article");
    expect(article!.style.borderLeft).toContain("color-accent");
  });

  it("renders empty section gracefully when items is empty", () => {
    render(<ServicesElegant items={[]} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });
});
