/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StepsPath } from "@/components/sections/steps/StepsPath";
import type { StepItem } from "@/lib/types";

const STEPS: StepItem[] = [
  { number: 1, title: "Consultation", description: "Meet with James to discuss your goals and budget." },
  { number: 2, title: "Property Tour", description: "Explore estates hand-picked for your criteria." },
  { number: 3, title: "Closing", description: "We handle the details so you can focus on moving in." },
];

describe("StepsPath", () => {
  it("renders the section heading with default title", () => {
    render(<StepsPath steps={STEPS} />);
    expect(
      screen.getByRole("heading", { level: 2, name: "Your Path Home" })
    ).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<StepsPath steps={STEPS} title="The Estate Journey" />);
    expect(
      screen.getByRole("heading", { level: 2, name: "The Estate Journey" })
    ).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<StepsPath steps={STEPS} subtitle="Three steps to your new estate." />);
    expect(screen.getByText("Three steps to your new estate.")).toBeInTheDocument();
  });

  it("renders all step titles", () => {
    render(<StepsPath steps={STEPS} />);
    expect(screen.getByText("Consultation")).toBeInTheDocument();
    expect(screen.getByText("Property Tour")).toBeInTheDocument();
    expect(screen.getByText("Closing")).toBeInTheDocument();
  });

  it("renders all step descriptions", () => {
    render(<StepsPath steps={STEPS} />);
    expect(
      screen.getByText("Meet with James to discuss your goals and budget.")
    ).toBeInTheDocument();
    expect(
      screen.getByText("We handle the details so you can focus on moving in.")
    ).toBeInTheDocument();
  });

  it("renders all step numbers", () => {
    render(<StepsPath steps={STEPS} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("renders section with id=how-it-works", () => {
    const { container } = render(<StepsPath steps={STEPS} />);
    expect(container.querySelector("#how-it-works")).toBeInTheDocument();
  });

  it("renders a dotted/dashed connecting line between steps", () => {
    const { container } = render(<StepsPath steps={STEPS} />);
    // The connecting line element should have dashed border style
    const connectingLines = container.querySelectorAll("[aria-hidden='true']");
    const hasDashed = Array.from(connectingLines).some(
      (el) =>
        (el as HTMLElement).style.borderLeft?.includes("dashed") ||
        (el as HTMLElement).style.borderStyle === "dashed"
    );
    expect(hasDashed).toBe(true);
  });

  it("renders green trail marker dots", () => {
    const { container } = render(<StepsPath steps={STEPS} />);
    // Each step number circle should use accent/primary color
    const stepDots = container.querySelectorAll("[aria-hidden='true']");
    // At least one dot should reference green color
    const hasGreenMarker = Array.from(stepDots).some(
      (el) =>
        (el as HTMLElement).style.background?.includes("var(--color-accent") ||
        (el as HTMLElement).style.background?.includes("var(--color-primary") ||
        (el as HTMLElement).style.background?.includes("#4a6741") ||
        (el as HTMLElement).style.background?.includes("#2d4a3e")
    );
    expect(hasGreenMarker).toBe(true);
  });
});
