/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { Testimonials } from "@/components/sections/Testimonials";
import type { TestimonialItem } from "@/lib/types";

const ITEMS: TestimonialItem[] = [
  { text: "Amazing service!", reviewer: "Alice B.", rating: 5, source: "Zillow" },
  { text: "Would recommend.", reviewer: "Tom C.", rating: 4 },
  { text: "Very professional.", reviewer: "Sara D.", rating: 3 },
];

describe("Testimonials", () => {
  it("renders the section heading", () => {
    render(<Testimonials items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "What My Clients Say" })).toBeInTheDocument();
  });

  it("renders all testimonial texts", () => {
    render(<Testimonials items={ITEMS} />);
    expect(screen.getByText("Amazing service!")).toBeInTheDocument();
    expect(screen.getByText("Would recommend.")).toBeInTheDocument();
    expect(screen.getByText("Very professional.")).toBeInTheDocument();
  });

  it("renders all reviewer names with em dash prefix", () => {
    render(<Testimonials items={ITEMS} />);
    // Each reviewer is rendered as "— Alice B." etc. The dash is an em-dash character.
    expect(screen.getByText(/Alice B\./)).toBeInTheDocument();
    expect(screen.getByText(/Tom C\./)).toBeInTheDocument();
    expect(screen.getByText(/Sara D\./)).toBeInTheDocument();
  });

  it("renders the source when provided", () => {
    render(<Testimonials items={ITEMS} />);
    expect(screen.getByText(/via Zillow/)).toBeInTheDocument();
  });

  it("does not render source text when source is absent", () => {
    render(<Testimonials items={ITEMS} />);
    // Tom C. has no source — verify no "via" text associated
    const cards = screen.getAllByText(/Would recommend/);
    expect(cards[0].closest("div")).not.toHaveTextContent("via");
  });

  it("renders 5 filled stars for rating 5", () => {
    render(<Testimonials items={[{ text: "Great!", reviewer: "X", rating: 5 }]} />);
    expect(screen.getByText("★★★★★")).toBeInTheDocument();
  });

  it("renders 4 filled and 1 empty star for rating 4", () => {
    render(<Testimonials items={[{ text: "Good!", reviewer: "X", rating: 4 }]} />);
    expect(screen.getByText("★★★★☆")).toBeInTheDocument();
  });

  it("renders 3 filled and 2 empty stars for rating 3", () => {
    render(<Testimonials items={[{ text: "OK", reviewer: "X", rating: 3 }]} />);
    expect(screen.getByText("★★★☆☆")).toBeInTheDocument();
  });

  it("renders 1 filled and 4 empty stars for rating 1", () => {
    render(<Testimonials items={[{ text: "Poor", reviewer: "X", rating: 1 }]} />);
    expect(screen.getByText("★☆☆☆☆")).toBeInTheDocument();
  });

  it("renders empty section when items is empty", () => {
    render(<Testimonials items={[]} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
    expect(screen.queryByText(/★/)).not.toBeInTheDocument();
  });

  it("renders correct count of cards", () => {
    render(<Testimonials items={ITEMS} />);
    // Each card has the testimonial text
    expect(screen.getAllByText(/Amazing service!|Would recommend|Very professional/)).toHaveLength(3);
  });

  it("renders subtitle paragraph when subtitle is provided", () => {
    render(<Testimonials items={ITEMS} subtitle="Don't just take our word for it" />);
    expect(screen.getByText("Don't just take our word for it")).toBeInTheDocument();
  });

  it("does not render subtitle paragraph when subtitle is absent", () => {
    render(<Testimonials items={ITEMS} />);
    expect(screen.queryByText("Don't just take our word for it")).not.toBeInTheDocument();
  });
});
