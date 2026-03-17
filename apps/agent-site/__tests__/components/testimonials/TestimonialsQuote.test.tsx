/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { TestimonialsQuote } from "@/components/sections/testimonials/TestimonialsQuote";
import type { TestimonialItem } from "@/lib/types";

const ITEMS: TestimonialItem[] = [
  { text: "Isabelle's knowledge of the Fairfield County market is unparalleled.", reviewer: "Catherine M.", rating: 5, source: "Zillow" },
  { text: "A truly refined and gracious experience from start to finish.", reviewer: "James R.", rating: 5 },
  { text: "We couldn't have asked for a more dedicated advocate.", reviewer: "Sophie L.", rating: 5 },
];

describe("TestimonialsQuote", () => {
  it("renders section with id=testimonials", () => {
    const { container } = render(<TestimonialsQuote items={ITEMS} />);
    expect(container.querySelector("section#testimonials")).toBeInTheDocument();
  });

  it("renders default heading 'Testimonials'", () => {
    render(<TestimonialsQuote items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "Testimonials" })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<TestimonialsQuote items={ITEMS} title="Client Stories" />);
    expect(screen.getByRole("heading", { level: 2, name: "Client Stories" })).toBeInTheDocument();
  });

  it("renders all quote texts", () => {
    render(<TestimonialsQuote items={ITEMS} />);
    expect(screen.getByText(/Isabelle's knowledge of the Fairfield County/)).toBeInTheDocument();
    expect(screen.getByText(/A truly refined and gracious experience/)).toBeInTheDocument();
    expect(screen.getByText(/We couldn't have asked for a more dedicated/)).toBeInTheDocument();
  });

  it("renders all reviewer names", () => {
    render(<TestimonialsQuote items={ITEMS} />);
    expect(screen.getByText("Catherine M.")).toBeInTheDocument();
    expect(screen.getByText("James R.")).toBeInTheDocument();
    expect(screen.getByText("Sophie L.")).toBeInTheDocument();
  });

  it("renders FTC disclaimer", () => {
    render(<TestimonialsQuote items={ITEMS} />);
    expect(screen.getByText(/Real reviews from real clients/)).toBeInTheDocument();
  });

  it("renders decorative quotation marks with accent color", () => {
    const { container } = render(<TestimonialsQuote items={ITEMS} />);
    const quotationMarks = container.querySelectorAll("[data-quotation-mark]");
    expect(quotationMarks.length).toBeGreaterThan(0);
    expect((quotationMarks[0] as HTMLElement).style.color).toContain("color-accent");
  });

  it("renders quotes in italic style", () => {
    render(<TestimonialsQuote items={ITEMS} />);
    // Quote text should be in italic paragraph
    const quoteTexts = screen.getAllByRole("paragraph");
    const italicTexts = quoteTexts.filter(el => el.style.fontStyle === "italic");
    expect(italicTexts.length).toBeGreaterThan(0);
  });
});
