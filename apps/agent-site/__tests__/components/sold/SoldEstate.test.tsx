/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { SoldEstate } from "@/components/sections/sold/SoldEstate";
import type { SoldHomeItem } from "@/features/config/types";

const ITEMS: SoldHomeItem[] = [
  {
    address: "300 Old Dominion Rd",
    city: "Middleburg",
    state: "VA",
    price: "$2,400,000",
    image_url: "https://picsum.photos/id/1015/800/500",
    features: [
      { label: "Acreage", value: "45 acres" },
      { label: "Stables", value: "12 stalls" },
    ],
  },
  {
    address: "18 Fox Chase Ln",
    city: "Upperville",
    state: "VA",
    price: "$1,850,000",
    image_url: "https://picsum.photos/id/1018/800/500",
    badge_label: "SOLD",
  },
  {
    address: "72 Vineyard Hill Dr",
    city: "Bluemont",
    state: "VA",
    price: "$3,500,000",
    image_url: "https://picsum.photos/id/1036/800/500",
  },
];

describe("SoldEstate", () => {
  it("renders the section heading with default title", () => {
    render(<SoldEstate items={ITEMS} />);
    expect(
      screen.getByRole("heading", { level: 2, name: "Properties Sold" })
    ).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<SoldEstate items={ITEMS} title="Estates Sold" />);
    expect(
      screen.getByRole("heading", { level: 2, name: "Estates Sold" })
    ).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<SoldEstate items={ITEMS} subtitle="Premium properties across Virginia." />);
    expect(
      screen.getByText("Premium properties across Virginia.")
    ).toBeInTheDocument();
  });

  it("renders all property prices", () => {
    render(<SoldEstate items={ITEMS} />);
    expect(screen.getByText("$2,400,000")).toBeInTheDocument();
    expect(screen.getByText("$1,850,000")).toBeInTheDocument();
    expect(screen.getByText("$3,500,000")).toBeInTheDocument();
  });

  it("renders all property addresses", () => {
    render(<SoldEstate items={ITEMS} />);
    expect(screen.getByText(/300 Old Dominion Rd/)).toBeInTheDocument();
    expect(screen.getByText(/18 Fox Chase Ln/)).toBeInTheDocument();
    expect(screen.getByText(/72 Vineyard Hill Dr/)).toBeInTheDocument();
  });

  it("renders features as label:value pills when provided", () => {
    render(<SoldEstate items={ITEMS} />);
    expect(screen.getByText(/Acreage/)).toBeInTheDocument();
    expect(screen.getByText(/45 acres/)).toBeInTheDocument();
    expect(screen.getByText(/Stables/)).toBeInTheDocument();
    expect(screen.getByText(/12 stalls/)).toBeInTheDocument();
  });

  it("renders custom badge_label when provided", () => {
    const items: SoldHomeItem[] = [
      {
        address: "1 Estate Way",
        city: "Leesburg",
        state: "VA",
        price: "$900,000",
        badge_label: "JUST SOLD",
      },
    ];
    render(<SoldEstate items={items} />);
    expect(screen.getByText("JUST SOLD")).toBeInTheDocument();
  });

  it("renders SOLD badge when badge_label is not provided", () => {
    render(<SoldEstate items={ITEMS} />);
    // Items without badge_label should show "SOLD"
    const soldBadges = screen.getAllByText("SOLD");
    expect(soldBadges.length).toBeGreaterThanOrEqual(1);
  });

  it("renders section with id=gallery for anchor linking", () => {
    const { container } = render(<SoldEstate items={ITEMS} />);
    expect(container.querySelector("#gallery")).toBeInTheDocument();
  });

  it("renders each property as an article", () => {
    const { container } = render(<SoldEstate items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
  });

  it("renders images with alt text when image_url is provided", () => {
    render(<SoldEstate items={ITEMS} />);
    const images = screen.getAllByRole("img");
    expect(images.length).toBe(3);
    expect(images[0]).toHaveAttribute("alt", "300 Old Dominion Rd, Middleburg");
  });

  it("applies hover lift on mouse enter", () => {
    render(<SoldEstate items={ITEMS} />);
    const card = screen.getAllByRole("article")[0];
    expect(card.style.transform).toBe("none");
    fireEvent.mouseEnter(card);
    expect(card.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(card);
    expect(card.style.transform).toBe("none");
  });
});
