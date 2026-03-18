/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StepsCards } from "@/components/sections/steps/StepsCards";
import type { StepItem } from "@/lib/types";

const STEPS: StepItem[] = [
  { number: 1, title: "Tell Me What You Need", description: "Quick 5-minute consultation." },
  { number: 2, title: "Get Your Report", description: "Custom market analysis delivered." },
  { number: 3, title: "Let's Do This", description: "We move fast and close deals." },
];

describe("StepsCards", () => {
  it("renders section with id=steps", () => {
    const { container } = render(<StepsCards steps={STEPS} />);
    expect(container.querySelector("section#steps")).toBeInTheDocument();
  });

  it("renders the default heading when no title provided", () => {
    render(<StepsCards steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("How It Works");
  });

  it("renders custom title when provided", () => {
    render(<StepsCards steps={STEPS} title="The Process" />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("The Process");
  });

  it("renders subtitle when provided", () => {
    render(<StepsCards steps={STEPS} subtitle="Simple and fast." />);
    expect(screen.getByText("Simple and fast.")).toBeInTheDocument();
  });

  it("renders all step titles", () => {
    render(<StepsCards steps={STEPS} />);
    expect(screen.getByText("Tell Me What You Need")).toBeInTheDocument();
    expect(screen.getByText("Get Your Report")).toBeInTheDocument();
    expect(screen.getByText("Let's Do This")).toBeInTheDocument();
  });

  it("renders all step descriptions", () => {
    render(<StepsCards steps={STEPS} />);
    expect(screen.getByText("Quick 5-minute consultation.")).toBeInTheDocument();
    expect(screen.getByText("Custom market analysis delivered.")).toBeInTheDocument();
    expect(screen.getByText("We move fast and close deals.")).toBeInTheDocument();
  });

  it("renders step numbers as watermarks", () => {
    const { container } = render(<StepsCards steps={STEPS} />);
    const watermarks = container.querySelectorAll("[data-step-watermark]");
    expect(watermarks.length).toBe(3);
    expect(watermarks[0].textContent).toBe("01");
    expect(watermarks[1].textContent).toBe("02");
    expect(watermarks[2].textContent).toBe("03");
  });

  it("watermarks are low opacity", () => {
    const { container } = render(<StepsCards steps={STEPS} />);
    const watermark = container.querySelector("[data-step-watermark]") as HTMLElement;
    const opacity = parseFloat(watermark.style.opacity);
    expect(opacity).toBeLessThan(0.2);
  });

  it("renders horizontal card layout (flex)", () => {
    const { container } = render(<StepsCards steps={STEPS} />);
    const row = container.querySelector("[data-steps-row]") as HTMLElement;
    expect(row).toBeInTheDocument();
    expect(row.style.display).toBe("flex");
  });

  it("renders empty section gracefully when steps is empty", () => {
    const { container } = render(<StepsCards steps={[]} />);
    expect(container.querySelector("section#steps")).toBeInTheDocument();
  });
});
