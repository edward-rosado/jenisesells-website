/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { SoldCards } from "@/components/sections/sold/SoldCards";
import type { SoldHomeItem } from "@/lib/types";

const ITEMS: SoldHomeItem[] = [
  {
    address: "123 Main St",
    city: "Springfield",
    state: "NJ",
    price: "$750,000",
    image_url: "/homes/home1.jpg",
  },
  {
    address: "456 Oak Ave",
    city: "Newark",
    state: "NJ",
    price: "$500,000",
    image_url: "/homes/home2.jpg",
  },
];

describe("SoldCards", () => {
  it("renders the section heading with default title", () => {
    render(<SoldCards items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<SoldCards items={ITEMS} title="Happy Families" />);
    expect(screen.getByRole("heading", { level: 2, name: "Happy Families" })).toBeInTheDocument();
  });

  it("renders all sold home prices", () => {
    render(<SoldCards items={ITEMS} />);
    expect(screen.getByText("$750,000")).toBeInTheDocument();
    expect(screen.getByText("$500,000")).toBeInTheDocument();
  });

  it("renders sold home addresses", () => {
    render(<SoldCards items={ITEMS} />);
    expect(screen.getByText(/123 Main St/)).toBeInTheDocument();
    expect(screen.getByText(/456 Oak Ave/)).toBeInTheDocument();
  });

  it("uses id=sold for anchor linking", () => {
    const { container } = render(<SoldCards items={ITEMS} />);
    expect(container.querySelector("#sold")).toBeInTheDocument();
  });

  it("renders SOLD badges", () => {
    render(<SoldCards items={ITEMS} />);
    const badges = screen.getAllByText("SOLD");
    expect(badges.length).toBe(2);
  });

  it("renders images with alt text", () => {
    render(<SoldCards items={ITEMS} />);
    const images = screen.getAllByRole("img");
    expect(images.length).toBe(2);
    expect(images[0]).toHaveAttribute("alt", "123 Main St");
  });

  it("uses rounded cards with soft shadows", () => {
    const { container } = render(<SoldCards items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(2);
    expect(articles[0].style.borderRadius).toBeTruthy();
    expect(articles[0].style.boxShadow).toBeTruthy();
  });

  it("renders subtitle when provided", () => {
    render(<SoldCards items={ITEMS} subtitle="Homes I have helped sell" />);
    expect(screen.getByText("Homes I have helped sell")).toBeInTheDocument();
  });
});
