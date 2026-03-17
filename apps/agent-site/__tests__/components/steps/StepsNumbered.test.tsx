/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StepsNumbered } from "@/components/sections/steps/StepsNumbered";
import type { StepItem } from "@/lib/types";

const STEPS: StepItem[] = [
  { number: 1, title: "Submit Info", description: "Fill out the form below." },
  { number: 2, title: "Get Report", description: "Receive your CMA within minutes." },
  { number: 3, title: "Meet Agent", description: "Schedule a walkthrough." },
];

describe("StepsNumbered", () => {
  it("renders the section heading", () => {
    render(<StepsNumbered steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 2, name: "How It Works" })).toBeInTheDocument();
  });

  it("renders all step numbers", () => {
    render(<StepsNumbered steps={STEPS} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("renders all step titles", () => {
    render(<StepsNumbered steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 3, name: "Submit Info" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 3, name: "Get Report" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 3, name: "Meet Agent" })).toBeInTheDocument();
  });

  it("renders all step descriptions", () => {
    render(<StepsNumbered steps={STEPS} />);
    expect(screen.getByText("Fill out the form below.")).toBeInTheDocument();
    expect(screen.getByText("Receive your CMA within minutes.")).toBeInTheDocument();
    expect(screen.getByText("Schedule a walkthrough.")).toBeInTheDocument();
  });

  it("renders with empty steps array (just heading)", () => {
    render(<StepsNumbered steps={[]} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
    expect(screen.queryAllByRole("heading", { level: 3 })).toHaveLength(0);
  });

  it("renders a single step correctly", () => {
    render(<StepsNumbered steps={[{ number: 42, title: "Only Step", description: "Do this" }]} />);
    expect(screen.getByText("42")).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 3, name: "Only Step" })).toBeInTheDocument();
    expect(screen.getByText("Do this")).toBeInTheDocument();
  });

  it("renders correct number of step cards", () => {
    render(<StepsNumbered steps={STEPS} />);
    expect(screen.getAllByRole("heading", { level: 3 })).toHaveLength(3);
  });

  it("renders subtitle paragraph when subtitle is provided", () => {
    render(<StepsNumbered steps={STEPS} subtitle="Three simple steps to sell your home" />);
    expect(screen.getByText("Three simple steps to sell your home")).toBeInTheDocument();
  });

  it("does not render subtitle paragraph when subtitle is absent", () => {
    render(<StepsNumbered steps={STEPS} />);
    // No subtitle — the only paragraphs are step descriptions
    expect(screen.queryByText("Three simple steps to sell your home")).not.toBeInTheDocument();
  });
});
