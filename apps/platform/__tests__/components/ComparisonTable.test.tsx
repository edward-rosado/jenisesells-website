import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ComparisonTable } from "@/components/landing/ComparisonTable";

describe("ComparisonTable", () => {
  it("renders a section heading", () => {
    render(<ComparisonTable />);
    expect(
      screen.getByRole("heading", { name: /why agents switch/i })
    ).toBeInTheDocument();
  });

  it("renders column headers for competitors", () => {
    render(<ComparisonTable />);
    expect(screen.getByText("Real Estate Star")).toBeInTheDocument();
    expect(screen.getByText("KVCore")).toBeInTheDocument();
    expect(screen.getByText("Ylopo")).toBeInTheDocument();
  });

  it("renders the price row", () => {
    render(<ComparisonTable />);
    expect(screen.getByText("$900 one-time")).toBeInTheDocument();
    expect(screen.getByText("$499/mo")).toBeInTheDocument();
    expect(screen.getByText("$395/mo")).toBeInTheDocument();
  });

  it("renders feature comparison rows", () => {
    render(<ComparisonTable />);
    const rows = ["Website", "CMA Tool", "Lead Capture", "Setup Time"];
    for (const row of rows) {
      expect(screen.getByText(row)).toBeInTheDocument();
    }
  });

  it("renders a table element", () => {
    const { container } = render(<ComparisonTable />);
    expect(container.querySelector("table")).toBeInTheDocument();
  });
});
