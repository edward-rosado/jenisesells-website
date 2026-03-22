/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StatsWarm } from "@/components/sections/stats/StatsWarm";
import type { StatItem } from "@/features/config/types";

const ITEMS: StatItem[] = [
  { value: "500+", label: "Families Helped" },
  { value: "4.9★", label: "Average Rating" },
  { value: "12 Yrs", label: "Together" },
  { value: "$180M+", label: "Volume" },
];

describe("StatsWarm", () => {
  it("renders all stat values", () => {
    render(<StatsWarm items={ITEMS} />);
    expect(screen.getByText("500+")).toBeInTheDocument();
    expect(screen.getByText("4.9★")).toBeInTheDocument();
    expect(screen.getByText("12 Yrs")).toBeInTheDocument();
    expect(screen.getByText("$180M+")).toBeInTheDocument();
  });

  it("renders all stat labels", () => {
    render(<StatsWarm items={ITEMS} />);
    expect(screen.getByText("Families Helped")).toBeInTheDocument();
    expect(screen.getByText("Average Rating")).toBeInTheDocument();
    expect(screen.getByText("Together")).toBeInTheDocument();
    expect(screen.getByText("Volume")).toBeInTheDocument();
  });

  it("uses id=stats for anchor linking", () => {
    const { container } = render(<StatsWarm items={ITEMS} />);
    expect(container.querySelector("#stats")).toBeInTheDocument();
  });

  it("renders rounded cards with box shadows", () => {
    const { container } = render(<StatsWarm items={ITEMS} />);
    const cards = container.querySelectorAll("[style*='border-radius']");
    const withShadow = Array.from(cards).filter(
      (el) => (el as HTMLElement).style.boxShadow
    );
    expect(withShadow.length).toBeGreaterThan(0);
  });

  it("uses accent color for stat values", () => {
    const { container } = render(<StatsWarm items={ITEMS} />);
    const valueEls = container.querySelectorAll("dd");
    expect(valueEls.length).toBe(4);
    const firstStyle = (valueEls[0] as HTMLElement).style.color;
    // Should reference accent color
    expect(firstStyle).toBeTruthy();
  });

  it("renders sourceDisclaimer when provided", () => {
    render(<StatsWarm items={ITEMS} sourceDisclaimer="Based on MLS data." />);
    expect(screen.getByText("Based on MLS data.")).toBeInTheDocument();
  });

  it("does not render disclaimer when not provided", () => {
    render(<StatsWarm items={ITEMS} />);
    expect(screen.queryByText(/Based on/)).not.toBeInTheDocument();
  });

  it("uses a warm background color", () => {
    const { container } = render(<StatsWarm items={ITEMS} />);
    const section = container.querySelector("#stats") as HTMLElement;
    expect(section?.style.background).toBeTruthy();
  });

  it("lifts card on hover", () => {
    const { container } = render(<StatsWarm items={ITEMS} />);
    const card = container.querySelector("[style*='border-radius: 16px']") as HTMLElement;
    expect(card.style.transform).toBe("none");
    fireEvent.mouseEnter(card);
    expect(card.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(card);
    expect(card.style.transform).toBe("none");
  });
});
