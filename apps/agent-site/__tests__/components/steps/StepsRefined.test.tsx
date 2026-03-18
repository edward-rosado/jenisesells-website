/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StepsRefined } from "@/components/sections/steps/StepsRefined";
import type { StepItem } from "@/lib/types";

const STEPS: StepItem[] = [
  { number: 1, title: "Consultation", description: "We begin with a private consultation to understand your goals." },
  { number: 2, title: "Property Strategy", description: "We develop a tailored strategy for your property." },
  { number: 3, title: "Seamless Closing", description: "We guide you through every detail to a graceful close." },
];

describe("StepsRefined", () => {
  it("renders section with id=steps", () => {
    const { container } = render(<StepsRefined steps={STEPS} />);
    expect(container.querySelector("section#steps")).toBeInTheDocument();
  });

  it("renders default heading 'The Process'", () => {
    render(<StepsRefined steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 2, name: "The Process" })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<StepsRefined steps={STEPS} title="Our Approach" />);
    expect(screen.getByRole("heading", { level: 2, name: "Our Approach" })).toBeInTheDocument();
  });

  it("renders all step titles", () => {
    render(<StepsRefined steps={STEPS} />);
    expect(screen.getByText("Consultation")).toBeInTheDocument();
    expect(screen.getByText("Property Strategy")).toBeInTheDocument();
    expect(screen.getByText("Seamless Closing")).toBeInTheDocument();
  });

  it("renders all step numbers", () => {
    render(<StepsRefined steps={STEPS} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("renders all step descriptions", () => {
    render(<StepsRefined steps={STEPS} />);
    expect(screen.getByText(/We begin with a private consultation/)).toBeInTheDocument();
    expect(screen.getByText(/We develop a tailored strategy/)).toBeInTheDocument();
    expect(screen.getByText(/We guide you through every detail/)).toBeInTheDocument();
  });

  it("renders number circles with accent border and color", () => {
    const { container } = render(<StepsRefined steps={STEPS} />);
    const circles = container.querySelectorAll("[data-step-circle]");
    expect(circles.length).toBe(3);
    expect((circles[0] as HTMLElement).style.border).toContain("color-accent");
    expect((circles[0] as HTMLElement).style.color).toContain("color-accent");
  });

  it("renders connecting lines between steps", () => {
    const { container } = render(<StepsRefined steps={STEPS} />);
    const lines = container.querySelectorAll("[data-step-line]");
    // 3 steps = 2 connecting lines
    expect(lines.length).toBe(2);
  });

  it("renders subtitle when provided", () => {
    render(<StepsRefined steps={STEPS} subtitle="A refined approach to real estate." />);
    expect(screen.getByText("A refined approach to real estate.")).toBeInTheDocument();
  });
});
