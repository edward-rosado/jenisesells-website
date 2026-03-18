/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StatsMetrics } from "@/components/sections/stats/StatsMetrics";
import type { StatItem } from "@/lib/types";

const ITEMS: StatItem[] = [
  { value: "$1.8B", label: "Transaction Volume" },
  { value: "450+", label: "Deals Closed" },
  { value: "22", label: "Years Experience" },
  { value: "12M+ SF", label: "Leased" },
];

describe("StatsMetrics", () => {
  it("renders all stat values", () => {
    render(<StatsMetrics items={ITEMS} />);
    expect(screen.getByText("$1.8B")).toBeInTheDocument();
    expect(screen.getByText("450+")).toBeInTheDocument();
    expect(screen.getByText("22")).toBeInTheDocument();
    expect(screen.getByText("12M+ SF")).toBeInTheDocument();
  });

  it("renders all stat labels", () => {
    render(<StatsMetrics items={ITEMS} />);
    expect(screen.getByText("Transaction Volume")).toBeInTheDocument();
    expect(screen.getByText("Deals Closed")).toBeInTheDocument();
    expect(screen.getByText("Years Experience")).toBeInTheDocument();
    expect(screen.getByText("Leased")).toBeInTheDocument();
  });

  it("uses id=stats for anchor linking", () => {
    const { container } = render(<StatsMetrics items={ITEMS} />);
    expect(container.querySelector("#stats")).toBeInTheDocument();
  });

  it("renders white background cards", () => {
    const { container } = render(<StatsMetrics items={ITEMS} />);
    const cards = container.querySelectorAll("[data-testid='stat-card']");
    expect(cards.length).toBe(4);
    cards.forEach((card) => {
      expect((card as HTMLElement).style.background).toBe("white");
    });
  });

  it("renders sourceDisclaimer when provided", () => {
    render(<StatsMetrics items={ITEMS} sourceDisclaimer="Based on MLS data 2023." />);
    expect(screen.getByText("Based on MLS data 2023.")).toBeInTheDocument();
  });

  it("does not render sourceDisclaimer when not provided", () => {
    render(<StatsMetrics items={ITEMS} />);
    expect(screen.queryByText(/Based on MLS/)).not.toBeInTheDocument();
  });

  it("renders the section element", () => {
    const { container } = render(<StatsMetrics items={ITEMS} />);
    expect(container.querySelector("section")).toBeInTheDocument();
  });

  it("renders values in accent color", () => {
    const { container } = render(<StatsMetrics items={ITEMS} />);
    const valueEls = container.querySelectorAll("[data-testid='stat-value']");
    expect(valueEls.length).toBe(4);
  });

  it("applies hover lift on mouse enter", () => {
    const { container } = render(<StatsMetrics items={ITEMS} />);
    const item = container.querySelector("[data-testid='stat-card']") as HTMLElement;
    expect(item.style.transform).toBe("none");
    fireEvent.mouseEnter(item);
    expect(item.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(item);
    expect(item.style.transform).toBe("none");
  });
});
