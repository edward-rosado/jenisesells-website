/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StepsCorporate } from "@/components/sections/steps/StepsCorporate";
import type { StepItem } from "@/features/config/types";

const STEPS: StepItem[] = [
  { number: 1, title: "Initial Assessment", description: "Review your portfolio objectives." },
  { number: 2, title: "Market Analysis", description: "Deep dive into comparable transactions." },
  { number: 3, title: "Transaction Execution", description: "Close with confidence." },
];

describe("StepsCorporate", () => {
  it("renders the default heading", () => {
    render(<StepsCorporate steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 2, name: "Our Process" })).toBeInTheDocument();
  });

  it("renders a custom heading when title is provided", () => {
    render(<StepsCorporate steps={STEPS} title="How We Work" />);
    expect(screen.getByRole("heading", { level: 2, name: "How We Work" })).toBeInTheDocument();
  });

  it("renders all step titles", () => {
    render(<StepsCorporate steps={STEPS} />);
    expect(screen.getByText("Initial Assessment")).toBeInTheDocument();
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Transaction Execution")).toBeInTheDocument();
  });

  it("renders all step numbers", () => {
    render(<StepsCorporate steps={STEPS} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("renders all step descriptions", () => {
    render(<StepsCorporate steps={STEPS} />);
    expect(screen.getByText("Review your portfolio objectives.")).toBeInTheDocument();
    expect(screen.getByText("Deep dive into comparable transactions.")).toBeInTheDocument();
    expect(screen.getByText("Close with confidence.")).toBeInTheDocument();
  });

  it("uses id=steps for anchor linking", () => {
    const { container } = render(<StepsCorporate steps={STEPS} />);
    expect(container.querySelector("#steps")).toBeInTheDocument();
  });

  it("renders blue number circles", () => {
    const { container } = render(<StepsCorporate steps={STEPS} />);
    const circles = container.querySelectorAll("[data-testid='step-number']");
    expect(circles.length).toBe(3);
    circles.forEach((circle) => {
      const bg = (circle as HTMLElement).style.background;
      // JSDOM normalises hex → rgb; accept either form
      expect(bg).toMatch(/#2563eb|rgb\(37,\s*99,\s*235\)/);
    });
  });

  it("renders subtitle when provided", () => {
    render(<StepsCorporate steps={STEPS} subtitle="A proven three-step process." />);
    expect(screen.getByText("A proven three-step process.")).toBeInTheDocument();
  });

  it("applies hover lift on mouse enter", () => {
    render(<StepsCorporate steps={STEPS} />);
    const item = screen.getAllByRole("listitem")[0];
    expect(item.style.transform).toBe("none");
    fireEvent.mouseEnter(item);
    expect(item.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(item);
    expect(item.style.transform).toBe("none");
  });
});
