import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ComparisonTable } from "@/features/landing/ComparisonTable";

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

  it("has an accessible caption for screen readers", () => {
    const { container } = render(<ComparisonTable />);
    const caption = container.querySelector("caption");
    expect(caption).toBeInTheDocument();
    expect(caption).toHaveClass("sr-only");
  });

  it("uses scope attributes on table headers", () => {
    const { container } = render(<ComparisonTable />);
    const colHeaders = container.querySelectorAll("th[scope='col']");
    expect(colHeaders.length).toBe(4);
    const rowHeaders = container.querySelectorAll("th[scope='row']");
    expect(rowHeaders.length).toBe(10);
  });

  it("renders the pricing rows", () => {
    render(<ComparisonTable />);
    expect(screen.getByText("$14.99/mo")).toBeInTheDocument();
    expect(screen.getByText("$499/mo")).toBeInTheDocument();
    expect(screen.getByText("$395/mo")).toBeInTheDocument();
  });

  it("renders feature comparison rows", () => {
    render(<ComparisonTable />);
    const rows = [
      "Website",
      "CMA Tool",
      "Lead Capture",
      "Multi-Language",
      "Legal Compliance",
      "SEO & AEO",
      "WhatsApp Alerts",
      "Setup Time",
    ];
    for (const row of rows) {
      expect(screen.getByText(row)).toBeInTheDocument();
    }
  });

  it("renders new competitive advantages", () => {
    render(<ComparisonTable />);
    expect(screen.getByText("12 templates")).toBeInTheDocument();
    expect(screen.getByText("English + Spanish")).toBeInTheDocument();
    expect(screen.getByText("Built-in")).toBeInTheDocument();
  });

  it("renders a table element", () => {
    const { container } = render(<ComparisonTable />);
    expect(container.querySelector("table")).toBeInTheDocument();
  });
});
