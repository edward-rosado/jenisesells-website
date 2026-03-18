/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StatsRugged } from "@/components/sections/stats/StatsRugged";
import type { StatItem } from "@/lib/types";

const STATS: StatItem[] = [
  { value: "1,200+", label: "Acres Sold" },
  { value: "85", label: "Homes Closed" },
  { value: "20 Years", label: "Experience" },
  { value: "$320M+", label: "Volume" },
];

describe("StatsRugged", () => {
  it("renders all stat values", () => {
    render(<StatsRugged items={STATS} />);
    expect(screen.getByText("1,200+")).toBeInTheDocument();
    expect(screen.getByText("85")).toBeInTheDocument();
    expect(screen.getByText("20 Years")).toBeInTheDocument();
    expect(screen.getByText("$320M+")).toBeInTheDocument();
  });

  it("renders all stat labels", () => {
    render(<StatsRugged items={STATS} />);
    expect(screen.getByText("Acres Sold")).toBeInTheDocument();
    expect(screen.getByText("Homes Closed")).toBeInTheDocument();
    expect(screen.getByText("Experience")).toBeInTheDocument();
    expect(screen.getByText("Volume")).toBeInTheDocument();
  });

  it("renders section with id=stats", () => {
    const { container } = render(<StatsRugged items={STATS} />);
    expect(container.querySelector("#stats")).toBeInTheDocument();
  });

  it("renders dark green background", () => {
    const { container } = render(<StatsRugged items={STATS} />);
    const section = container.querySelector("section");
    expect(section?.style.background).toContain("var(--color-primary");
  });

  it("renders source disclaimer when provided", () => {
    render(
      <StatsRugged
        items={STATS}
        sourceDisclaimer="Based on MLS data. Results may vary."
      />
    );
    expect(
      screen.getByText("Based on MLS data. Results may vary.")
    ).toBeInTheDocument();
  });

  it("does not render disclaimer when not provided", () => {
    const { container } = render(<StatsRugged items={STATS} />);
    // No extra paragraph outside the dl
    const section = container.querySelector("section");
    const paras = section?.querySelectorAll("p");
    expect(paras?.length ?? 0).toBe(0);
  });

  it("renders a dl element for stats", () => {
    const { container } = render(<StatsRugged items={STATS} />);
    expect(container.querySelector("dl")).toBeInTheDocument();
  });

  it("renders horizontal flex layout", () => {
    const { container } = render(<StatsRugged items={STATS} />);
    const dl = container.querySelector("dl");
    expect(dl?.style.display).toBe("flex");
  });

  it("renders correct number of stat items", () => {
    const { container } = render(<StatsRugged items={STATS} />);
    const dl = container.querySelector("dl");
    expect(dl?.children.length).toBe(4);
  });

  it("applies hover lift on mouse enter", () => {
    const { container } = render(<StatsRugged items={STATS} />);
    const item = container.querySelector("dl")!.children[0] as HTMLElement;
    expect(item.style.transform).toBe("none");
    fireEvent.mouseEnter(item);
    expect(item.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(item);
    expect(item.style.transform).toBe("none");
  });
});
