/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatsCards } from "@/components/sections/stats/StatsCards";

const ITEMS = [
  { value: "150+", label: "Homes Sold" },
  { value: "$2.5M", label: "Total Volume" },
  { value: "5.0", label: "Rating" },
];

describe("StatsCards", () => {
  it("renders all stat values", () => {
    render(<StatsCards items={ITEMS} />);
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("$2.5M")).toBeInTheDocument();
    expect(screen.getByText("5.0")).toBeInTheDocument();
  });

  it("renders all stat labels", () => {
    render(<StatsCards items={ITEMS} />);
    expect(screen.getByText("Homes Sold")).toBeInTheDocument();
    expect(screen.getByText("Total Volume")).toBeInTheDocument();
    expect(screen.getByText("Rating")).toBeInTheDocument();
  });

  it("has aria-label for accessibility", () => {
    render(<StatsCards items={ITEMS} />);
    expect(screen.getByLabelText("Agent statistics")).toBeInTheDocument();
  });

  it("renders disclaimer when provided", () => {
    render(<StatsCards items={ITEMS} sourceDisclaimer="Data from Zillow." />);
    expect(screen.getByText("Data from Zillow.")).toBeInTheDocument();
  });

  it("does not render disclaimer when not provided", () => {
    const { container } = render(<StatsCards items={ITEMS} />);
    const paragraphs = container.querySelectorAll("p");
    expect(paragraphs.length).toBe(0);
  });

  it("uses bordered cards with clean styling", () => {
    const { container } = render(<StatsCards items={ITEMS} />);
    const section = container.querySelector("section");
    expect(section?.style.background).not.toContain("gradient");
  });

  it("uses dl/dt/dd for semantic markup", () => {
    const { container } = render(<StatsCards items={ITEMS} />);
    expect(container.querySelector("dl")).toBeInTheDocument();
    expect(container.querySelectorAll("dt").length).toBe(3);
    expect(container.querySelectorAll("dd").length).toBe(3);
  });

  it("renders dt before dd in DOM order within each stat card", () => {
    const { container } = render(<StatsCards items={ITEMS} />);
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
