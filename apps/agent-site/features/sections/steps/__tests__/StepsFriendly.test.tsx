/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StepsFriendly } from "@/features/sections/steps/StepsFriendly";
import type { StepItem } from "@/features/config/types";

const STEPS: StepItem[] = [
  { number: 1, title: "Submit Info", description: "Fill out a simple form" },
  { number: 2, title: "Get Analysis", description: "We run the numbers" },
  { number: 3, title: "Review Report", description: "See your CMA report" },
];

describe("StepsFriendly", () => {
  it("renders the section heading with default title", () => {
    render(<StepsFriendly steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<StepsFriendly steps={STEPS} title="Easy Steps" />);
    expect(screen.getByRole("heading", { level: 2, name: "Easy Steps" })).toBeInTheDocument();
  });

  it("renders all step titles", () => {
    render(<StepsFriendly steps={STEPS} />);
    expect(screen.getByText("Submit Info")).toBeInTheDocument();
    expect(screen.getByText("Get Analysis")).toBeInTheDocument();
    expect(screen.getByText("Review Report")).toBeInTheDocument();
  });

  it("renders step descriptions", () => {
    render(<StepsFriendly steps={STEPS} />);
    expect(screen.getByText("Fill out a simple form")).toBeInTheDocument();
    expect(screen.getByText("We run the numbers")).toBeInTheDocument();
  });

  it("uses id=steps for anchor linking", () => {
    const { container } = render(<StepsFriendly steps={STEPS} />);
    expect(container.querySelector("#steps")).toBeInTheDocument();
  });

  it("renders step numbers", () => {
    render(<StepsFriendly steps={STEPS} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("uses warm soft rounded cards", () => {
    const { container } = render(<StepsFriendly steps={STEPS} />);
    const section = container.querySelector("#steps");
    expect(section?.style.background).toBe("rgb(255, 248, 240)");
  });

  it("renders subtitle when provided", () => {
    render(<StepsFriendly steps={STEPS} subtitle="Simple and easy" />);
    expect(screen.getByText("Simple and easy")).toBeInTheDocument();
  });

  it("uses semantic ordered list for steps", () => {
    const { container } = render(<StepsFriendly steps={STEPS} />);
    const ol = container.querySelector("ol");
    expect(ol).toBeInTheDocument();
    const items = ol?.querySelectorAll("li");
    expect(items?.length).toBe(3);
  });

  it("lifts step card on hover", () => {
    const { container } = render(<StepsFriendly steps={STEPS} />);
    const li = container.querySelector("li") as HTMLElement;
    fireEvent.mouseEnter(li);
    expect(li.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(li);
    expect(li.style.transform).toBe("none");
  });
});
