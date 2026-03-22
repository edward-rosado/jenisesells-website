/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StatsBar } from "@/components/sections/stats/StatsBar";
import type { StatItem } from "@/features/config/types";

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

  it("renders empty dl for an empty array", () => {
    const { container } = render(<StatsBar items={[]} />);
    const dl = container.querySelector("dl");
    expect(dl).toBeInTheDocument();
    expect(dl!.children).toHaveLength(0);
  });

  it("renders a single stat correctly", () => {
    render(<StatsBar items={[{ value: "1", label: "One" }]} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("One")).toBeInTheDocument();
  });

  it("renders section with dl element", () => {
    const { container } = render(<StatsBar items={STATS} />);
    expect(container.querySelector("section")).toBeInTheDocument();
    expect(container.querySelector("dl")).toBeInTheDocument();
  });

  it("renders source disclaimer when provided", () => {
    render(<StatsBar items={STATS} sourceDisclaimer="Based on data from Zillow." />);
    expect(screen.getByText("Based on data from Zillow.")).toBeInTheDocument();
  });

  it("applies hover lift on mouse enter", () => {
    const { container } = render(<StatsBar items={STATS} />);
    const item = container.querySelector("dl")!.children[0] as HTMLElement;
    expect(item.style.transform).toBe("none");
    fireEvent.mouseEnter(item);
    expect(item.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(item);
    expect(item.style.transform).toBe("none");
  });
});
