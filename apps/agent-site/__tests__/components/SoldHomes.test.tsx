/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { SoldHomes } from "@/components/sections/SoldHomes";
import type { SoldHomeItem } from "@/lib/types";

const ITEMS: SoldHomeItem[] = [
  { address: "123 Main St", city: "Hoboken", state: "NJ", price: "$750,000" },
  { address: "456 Elm Ave", city: "Jersey City", state: "NJ", price: "$620,000", sold_date: "2024-01-15" },
];

describe("SoldHomes", () => {
  it("renders the section heading", () => {
    render(<SoldHomes items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "Recently Sold" })).toBeInTheDocument();
  });

  it("renders all prices", () => {
    render(<SoldHomes items={ITEMS} />);
    expect(screen.getByText("$750,000")).toBeInTheDocument();
    expect(screen.getByText("$620,000")).toBeInTheDocument();
  });

  it("renders address, city and state for each item", () => {
    render(<SoldHomes items={ITEMS} />);
    expect(screen.getByText("123 Main St, Hoboken, NJ")).toBeInTheDocument();
    expect(screen.getByText("456 Elm Ave, Jersey City, NJ")).toBeInTheDocument();
  });

  it("renders SOLD badge for each item", () => {
    render(<SoldHomes items={ITEMS} />);
    const badges = screen.getAllByText("SOLD");
    expect(badges).toHaveLength(2);
  });

  it("renders empty section with no cards when items is empty", () => {
    render(<SoldHomes items={[]} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
    expect(screen.queryByText("SOLD")).not.toBeInTheDocument();
  });

  it("renders a single item correctly", () => {
    const single: SoldHomeItem[] = [
      { address: "1 Oak Ln", city: "Newark", state: "NJ", price: "$500,000" },
    ];
    render(<SoldHomes items={single} />);
    expect(screen.getByText("$500,000")).toBeInTheDocument();
    expect(screen.getByText("1 Oak Ln, Newark, NJ")).toBeInTheDocument();
    expect(screen.getAllByText("SOLD")).toHaveLength(1);
  });

  it("handles items with special characters in address", () => {
    const special: SoldHomeItem[] = [
      { address: "10 O'Brien Ct", city: "Trenton", state: "NJ", price: "$450,000" },
    ];
    render(<SoldHomes items={special} />);
    expect(screen.getByText("10 O'Brien Ct, Trenton, NJ")).toBeInTheDocument();
  });

  it("renders subtitle paragraph when subtitle is provided", () => {
    render(<SoldHomes items={ITEMS} subtitle="Real results for real people" />);
    expect(screen.getByText("Real results for real people")).toBeInTheDocument();
  });

  it("does not render subtitle paragraph when subtitle is absent", () => {
    render(<SoldHomes items={ITEMS} />);
    expect(screen.queryByText("Real results for real people")).not.toBeInTheDocument();
  });

  it("renders property image when image_url is provided", () => {
    const withImage: SoldHomeItem[] = [
      {
        address: "789 Oak Dr",
        city: "Princeton",
        state: "NJ",
        price: "$900,000",
        image_url: "https://example.com/house.jpg",
      },
    ];
    render(<SoldHomes items={withImage} />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "789 Oak Dr, Princeton");
  });

  it("does not render an image when image_url is absent", () => {
    render(<SoldHomes items={ITEMS} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });
});
