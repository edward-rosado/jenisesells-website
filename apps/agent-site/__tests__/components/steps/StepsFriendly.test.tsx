/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StepsFriendly } from "@/components/sections/steps/StepsFriendly";
import type { StepItem } from "@/lib/types";

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

  it("uses id=how-it-works for anchor linking", () => {
    const { container } = render(<StepsFriendly steps={STEPS} />);
    expect(container.querySelector("#how-it-works")).toBeInTheDocument();
  });

  it("renders step numbers", () => {
    render(<StepsFriendly steps={STEPS} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("uses warm soft rounded cards", () => {
    const { container } = render(<StepsFriendly steps={STEPS} />);
    const section = container.querySelector("#how-it-works");
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

  it("has role=list on the ordered list", () => {
    const { container } = render(<StepsFriendly steps={STEPS} />);
    const ol = container.querySelector("ol");
    expect(ol).toHaveAttribute("role", "list");
  });

  it("hides step number from assistive technology", () => {
    const { container } = render(<StepsFriendly steps={STEPS} />);
    const hiddenNumbers = container.querySelectorAll("[aria-hidden='true']");
    expect(hiddenNumbers.length).toBeGreaterThanOrEqual(STEPS.length);
  });
});
