/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatsInline } from "@/components/sections/stats/StatsInline";

const ITEMS = [
  { value: "150+", label: "Homes Sold" },
  { value: "$2.5M", label: "Total Volume" },
  { value: "5.0", label: "Rating" },
];

describe("StatsInline", () => {
  it("renders all stat values", () => {
    render(<StatsInline items={ITEMS} />);
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("$2.5M")).toBeInTheDocument();
    expect(screen.getByText("5.0")).toBeInTheDocument();
  });

  it("renders all stat labels", () => {
    render(<StatsInline items={ITEMS} />);
    expect(screen.getByText("Homes Sold")).toBeInTheDocument();
    expect(screen.getByText("Total Volume")).toBeInTheDocument();
    expect(screen.getByText("Rating")).toBeInTheDocument();
  });

  it("has aria-label for accessibility", () => {
    render(<StatsInline items={ITEMS} />);
    expect(screen.getByLabelText("Agent statistics")).toBeInTheDocument();
  });

  it("renders disclaimer when provided", () => {
    render(<StatsInline items={ITEMS} sourceDisclaimer="Data from Zillow." />);
    expect(screen.getByText("Data from Zillow.")).toBeInTheDocument();
  });

  it("does not render disclaimer when not provided", () => {
    const { container } = render(<StatsInline items={ITEMS} />);
    const paragraphs = container.querySelectorAll("p");
    expect(paragraphs.length).toBe(0);
  });

  it("uses warm styling — soft rounded cards with shadows", () => {
    const { container } = render(<StatsInline items={ITEMS} />);
    const cards = container.querySelectorAll("div[style]");
    const styledCards = Array.from(cards).filter(
      (el) => (el as HTMLElement).style.borderRadius && (el as HTMLElement).style.boxShadow
    );
    expect(styledCards.length).toBeGreaterThan(0);
  });

  it("renders dt before dd in DOM order within each stat card", () => {
    const { container } = render(<StatsInline items={ITEMS} />);
    const wrappers = container.querySelectorAll("dl > div");
    wrappers.forEach((wrapper) => {
      const children = Array.from(wrapper.children);
      const dtIndex = children.findIndex((c) => c.tagName === "DT");
      const ddIndex = children.findIndex((c) => c.tagName === "DD");
      expect(dtIndex).not.toBe(-1);
      expect(ddIndex).not.toBe(-1);
      expect(dtIndex).toBeLessThan(ddIndex);
    });
  });
});
