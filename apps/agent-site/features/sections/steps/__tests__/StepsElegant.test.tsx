/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StepsElegant } from "@/features/sections/steps/StepsElegant";
import type { StepItem } from "@/features/config/types";

const STEPS: StepItem[] = [
  { number: 1, title: "Consultation", description: "We begin with a private consultation." },
  { number: 2, title: "Market Analysis", description: "Comprehensive analysis of your market." },
  { number: 3, title: "Strategic Execution", description: "Expert execution of your sale strategy." },
];

describe("StepsElegant", () => {
  it("renders section with id=steps", () => {
    const { container } = render(<StepsElegant steps={STEPS} />);
    expect(container.querySelector("section#steps")).toBeInTheDocument();
  });

  it("renders default heading 'How It Works' when title is not provided", () => {
    render(<StepsElegant steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 2, name: "How It Works" })).toBeInTheDocument();
  });

  it("renders custom heading when title is provided", () => {
    render(<StepsElegant steps={STEPS} title="Our Process" />);
    expect(screen.getByRole("heading", { level: 2, name: "Our Process" })).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<StepsElegant steps={STEPS} subtitle="A refined approach" />);
    expect(screen.getByText("A refined approach")).toBeInTheDocument();
  });

  it("renders all step numbers in circles", () => {
    render(<StepsElegant steps={STEPS} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("renders all step titles", () => {
    render(<StepsElegant steps={STEPS} />);
    expect(screen.getByText("Consultation")).toBeInTheDocument();
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Strategic Execution")).toBeInTheDocument();
  });

  it("renders all step descriptions", () => {
    render(<StepsElegant steps={STEPS} />);
    expect(screen.getByText("We begin with a private consultation.")).toBeInTheDocument();
    expect(screen.getByText("Comprehensive analysis of your market.")).toBeInTheDocument();
  });

  it("renders number circles with accent border", () => {
    const { container } = render(<StepsElegant steps={STEPS} />);
    // Number circles should have accent border styling
    const circles = container.querySelectorAll("[data-step-circle]");
    expect(circles.length).toBe(3);
    expect((circles[0] as HTMLElement).style.border).toContain("color-accent");
  });

  it("uses dark background on section", () => {
    const { container } = render(<StepsElegant steps={STEPS} />);
    const section = container.querySelector("section#steps");
    expect(section!.style.background).toContain("color-primary");
  });

  it("renders empty section gracefully when steps is empty", () => {
    render(<StepsElegant steps={[]} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("applies hover lift on mouse enter", () => {
    render(<StepsElegant steps={STEPS} />);
    // The hoverable div contains the step title text
    const stepTitle = screen.getByText("Consultation");
    const item = stepTitle.parentElement as HTMLElement;
    expect(item.style.transform).toBe("none");
    fireEvent.mouseEnter(item);
    expect(item.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(item);
    expect(item.style.transform).toBe("none");
  });
});
