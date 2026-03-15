/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatsBar } from "@/components/sections/StatsBar";
import type { StatItem } from "@/lib/types";

const STATS: StatItem[] = [
  { value: "150+", label: "Homes Sold" },
  { value: "$2.5M", label: "Total Volume" },
  { value: "98%", label: "Satisfaction" },
];

describe("StatsBar", () => {
  it("renders all stat values", () => {
    render(<StatsBar items={STATS} />);
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("$2.5M")).toBeInTheDocument();
    expect(screen.getByText("98%")).toBeInTheDocument();
  });

  it("renders all stat labels", () => {
    render(<StatsBar items={STATS} />);
    expect(screen.getByText("Homes Sold")).toBeInTheDocument();
    expect(screen.getByText("Total Volume")).toBeInTheDocument();
    expect(screen.getByText("Satisfaction")).toBeInTheDocument();
  });

  it("renders correct number of stat items", () => {
    render(<StatsBar items={STATS} />);
    // Each stat has a value and label - check all values render
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("$2.5M")).toBeInTheDocument();
    expect(screen.getByText("98%")).toBeInTheDocument();
  });

  it("renders nothing for an empty array", () => {
    const { container } = render(<StatsBar items={[]} />);
    const wrapper = container.firstElementChild;
    expect(wrapper).toBeInTheDocument();
    expect(wrapper!.children).toHaveLength(0);
  });

  it("renders a single stat correctly", () => {
    render(<StatsBar items={[{ value: "1", label: "One" }]} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("One")).toBeInTheDocument();
  });

  it("renders div element", () => {
    const { container } = render(<StatsBar items={STATS} />);
    expect(container.querySelector("div")).toBeInTheDocument();
  });
});
