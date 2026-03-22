/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { SoldCoastal } from "@/components/sections/sold/SoldCoastal";
import type { SoldHomeItem } from "@/features/config/types";

const ITEMS: SoldHomeItem[] = [
  {
    address: "101 Oceanfront Dr",
    city: "Nags Head",
    state: "NC",
    price: "$1,200,000",
    image_url: "https://picsum.photos/id/1040/400/300",
    tags: ["Oceanfront", "Beach Access"],
  },
  {
    address: "44 Canal Front Way",
    city: "Kill Devil Hills",
    state: "NC",
    price: "$650,000",
    image_url: "https://picsum.photos/id/1041/400/300",
    tags: ["Canal Front", "Boat Dock"],
    badge_label: "SOLD",
  },
  {
    address: "200 Dune Road",
    city: "Duck",
    state: "NC",
    price: "$895,000",
    // no image, no tags
  },
];

describe("SoldCoastal", () => {
  it("renders the default heading 'Recent Sales'", () => {
    render(<SoldCoastal items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "Recent Sales" })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<SoldCoastal items={ITEMS} title="Beach Properties Sold" />);
    expect(screen.getByRole("heading", { level: 2, name: "Beach Properties Sold" })).toBeInTheDocument();
  });

  it("renders all property prices", () => {
    render(<SoldCoastal items={ITEMS} />);
    expect(screen.getByText("$1,200,000")).toBeInTheDocument();
    expect(screen.getByText("$650,000")).toBeInTheDocument();
    expect(screen.getByText("$895,000")).toBeInTheDocument();
  });

  it("renders all property addresses", () => {
    render(<SoldCoastal items={ITEMS} />);
    expect(screen.getByText(/101 Oceanfront Dr/)).toBeInTheDocument();
    expect(screen.getByText(/44 Canal Front Way/)).toBeInTheDocument();
    expect(screen.getByText(/200 Dune Road/)).toBeInTheDocument();
  });

  it("renders tags as teal pill badges when provided", () => {
    render(<SoldCoastal items={ITEMS} />);
    expect(screen.getByText("Oceanfront")).toBeInTheDocument();
    expect(screen.getByText("Beach Access")).toBeInTheDocument();
    expect(screen.getByText("Canal Front")).toBeInTheDocument();
    expect(screen.getByText("Boat Dock")).toBeInTheDocument();
  });

  it("renders teal SOLD badge on each card", () => {
    render(<SoldCoastal items={ITEMS} />);
    const badges = screen.getAllByText(/SOLD/i);
    expect(badges.length).toBeGreaterThanOrEqual(1);
  });

  it("has id=gallery for anchor linking", () => {
    const { container } = render(<SoldCoastal items={ITEMS} />);
    expect(container.querySelector("#gallery")).toBeInTheDocument();
  });

  it("renders images with alt text when image_url provided", () => {
    render(<SoldCoastal items={ITEMS} />);
    const images = screen.getAllByRole("img");
    expect(images.length).toBe(2); // only 2 items have image_url
    expect(images[0]).toHaveAttribute("alt", "101 Oceanfront Dr, Nags Head");
  });

  it("renders each property as an article", () => {
    const { container } = render(<SoldCoastal items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
  });

  it("renders subtitle when provided", () => {
    render(<SoldCoastal items={ITEMS} subtitle="Properties I've sold on the Outer Banks." />);
    expect(screen.getByText("Properties I've sold on the Outer Banks.")).toBeInTheDocument();
  });

  it("tag pills have teal styling", () => {
    render(<SoldCoastal items={ITEMS} />);
    const tag = screen.getByText("Oceanfront");
    expect(tag.tagName.toLowerCase()).toBe("span");
    expect((tag as HTMLElement).style.background).toMatch(/var\(--color-primary|#2c7a7b/);
  });
});
