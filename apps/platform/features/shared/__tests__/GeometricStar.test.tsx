import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { GeometricStar } from "@/features/shared/GeometricStar";

describe("GeometricStar", () => {
  it("renders an SVG element with correct dimensions", () => {
    const { container } = render(<GeometricStar size={32} />);
    const svg = container.querySelector("svg");
    expect(svg).toBeInTheDocument();
    expect(svg).toHaveAttribute("width", "32");
    expect(svg).toHaveAttribute("height", "32");
  });

  it("has accessible role img with label", () => {
    render(<GeometricStar size={24} />);
    expect(screen.getByRole("img", { hidden: true })).toBeInTheDocument();
    expect(screen.getByLabelText("Real Estate Star logo")).toBeInTheDocument();
  });

  it("renders two polygon elements (outer stroke + inner fill)", () => {
    const { container } = render(<GeometricStar size={24} />);
    const polygons = container.querySelectorAll("polygon");
    expect(polygons).toHaveLength(2);
    // Outer: stroke only
    expect(polygons[0]).toHaveAttribute("fill", "none");
    expect(polygons[0]).toHaveAttribute("stroke", "#10b981");
    // Inner: solid fill
    expect(polygons[1]).toHaveAttribute("fill", "#10b981");
  });

  it("applies spin animation when state is thinking", () => {
    const { container } = render(<GeometricStar size={24} state="thinking" />);
    const svg = container.querySelector("svg");
    expect(svg?.style.animation).toContain("star-spin");
    expect(svg?.style.animation).toContain("8s");
  });

  it("applies pulse animation when state is idle", () => {
    const { container } = render(<GeometricStar size={24} state="idle" />);
    const svg = container.querySelector("svg");
    expect(svg?.style.animation).toContain("star-pulse");
    expect(svg?.style.animation).toContain("2s");
  });

  it("has no animation when state is undefined", () => {
    const { container } = render(<GeometricStar size={24} />);
    const svg = container.querySelector("svg");
    expect(svg?.style.animation).toBeFalsy();
  });

  it("passes through className prop", () => {
    const { container } = render(<GeometricStar size={24} className="my-class" />);
    const svg = container.querySelector("svg");
    expect(svg).toHaveClass("my-class");
  });
});
