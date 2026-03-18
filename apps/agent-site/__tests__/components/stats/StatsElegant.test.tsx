/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StatsElegant } from "@/components/sections/stats/StatsElegant";
import type { StatItem } from "@/lib/types";

const ITEMS: StatItem[] = [
  { value: "$850M+", label: "Career Volume" },
  { value: "200+", label: "Homes Sold" },
  { value: "25 Years", label: "Experience" },
  { value: "Top 1%", label: "CT Agents" },
];

describe("StatsElegant", () => {
  it("renders section with id=stats", () => {
    const { container } = render(<StatsElegant items={ITEMS} />);
    expect(container.querySelector("section#stats")).toBeInTheDocument();
  });

  it("renders all stat values", () => {
    render(<StatsElegant items={ITEMS} />);
    expect(screen.getByText("$850M+")).toBeInTheDocument();
    expect(screen.getByText("200+")).toBeInTheDocument();
    expect(screen.getByText("25 Years")).toBeInTheDocument();
    expect(screen.getByText("Top 1%")).toBeInTheDocument();
  });

  it("renders all stat labels", () => {
    render(<StatsElegant items={ITEMS} />);
    expect(screen.getByText("Career Volume")).toBeInTheDocument();
    expect(screen.getByText("Homes Sold")).toBeInTheDocument();
    expect(screen.getByText("Experience")).toBeInTheDocument();
    expect(screen.getByText("CT Agents")).toBeInTheDocument();
  });

  it("renders values with accent color CSS variable", () => {
    render(<StatsElegant items={ITEMS} />);
    const value = screen.getByText("$850M+");
    expect(value.style.color).toContain("color-accent");
  });

  it("renders separating vertical lines between stat items", () => {
    const { container } = render(<StatsElegant items={ITEMS} />);
    const separators = container.querySelectorAll("[data-separator]");
    // 4 items = 3 separators
    expect(separators.length).toBe(3);
  });

  it("renders sourceDisclaimer when provided", () => {
    render(<StatsElegant items={ITEMS} sourceDisclaimer="Based on MLS data." />);
    expect(screen.getByText("Based on MLS data.")).toBeInTheDocument();
  });

  it("does not render sourceDisclaimer when absent", () => {
    render(<StatsElegant items={ITEMS} />);
    expect(screen.queryByText("Based on MLS data.")).not.toBeInTheDocument();
  });

  it("renders empty section gracefully when items is empty", () => {
    const { container } = render(<StatsElegant items={[]} />);
    expect(container.querySelector("section#stats")).toBeInTheDocument();
  });

  it("stat values use serif font family", () => {
    render(<StatsElegant items={ITEMS} />);
    const value = screen.getByText("$850M+");
    expect(value.style.fontFamily).toContain("font-family");
  });

  it("applies hover lift on mouse enter", () => {
    const { container } = render(<StatsElegant items={ITEMS} />);
    // The hoverable div is the inner child of the outer wrapper div in the dl
    const item = container.querySelector("dl")!.children[0].children[0] as HTMLElement;
    expect(item.style.transform).toBe("none");
    fireEvent.mouseEnter(item);
    expect(item.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(item);
    expect(item.style.transform).toBe("none");
  });
});
