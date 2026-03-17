/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { SoldElegant } from "@/components/sections/sold/SoldElegant";
import type { SoldHomeItem } from "@/lib/types";

const ITEMS: SoldHomeItem[] = [
  { address: "100 Greenwich Ave", city: "Greenwich", state: "CT", price: "$5,500,000", image_url: "/sold/100-greenwich.jpg" },
  { address: "45 Elm Street", city: "Darien", state: "CT", price: "$3,200,000", image_url: "/sold/45-elm.jpg" },
  { address: "200 Main Street", city: "New Canaan", state: "CT", price: "$2,800,000" },
  { address: "88 Post Road", city: "Westport", state: "CT", price: "$1,200,000" },
];

describe("SoldElegant", () => {
  it("renders section with id=sold", () => {
    const { container } = render(<SoldElegant items={ITEMS} />);
    expect(container.querySelector("section#sold")).toBeInTheDocument();
  });

  it("renders default heading 'Recent Sales'", () => {
    render(<SoldElegant items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "Recent Sales" })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<SoldElegant items={ITEMS} title="Portfolio" />);
    expect(screen.getByRole("heading", { level: 2, name: "Portfolio" })).toBeInTheDocument();
  });

  it("renders all prices", () => {
    render(<SoldElegant items={ITEMS} />);
    expect(screen.getByText("$5,500,000")).toBeInTheDocument();
    expect(screen.getByText("$3,200,000")).toBeInTheDocument();
    expect(screen.getByText("$2,800,000")).toBeInTheDocument();
    expect(screen.getByText("$1,200,000")).toBeInTheDocument();
  });

  it("renders all addresses", () => {
    render(<SoldElegant items={ITEMS} />);
    expect(screen.getByText(/100 Greenwich Ave/)).toBeInTheDocument();
    expect(screen.getByText(/45 Elm Street/)).toBeInTheDocument();
  });

  it("renders images for items with image_url", () => {
    render(<SoldElegant items={ITEMS} />);
    const images = screen.getAllByRole("img");
    expect(images.length).toBe(2);
  });

  it("renders image containers with thin accent border", () => {
    const { container } = render(<SoldElegant items={ITEMS} />);
    const imageWrappers = container.querySelectorAll("[data-image-wrapper]");
    expect(imageWrappers.length).toBe(2);
    expect((imageWrappers[0] as HTMLElement).style.border).toContain("color-accent");
  });

  it("renders a grid layout (2 columns)", () => {
    const { container } = render(<SoldElegant items={ITEMS} />);
    const grid = container.querySelector("[data-sold-grid]");
    expect(grid).toBeInTheDocument();
    expect((grid as HTMLElement).style.display).toBe("grid");
  });

  it("renders subtitle when provided", () => {
    render(<SoldElegant items={ITEMS} subtitle="Exceptional results." />);
    expect(screen.getByText("Exceptional results.")).toBeInTheDocument();
  });
});
