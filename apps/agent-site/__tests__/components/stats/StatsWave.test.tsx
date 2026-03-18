/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StatsWave } from "@/components/sections/stats/StatsWave";
import type { StatItem } from "@/lib/types";

const ITEMS: StatItem[] = [
  { value: "300+", label: "Beach Homes Sold" },
  { value: "4.9★", label: "Rating" },
  { value: "15 Years", label: "Experience" },
  { value: "$250M+", label: "Volume" },
];

describe("StatsWave", () => {
  it("renders all stat values", () => {
    render(<StatsWave items={ITEMS} />);
    expect(screen.getByText("300+")).toBeInTheDocument();
    expect(screen.getByText("4.9★")).toBeInTheDocument();
    expect(screen.getByText("15 Years")).toBeInTheDocument();
    expect(screen.getByText("$250M+")).toBeInTheDocument();
  });

  it("renders all stat labels", () => {
    render(<StatsWave items={ITEMS} />);
    expect(screen.getByText("Beach Homes Sold")).toBeInTheDocument();
    expect(screen.getByText("Rating")).toBeInTheDocument();
    expect(screen.getByText("Experience")).toBeInTheDocument();
    expect(screen.getByText("Volume")).toBeInTheDocument();
  });

  it("has id=stats for anchor linking", () => {
    const { container } = render(<StatsWave items={ITEMS} />);
    expect(container.querySelector("#stats")).toBeInTheDocument();
  });

  it("has teal background", () => {
    const { container } = render(<StatsWave items={ITEMS} />);
    const section = container.querySelector("section");
    expect(section!.style.background).toMatch(/var\(--color-primary|#2c7a7b/);
  });

  it("renders white text on the container", () => {
    const { container } = render(<StatsWave items={ITEMS} />);
    const section = container.querySelector("section");
    expect(section!.style.color).toMatch(/white|#fff|rgb\(255, 255, 255\)/i);
  });

  it("renders sourceDisclaimer when provided", () => {
    render(<StatsWave items={ITEMS} sourceDisclaimer="Based on MLS data." />);
    expect(screen.getByText("Based on MLS data.")).toBeInTheDocument();
  });

  it("does not render sourceDisclaimer when not provided", () => {
    const { container } = render(<StatsWave items={ITEMS} />);
    // No disclaimer paragraph beyond stat labels
    expect(container.querySelectorAll("p").length).toBe(0);
  });

  it("renders items in a horizontal flex layout", () => {
    const { container } = render(<StatsWave items={ITEMS} />);
    const dl = container.querySelector("dl");
    expect(dl).toBeInTheDocument();
    expect(dl!.style.display).toBe("flex");
  });

  it("applies hover lift on mouse enter", () => {
    const { container } = render(<StatsWave items={ITEMS} />);
    const item = container.querySelector("dl")!.children[0] as HTMLElement;
    expect(item.style.transform).toBe("none");
    fireEvent.mouseEnter(item);
    expect(item.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(item);
    expect(item.style.transform).toBe("none");
  });
});
