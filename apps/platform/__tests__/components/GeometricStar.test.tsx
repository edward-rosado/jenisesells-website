import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { GeometricStar } from "@/components/GeometricStar";

describe("GeometricStar", () => {
  it("renders an SVG element", () => {
    const { container } = render(<GeometricStar />);
    expect(container.querySelector("svg")).toBeInTheDocument();
  });

  it("applies custom className", () => {
    const { container } = render(<GeometricStar className="w-10 h-10" />);
    const svg = container.querySelector("svg");
    expect(svg).toHaveClass("w-10", "h-10");
  });

  it("has accessible role img", () => {
    render(<GeometricStar />);
    expect(screen.getByRole("img", { hidden: true })).toBeInTheDocument();
  });

  it("renders with default size classes when no className provided", () => {
    const { container } = render(<GeometricStar />);
    const svg = container.querySelector("svg");
    expect(svg).toHaveClass("w-8", "h-8");
  });
});
