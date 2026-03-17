/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ServicesHeart } from "@/components/sections/services/ServicesHeart";
import type { ServiceItem } from "@/lib/types";

const ITEMS: ServiceItem[] = [
  { title: "First-Time Buyer Guidance", description: "We'll hold your hand through every step." },
  { title: "Home Staging", description: "Make your home shine for buyers." },
  { title: "Market Analysis", description: "Know your home's real value." },
];

describe("ServicesHeart", () => {
  it("renders the default heading", () => {
    render(<ServicesHeart items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "How We Help" })).toBeInTheDocument();
  });

  it("renders a custom title when provided", () => {
    render(<ServicesHeart items={ITEMS} title="Our Services" />);
    expect(screen.getByRole("heading", { level: 2, name: "Our Services" })).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<ServicesHeart items={ITEMS} subtitle="Tailored to your needs" />);
    expect(screen.getByText("Tailored to your needs")).toBeInTheDocument();
  });

  it("renders all service item titles", () => {
    render(<ServicesHeart items={ITEMS} />);
    expect(screen.getByText("First-Time Buyer Guidance")).toBeInTheDocument();
    expect(screen.getByText("Home Staging")).toBeInTheDocument();
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
  });

  it("renders all service item descriptions", () => {
    render(<ServicesHeart items={ITEMS} />);
    expect(screen.getByText("We'll hold your hand through every step.")).toBeInTheDocument();
    expect(screen.getByText("Make your home shine for buyers.")).toBeInTheDocument();
    expect(screen.getByText("Know your home's real value.")).toBeInTheDocument();
  });

  it("uses id=services for anchor linking", () => {
    const { container } = render(<ServicesHeart items={ITEMS} />);
    expect(container.querySelector("#services")).toBeInTheDocument();
  });

  it("renders each card as an article element", () => {
    const { container } = render(<ServicesHeart items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
  });

  it("cards have rounded corners", () => {
    const { container } = render(<ServicesHeart items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect((articles[0] as HTMLElement).style.borderRadius).toBeTruthy();
  });
});
