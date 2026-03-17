/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { SoldMinimal } from "@/components/sections/sold/SoldMinimal";

const ITEMS = [
  { address: "123 Main St", city: "Hoboken", state: "NJ", price: "$750,000" },
  { address: "456 Elm Ave", city: "Jersey City", state: "NJ", price: "$620,000" },
];

describe("SoldMinimal", () => {
  it("renders the section heading with default title", () => {
    render(<SoldMinimal items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders all sold home prices", () => {
    render(<SoldMinimal items={ITEMS} />);
    expect(screen.getByText("$750,000")).toBeInTheDocument();
    expect(screen.getByText("$620,000")).toBeInTheDocument();
  });

  it("renders addresses", () => {
    render(<SoldMinimal items={ITEMS} />);
    expect(screen.getByText(/123 Main St/)).toBeInTheDocument();
    expect(screen.getByText(/456 Elm Ave/)).toBeInTheDocument();
  });

  it("has sold section id for anchor linking", () => {
    const { container } = render(<SoldMinimal items={ITEMS} />);
    expect(container.querySelector("#sold")).toBeInTheDocument();
  });

  it("renders custom title", () => {
    render(<SoldMinimal items={ITEMS} title="Recent Closings" />);
    expect(screen.getByRole("heading", { level: 2, name: "Recent Closings" })).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<SoldMinimal items={ITEMS} subtitle="Proven track record" />);
    expect(screen.getByText("Proven track record")).toBeInTheDocument();
  });

  it("renders property images when image_url is provided", () => {
    const itemsWithImages = [
      { ...ITEMS[0], image_url: "/photos/house1.jpg" },
      { ...ITEMS[1] },
    ];
    render(<SoldMinimal items={itemsWithImages} />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "123 Main St, Hoboken");
  });

  it("renders SOLD badge on each listing", () => {
    render(<SoldMinimal items={ITEMS} />);
    const badges = screen.getAllByText("SOLD");
    expect(badges).toHaveLength(ITEMS.length);
  });

  it("does not render subtitle when omitted", () => {
    const { container } = render(<SoldMinimal items={ITEMS} />);
    const section = container.querySelector("#sold");
    const paragraphs = section?.querySelectorAll("p");
    expect(paragraphs?.length ?? 0).toBe(0);
  });
});
