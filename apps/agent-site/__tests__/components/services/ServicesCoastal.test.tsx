/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ServicesCoastal } from "@/components/sections/services/ServicesCoastal";
import type { ServiceItem } from "@/lib/types";

const ITEMS: ServiceItem[] = [
  { title: "Beach Home Search", description: "Find your perfect beachfront property." },
  { title: "Vacation Rental Advisory", description: "Maximize rental income on your coastal property." },
  { title: "Waterfront Valuation", description: "Know what your waterfront home is worth." },
];

describe("ServicesCoastal", () => {
  it("renders the default heading 'Our Services'", () => {
    render(<ServicesCoastal items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "Our Services" })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<ServicesCoastal items={ITEMS} title="Coastal Expertise" />);
    expect(screen.getByRole("heading", { level: 2, name: "Coastal Expertise" })).toBeInTheDocument();
  });

  it("renders all service item titles", () => {
    render(<ServicesCoastal items={ITEMS} />);
    expect(screen.getByText("Beach Home Search")).toBeInTheDocument();
    expect(screen.getByText("Vacation Rental Advisory")).toBeInTheDocument();
    expect(screen.getByText("Waterfront Valuation")).toBeInTheDocument();
  });

  it("renders all service item descriptions", () => {
    render(<ServicesCoastal items={ITEMS} />);
    expect(screen.getByText("Find your perfect beachfront property.")).toBeInTheDocument();
    expect(screen.getByText("Maximize rental income on your coastal property.")).toBeInTheDocument();
    expect(screen.getByText("Know what your waterfront home is worth.")).toBeInTheDocument();
  });

  it("has id=services for anchor linking", () => {
    const { container } = render(<ServicesCoastal items={ITEMS} />);
    expect(container.querySelector("#services")).toBeInTheDocument();
  });

  it("renders each service as an article element", () => {
    const { container } = render(<ServicesCoastal items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
  });

  it("cards have ocean-blue top border", () => {
    const { container } = render(<ServicesCoastal items={ITEMS} />);
    const article = container.querySelector("article");
    expect(article!.style.borderTop).toMatch(/3px solid/);
  });

  it("cards have rounded corners (borderRadius 12px)", () => {
    const { container } = render(<ServicesCoastal items={ITEMS} />);
    const article = container.querySelector("article");
    expect(article!.style.borderRadius).toBe("12px");
  });

  it("cards have sandy background", () => {
    const { container } = render(<ServicesCoastal items={ITEMS} />);
    const article = container.querySelector("article");
    expect(article!.style.background).toMatch(/#fefcf8|rgb\(254, 252, 248\)/i);
  });

  it("renders subtitle when provided", () => {
    render(<ServicesCoastal items={ITEMS} subtitle="We serve coastal communities." />);
    expect(screen.getByText("We serve coastal communities.")).toBeInTheDocument();
  });

  it("renders grid layout container", () => {
    const { container } = render(<ServicesCoastal items={ITEMS} />);
    const grid = container.querySelector("div[style*='grid']");
    expect(grid).toBeInTheDocument();
  });
});
