/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StepsTimeline } from "@/components/sections/steps/StepsTimeline";

const STEPS = [
  { number: 1, title: "Submit Info", description: "Fill out the form" },
  { number: 2, title: "Get Report", description: "Receive your CMA" },
  { number: 3, title: "Meet Agent", description: "Schedule walkthrough" },
];

describe("StepsTimeline", () => {
  it("renders the section heading with default title", () => {
    render(<StepsTimeline steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<StepsTimeline steps={STEPS} title="The Process" />);
    expect(screen.getByRole("heading", { level: 2, name: "The Process" })).toBeInTheDocument();
  });

  it("renders all step titles", () => {
    render(<StepsTimeline steps={STEPS} />);
    expect(screen.getByText("Submit Info")).toBeInTheDocument();
    expect(screen.getByText("Get Report")).toBeInTheDocument();
    expect(screen.getByText("Meet Agent")).toBeInTheDocument();
  });

  it("renders all step descriptions", () => {
    render(<StepsTimeline steps={STEPS} />);
    expect(screen.getByText("Fill out the form")).toBeInTheDocument();
    expect(screen.getByText("Receive your CMA")).toBeInTheDocument();
    expect(screen.getByText("Schedule walkthrough")).toBeInTheDocument();
  });

  it("has how-it-works section id for anchor linking", () => {
    const { container } = render(<StepsTimeline steps={STEPS} />);
    expect(container.querySelector("#how-it-works")).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<StepsTimeline steps={STEPS} subtitle="Simple 3-step process" />);
    expect(screen.getByText("Simple 3-step process")).toBeInTheDocument();
  });

  it("step items are focusable via tabIndex", () => {
    const { container } = render(<StepsTimeline steps={STEPS} />);
    const items = container.querySelectorAll("li");
    items.forEach((item) => {
      expect(item.tabIndex).toBe(0);
    });
  });

  it("step highlights on hover with background and shift", () => {
    const { container } = render(<StepsTimeline steps={STEPS} />);
    const item = container.querySelector("li")!;
    expect(item.style.background).toBe("transparent");
    fireEvent.mouseEnter(item);
    expect(item.style.background).toMatch(/#f5f5f5|rgb\(245, 245, 245\)/);
    expect(item.style.transform).toBe("translateX(4px)");
    fireEvent.mouseLeave(item);
    expect(item.style.background).toBe("transparent");
  });

  it("step highlights on focus", () => {
    const { container } = render(<StepsTimeline steps={STEPS} />);
    const item = container.querySelector("li")!;
    fireEvent.focus(item);
    expect(item.style.background).toMatch(/#f5f5f5|rgb\(245, 245, 245\)/);
    fireEvent.blur(item);
    expect(item.style.background).toBe("transparent");
  });
});
