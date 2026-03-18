/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StatsCompact } from "@/components/sections/stats/StatsCompact";
import type { StatItem } from "@/lib/types";

const ITEMS: StatItem[] = [
  { value: "200+", label: "Deals Closed" },
  { value: "4.9★", label: "Average Rating" },
  { value: "6 Years", label: "Experience" },
  { value: "$35M+", label: "Total Volume" },
];

describe("StatsCompact", () => {
  it("renders section with id=stats", () => {
    const { container } = render(<StatsCompact items={ITEMS} />);
    expect(container.querySelector("section#stats")).toBeInTheDocument();
  });

  it("renders all stat values", () => {
    render(<StatsCompact items={ITEMS} />);
    expect(screen.getByText("200+")).toBeInTheDocument();
    expect(screen.getByText("4.9★")).toBeInTheDocument();
    expect(screen.getByText("6 Years")).toBeInTheDocument();
    expect(screen.getByText("$35M+")).toBeInTheDocument();
  });

  it("renders all stat labels", () => {
    render(<StatsCompact items={ITEMS} />);
    expect(screen.getByText("Deals Closed")).toBeInTheDocument();
    expect(screen.getByText("Average Rating")).toBeInTheDocument();
    expect(screen.getByText("Experience")).toBeInTheDocument();
    expect(screen.getByText("Total Volume")).toBeInTheDocument();
  });

  it("renders values with accent color CSS variable", () => {
    render(<StatsCompact items={ITEMS} />);
    const value = screen.getByText("200+");
    expect(value.style.color).toContain("color-accent");
  });

  it("renders pill items with dark background", () => {
    const { container } = render(<StatsCompact items={ITEMS} />);
    const pills = container.querySelectorAll("[data-stat-pill]");
    expect(pills.length).toBe(4);
    pills.forEach((pill) => {
      // jsdom normalizes #1a1a1a → rgb(26, 26, 26)
      const bg = (pill as HTMLElement).style.background;
      expect(bg === "#1a1a1a" || bg === "rgb(26, 26, 26)").toBe(true);
    });
  });

  it("renders pills with borderRadius (pill shape)", () => {
    const { container } = render(<StatsCompact items={ITEMS} />);
    const pill = container.querySelector("[data-stat-pill]") as HTMLElement;
    expect(pill.style.borderRadius).toBeTruthy();
  });

  it("renders horizontal flex layout", () => {
    const { container } = render(<StatsCompact items={ITEMS} />);
    const wrapper = container.querySelector("[data-stats-row]") as HTMLElement;
    expect(wrapper).toBeInTheDocument();
    expect(wrapper.style.display).toBe("flex");
  });

  it("renders sourceDisclaimer when provided", () => {
    render(<StatsCompact items={ITEMS} sourceDisclaimer="Based on MLS data." />);
    expect(screen.getByText("Based on MLS data.")).toBeInTheDocument();
  });

  it("does not render sourceDisclaimer when absent", () => {
    render(<StatsCompact items={ITEMS} />);
    expect(screen.queryByText("Based on MLS data.")).not.toBeInTheDocument();
  });

  it("renders empty section gracefully when items is empty", () => {
    const { container } = render(<StatsCompact items={[]} />);
    expect(container.querySelector("section#stats")).toBeInTheDocument();
  });

  it("applies hover lift on mouse enter", () => {
    const { container } = render(<StatsCompact items={ITEMS} />);
    const item = container.querySelector("[data-stat-pill]") as HTMLElement;
    expect(item.style.transform).toBe("none");
    fireEvent.mouseEnter(item);
    expect(item.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(item);
    expect(item.style.transform).toBe("none");
  });
});
