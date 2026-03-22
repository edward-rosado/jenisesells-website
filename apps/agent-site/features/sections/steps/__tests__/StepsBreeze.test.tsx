/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StepsBreeze } from "@/features/sections/steps/StepsBreeze";
import type { StepItem } from "@/features/config/types";

const STEPS: StepItem[] = [
  { number: 1, title: "Share Your Vision", description: "Tell us about your dream coastal home." },
  { number: 2, title: "Explore Properties", description: "We'll show you the best matches." },
  { number: 3, title: "Close & Celebrate", description: "Sign the papers and pop the champagne." },
];

describe("StepsBreeze", () => {
  it("renders the default heading 'How It Works'", () => {
    render(<StepsBreeze steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 2, name: "How It Works" })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<StepsBreeze steps={STEPS} title="Your Journey" />);
    expect(screen.getByRole("heading", { level: 2, name: "Your Journey" })).toBeInTheDocument();
  });

  it("renders all step titles", () => {
    render(<StepsBreeze steps={STEPS} />);
    expect(screen.getByText("Share Your Vision")).toBeInTheDocument();
    expect(screen.getByText("Explore Properties")).toBeInTheDocument();
    expect(screen.getByText("Close & Celebrate")).toBeInTheDocument();
  });

  it("renders all step numbers", () => {
    render(<StepsBreeze steps={STEPS} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("renders all step descriptions", () => {
    render(<StepsBreeze steps={STEPS} />);
    expect(screen.getByText("Tell us about your dream coastal home.")).toBeInTheDocument();
    expect(screen.getByText("We'll show you the best matches.")).toBeInTheDocument();
    expect(screen.getByText("Sign the papers and pop the champagne.")).toBeInTheDocument();
  });

  it("has id=steps for anchor linking", () => {
    const { container } = render(<StepsBreeze steps={STEPS} />);
    expect(container.querySelector("#steps")).toBeInTheDocument();
  });

  it("step number circles have teal background", () => {
    const { container } = render(<StepsBreeze steps={STEPS} />);
    // The number circle divs have borderRadius: 50% and display: flex
    const circles = Array.from(container.querySelectorAll("[aria-hidden='true']")).filter(
      (el) => (el as HTMLElement).style.borderRadius === "50%"
    );
    expect(circles.length).toBeGreaterThan(0);
    expect((circles[0] as HTMLElement).style.background).toMatch(/var\(--color-primary|#2c7a7b/);
  });

  it("step number circles are circular (borderRadius 50%)", () => {
    const { container } = render(<StepsBreeze steps={STEPS} />);
    const circles = Array.from(container.querySelectorAll("[aria-hidden='true']")).filter(
      (el) => (el as HTMLElement).style.borderRadius === "50%"
    );
    expect(circles.length).toBeGreaterThan(0);
    expect((circles[0] as HTMLElement).style.borderRadius).toBe("50%");
  });

  it("renders subtitle when provided", () => {
    render(<StepsBreeze steps={STEPS} subtitle="Simple and stress-free." />);
    expect(screen.getByText("Simple and stress-free.")).toBeInTheDocument();
  });

  it("renders steps in a horizontal list", () => {
    const { container } = render(<StepsBreeze steps={STEPS} />);
    const ol = container.querySelector("ol");
    expect(ol).toBeInTheDocument();
    expect(ol!.style.display).toBe("flex");
  });

  it("applies hover lift on mouse enter", () => {
    render(<StepsBreeze steps={STEPS} />);
    const item = screen.getAllByRole("listitem")[0];
    expect(item.style.transform).toBe("none");
    fireEvent.mouseEnter(item);
    expect(item.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(item);
    expect(item.style.transform).toBe("none");
  });
});
