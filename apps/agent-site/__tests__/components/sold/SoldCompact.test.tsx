/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { SoldCompact } from "@/components/sections/sold/SoldCompact";
import type { SoldHomeItem } from "@/lib/types";

const ITEMS: SoldHomeItem[] = [
  { address: "100 N 3rd St", city: "Brooklyn", state: "NY", price: "$850,000", image_url: "/sold/100-n3rd.jpg" },
  { address: "88 Berry St", city: "Williamsburg", state: "NY", price: "$720,000", image_url: "/sold/88-berry.jpg" },
  { address: "200 Kent Ave", city: "Brooklyn", state: "NY", price: "$640,000" },
];

describe("SoldCompact", () => {
  it("renders section with id=gallery", () => {
    const { container } = render(<SoldCompact items={ITEMS} />);
    expect(container.querySelector("section#gallery")).toBeInTheDocument();
  });

  it("renders the default heading when no title provided", () => {
    render(<SoldCompact items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Recent Sales");
  });

  it("renders custom title when provided", () => {
    render(<SoldCompact items={ITEMS} title="Just Sold" />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Just Sold");
  });

  it("renders all prices", () => {
    render(<SoldCompact items={ITEMS} />);
    expect(screen.getByText("$850,000")).toBeInTheDocument();
    expect(screen.getByText("$720,000")).toBeInTheDocument();
    expect(screen.getByText("$640,000")).toBeInTheDocument();
  });

  it("renders all addresses", () => {
    render(<SoldCompact items={ITEMS} />);
    expect(screen.getByText("100 N 3rd St")).toBeInTheDocument();
    expect(screen.getByText("88 Berry St")).toBeInTheDocument();
    expect(screen.getByText("200 Kent Ave")).toBeInTheDocument();
  });

  it("renders city and state", () => {
    render(<SoldCompact items={ITEMS} />);
    const brooklynItems = screen.getAllByText("Brooklyn, NY");
    expect(brooklynItems.length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("Williamsburg, NY")).toBeInTheDocument();
  });

  it("renders images when image_url is provided", () => {
    render(<SoldCompact items={ITEMS} />);
    const images = screen.getAllByRole("img");
    expect(images.length).toBe(2); // only items with image_url
  });

  it("uses CSS grid layout", () => {
    const { container } = render(<SoldCompact items={ITEMS} />);
    const grid = container.querySelector("[data-sold-grid]") as HTMLElement;
    expect(grid).toBeInTheDocument();
    expect(grid.style.display).toBe("grid");
  });

  it("renders empty section gracefully when items is empty", () => {
    const { container } = render(<SoldCompact items={[]} />);
    expect(container.querySelector("section#gallery")).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<SoldCompact items={ITEMS} subtitle="Our top deals this year" />);
    expect(screen.getByText("Our top deals this year")).toBeInTheDocument();
  });

  it("does not render subtitle element when subtitle is absent", () => {
    render(<SoldCompact items={ITEMS} />);
    expect(screen.queryByText("Our top deals this year")).not.toBeInTheDocument();
  });
});
