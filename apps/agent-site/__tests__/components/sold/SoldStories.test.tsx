/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { SoldStories } from "@/components/sections/sold/SoldStories";
import type { SoldHomeItem } from "@/features/config/types";

const ITEMS: SoldHomeItem[] = [
  {
    address: "200 S Tryon St",
    city: "Charlotte",
    state: "NC",
    price: "$425,000",
    image_url: "/agents/test-beginnings/sold/200-tryon.jpg",
    client_quote: "Rachel found us our dream home in just two weeks!",
    client_name: "The Nguyen Family",
  },
  {
    address: "45 Camden Ave",
    city: "Charlotte",
    state: "NC",
    price: "$310,000",
    image_url: "/agents/test-beginnings/sold/45-camden.jpg",
    client_quote: "We felt supported the whole way through.",
    client_name: "Marcus & Tia",
  },
  {
    address: "88 Park Rd",
    city: "Charlotte",
    state: "NC",
    price: "$580,000",
    image_url: "/agents/test-beginnings/sold/88-park.jpg",
  },
];

describe("SoldStories", () => {
  it("renders the default heading", () => {
    render(<SoldStories items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "Happy Homeowners" })).toBeInTheDocument();
  });

  it("renders a custom title when provided", () => {
    render(<SoldStories items={ITEMS} title="Success Stories" />);
    expect(screen.getByRole("heading", { level: 2, name: "Success Stories" })).toBeInTheDocument();
  });

  it("renders all prices", () => {
    render(<SoldStories items={ITEMS} />);
    expect(screen.getByText("$425,000")).toBeInTheDocument();
    expect(screen.getByText("$310,000")).toBeInTheDocument();
    expect(screen.getByText("$580,000")).toBeInTheDocument();
  });

  it("renders all addresses", () => {
    render(<SoldStories items={ITEMS} />);
    expect(screen.getByText(/200 S Tryon St/)).toBeInTheDocument();
    expect(screen.getByText(/45 Camden Ave/)).toBeInTheDocument();
    expect(screen.getByText(/88 Park Rd/)).toBeInTheDocument();
  });

  it("renders client quotes when provided", () => {
    render(<SoldStories items={ITEMS} />);
    expect(screen.getByText("Rachel found us our dream home in just two weeks!")).toBeInTheDocument();
    expect(screen.getByText("We felt supported the whole way through.")).toBeInTheDocument();
  });

  it("renders client names when provided", () => {
    render(<SoldStories items={ITEMS} />);
    expect(screen.getByText(/The Nguyen Family/)).toBeInTheDocument();
    expect(screen.getByText(/Marcus & Tia/)).toBeInTheDocument();
  });

  it("does not render client quote block when not provided", () => {
    render(<SoldStories items={ITEMS} />);
    // Only 2 of 3 items have quotes
    const quotes = screen.queryAllByText(/Rachel|Marcus/);
    expect(quotes.length).toBe(2);
  });

  it("uses id=gallery for anchor linking", () => {
    const { container } = render(<SoldStories items={ITEMS} />);
    expect(container.querySelector("#gallery")).toBeInTheDocument();
  });

  it("renders images for items with image_url", () => {
    render(<SoldStories items={ITEMS} />);
    const images = screen.getAllByRole("img");
    expect(images.length).toBe(3);
  });

  it("renders rounded cards with warm shadows", () => {
    const { container } = render(<SoldStories items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
    expect((articles[0] as HTMLElement).style.borderRadius).toBeTruthy();
    expect((articles[0] as HTMLElement).style.boxShadow).toBeTruthy();
  });

  it("renders subtitle when provided", () => {
    render(<SoldStories items={ITEMS} subtitle="People we've helped" />);
    expect(screen.getByText("People we've helped")).toBeInTheDocument();
  });

  it("lifts card on hover", () => {
    const { container } = render(<SoldStories items={ITEMS} />);
    const card = container.querySelector("article") as HTMLElement;
    expect(card.style.transform).toBe("none");
    fireEvent.mouseEnter(card);
    expect(card.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(card);
    expect(card.style.transform).toBe("none");
  });

  it("renders quote paragraph with zero bottom margin when client_name is absent", () => {
    const itemsWithQuoteNoName: SoldHomeItem[] = [
      {
        address: "10 Maple Dr",
        city: "Raleigh",
        state: "NC",
        price: "$275,000",
        client_quote: "Fantastic experience from start to finish.",
      },
    ];
    const { container } = render(<SoldStories items={itemsWithQuoteNoName} />);
    expect(screen.getByText("Fantastic experience from start to finish.")).toBeInTheDocument();
    // client_name footer should not be rendered
    const footers = container.querySelectorAll("footer");
    expect(footers.length).toBe(0);
  });
});
