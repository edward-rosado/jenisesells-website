/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatsOverlay } from "@/components/sections/stats/StatsOverlay";
import type { StatItem } from "@/lib/types";

const ITEMS: StatItem[] = [
  { value: "$2.1B", label: "Total Volume" },
  { value: "340+", label: "Properties" },
  { value: "#1", label: "NYC Luxury Agent" },
  { value: "18 Years", label: "Experience" },
];

describe("StatsOverlay", () => {
  it("renders section with id=stats", () => {
    const { container } = render(<StatsOverlay items={ITEMS} />);
    const section = container.querySelector("section#stats");
    expect(section).toBeInTheDocument();
  });

  it("renders all stat values", () => {
    render(<StatsOverlay items={ITEMS} />);
    expect(screen.getByText("$2.1B")).toBeInTheDocument();
    expect(screen.getByText("340+")).toBeInTheDocument();
    expect(screen.getByText("#1")).toBeInTheDocument();
    expect(screen.getByText("18 Years")).toBeInTheDocument();
  });

  it("renders all stat labels", () => {
    render(<StatsOverlay items={ITEMS} />);
    expect(screen.getByText("Total Volume")).toBeInTheDocument();
    expect(screen.getByText("Properties")).toBeInTheDocument();
    expect(screen.getByText("NYC Luxury Agent")).toBeInTheDocument();
    expect(screen.getByText("Experience")).toBeInTheDocument();
  });

  it("renders values with accent color CSS variable", () => {
    render(<StatsOverlay items={ITEMS} />);
    const value = screen.getByText("$2.1B");
    expect(value.style.color).toContain("color-accent");
  });

  it("renders labels with uppercase text transform", () => {
    render(<StatsOverlay items={ITEMS} />);
    const label = screen.getByText("Total Volume");
    expect(label.style.textTransform).toBe("uppercase");
  });

  it("renders dark semi-transparent background", () => {
    const { container } = render(<StatsOverlay items={ITEMS} />);
    const section = container.querySelector("section#stats");
    expect(section!.style.background).toContain("rgba");
  });

  it("renders sourceDisclaimer when provided", () => {
    render(<StatsOverlay items={ITEMS} sourceDisclaimer="Based on MLS data." />);
    expect(screen.getByText("Based on MLS data.")).toBeInTheDocument();
  });

  it("does not render sourceDisclaimer when absent", () => {
    render(<StatsOverlay items={ITEMS} />);
    expect(screen.queryByText("Based on MLS data.")).not.toBeInTheDocument();
  });

  it("renders empty section gracefully when items is empty", () => {
    const { container } = render(<StatsOverlay items={[]} />);
    expect(container.querySelector("section#stats")).toBeInTheDocument();
  });
});
