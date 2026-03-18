/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { SoldMetrics } from "@/components/sections/sold/SoldMetrics";
import type { SoldHomeItem } from "@/lib/types";

const ITEMS: SoldHomeItem[] = [
  {
    address: "2200 Ross Ave",
    city: "Dallas",
    state: "TX",
    price: "$8,500,000",
    property_type: "Office",
    sq_ft: "45,000 SF",
    cap_rate: "6.2%",
    noi: "$527,000",
    badge_label: "CLOSED",
    image_url: "/sold/office.jpg",
  },
  {
    address: "500 Commerce St",
    city: "Fort Worth",
    state: "TX",
    price: "$3,200,000",
    property_type: "Retail",
    sq_ft: "18,500 SF",
    cap_rate: "5.8%",
    noi: "$185,600",
    badge_label: "CLOSED",
  },
];

const ITEMS_NO_BADGE: SoldHomeItem[] = [
  {
    address: "100 Industrial Pkwy",
    city: "Irving",
    state: "TX",
    price: "$5,100,000",
    property_type: "Industrial",
    sq_ft: "62,000 SF",
  },
];

describe("SoldMetrics", () => {
  it("renders the default section heading", () => {
    render(<SoldMetrics items={ITEMS} />);
    expect(
      screen.getByRole("heading", { level: 2, name: "Recent Transactions" })
    ).toBeInTheDocument();
  });

  it("renders a custom heading when title is provided", () => {
    render(<SoldMetrics items={ITEMS} title="Portfolio Highlights" />);
    expect(
      screen.getByRole("heading", { level: 2, name: "Portfolio Highlights" })
    ).toBeInTheDocument();
  });

  it("renders all prices", () => {
    render(<SoldMetrics items={ITEMS} />);
    expect(screen.getByText("$8,500,000")).toBeInTheDocument();
    expect(screen.getByText("$3,200,000")).toBeInTheDocument();
  });

  it("renders all addresses", () => {
    render(<SoldMetrics items={ITEMS} />);
    expect(screen.getByText(/2200 Ross Ave/)).toBeInTheDocument();
    expect(screen.getByText(/500 Commerce St/)).toBeInTheDocument();
  });

  it("renders property_type badges", () => {
    render(<SoldMetrics items={ITEMS} />);
    expect(screen.getByText("Office")).toBeInTheDocument();
    expect(screen.getByText("Retail")).toBeInTheDocument();
  });

  it("renders sq_ft metrics when available", () => {
    render(<SoldMetrics items={ITEMS} />);
    expect(screen.getByText("45,000 SF")).toBeInTheDocument();
    expect(screen.getByText("18,500 SF")).toBeInTheDocument();
  });

  it("renders cap_rate metrics when available", () => {
    render(<SoldMetrics items={ITEMS} />);
    expect(screen.getByText("6.2%")).toBeInTheDocument();
    expect(screen.getByText("5.8%")).toBeInTheDocument();
  });

  it("renders noi metrics when available", () => {
    render(<SoldMetrics items={ITEMS} />);
    expect(screen.getByText("$527,000")).toBeInTheDocument();
    expect(screen.getByText("$185,600")).toBeInTheDocument();
  });

  it("renders badge_label text on cards", () => {
    render(<SoldMetrics items={ITEMS} />);
    const badges = screen.getAllByText("CLOSED");
    expect(badges.length).toBe(2);
  });

  it("falls back to CLOSED text when badge_label is not provided", () => {
    render(<SoldMetrics items={ITEMS_NO_BADGE} />);
    expect(screen.getByText("CLOSED")).toBeInTheDocument();
  });

  it("uses id=gallery for anchor linking", () => {
    const { container } = render(<SoldMetrics items={ITEMS} />);
    expect(container.querySelector("#gallery")).toBeInTheDocument();
  });

  it("renders article elements for each property", () => {
    const { container } = render(<SoldMetrics items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(2);
  });

  it("renders subtitle when provided", () => {
    render(<SoldMetrics items={ITEMS} subtitle="Selected from 450+ closed transactions." />);
    expect(screen.getByText("Selected from 450+ closed transactions.")).toBeInTheDocument();
  });

  it("applies hover lift on mouse enter", () => {
    render(<SoldMetrics items={ITEMS} />);
    const card = screen.getAllByRole("article")[0];
    expect(card.style.transform).toBe("none");
    fireEvent.mouseEnter(card);
    expect(card.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(card);
    expect(card.style.transform).toBe("none");
  });
});
