/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StepsJourney } from "@/components/sections/steps/StepsJourney";
import type { StepItem } from "@/lib/types";

const STEPS: StepItem[] = [
  { number: 1, title: "Share Your Story", description: "You'll tell us what matters most to you." },
  { number: 2, title: "Find Your Match", description: "We'll find homes that fit your life." },
  { number: 3, title: "Welcome Home", description: "You'll step into a place that feels right." },
];

describe("StepsJourney", () => {
  it("renders the default heading", () => {
    render(<StepsJourney steps={STEPS} />);
    expect(screen.getByRole("heading", { level: 2, name: "Your Journey Home" })).toBeInTheDocument();
  });

  it("renders a custom title when provided", () => {
    render(<StepsJourney steps={STEPS} title="How It Works" />);
    expect(screen.getByRole("heading", { level: 2, name: "How It Works" })).toBeInTheDocument();
  });

  it("renders all step titles", () => {
    render(<StepsJourney steps={STEPS} />);
    expect(screen.getByText("Share Your Story")).toBeInTheDocument();
    expect(screen.getByText("Find Your Match")).toBeInTheDocument();
    expect(screen.getByText("Welcome Home")).toBeInTheDocument();
  });

  it("renders step numbers", () => {
    render(<StepsJourney steps={STEPS} />);
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("renders step descriptions", () => {
    render(<StepsJourney steps={STEPS} />);
    expect(screen.getByText("You'll tell us what matters most to you.")).toBeInTheDocument();
    expect(screen.getByText("We'll find homes that fit your life.")).toBeInTheDocument();
    expect(screen.getByText("You'll step into a place that feels right.")).toBeInTheDocument();
  });

  it("uses id=steps for anchor linking", () => {
    const { container } = render(<StepsJourney steps={STEPS} />);
    expect(container.querySelector("#steps")).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<StepsJourney steps={STEPS} subtitle="Simple and warm" />);
    expect(screen.getByText("Simple and warm")).toBeInTheDocument();
  });

  it("applies hover lift on mouse enter", () => {
    render(<StepsJourney steps={STEPS} />);
    const item = screen.getAllByRole("listitem")[0];
    expect(item.style.transform).toBe("none");
    fireEvent.mouseEnter(item);
    expect(item.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(item);
    expect(item.style.transform).toBe("none");
  });
});
