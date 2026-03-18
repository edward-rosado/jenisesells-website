/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { TestimonialsHeart } from "@/components/sections/testimonials/TestimonialsHeart";
import type { TestimonialItem } from "@/lib/types";

const ITEMS: TestimonialItem[] = [
  { text: "They made buying our first home so easy.", reviewer: "The Park Family", rating: 5, source: "Zillow" },
  { text: "So patient, so kind, so knowledgeable.", reviewer: "Sarah & Mark", rating: 5, source: "Google" },
  { text: "Best experience we've ever had with an agent.", reviewer: "James T.", rating: 4 },
];

describe("TestimonialsHeart", () => {
  it("renders the default heading", () => {
    render(<TestimonialsHeart items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "What Our Clients Say" })).toBeInTheDocument();
  });

  it("renders a custom title when provided", () => {
    render(<TestimonialsHeart items={ITEMS} title="Kind Words" />);
    expect(screen.getByRole("heading", { level: 2, name: "Kind Words" })).toBeInTheDocument();
  });

  it("renders all quote texts", () => {
    render(<TestimonialsHeart items={ITEMS} />);
    expect(screen.getByText("They made buying our first home so easy.")).toBeInTheDocument();
    expect(screen.getByText("So patient, so kind, so knowledgeable.")).toBeInTheDocument();
    expect(screen.getByText("Best experience we've ever had with an agent.")).toBeInTheDocument();
  });

  it("renders reviewer attributions", () => {
    render(<TestimonialsHeart items={ITEMS} />);
    expect(screen.getByText(/The Park Family/)).toBeInTheDocument();
    expect(screen.getByText(/Sarah & Mark/)).toBeInTheDocument();
    expect(screen.getByText(/James T\./)).toBeInTheDocument();
  });

  it("renders star ratings with aria labels", () => {
    render(<TestimonialsHeart items={ITEMS} />);
    expect(screen.getAllByLabelText("5 out of 5 stars").length).toBe(2);
    expect(screen.getByLabelText("4 out of 5 stars")).toBeInTheDocument();
  });

  it("uses id=testimonials for anchor linking", () => {
    const { container } = render(<TestimonialsHeart items={ITEMS} />);
    expect(container.querySelector("#testimonials")).toBeInTheDocument();
  });

  it("includes the FTC disclaimer", () => {
    render(<TestimonialsHeart items={ITEMS} />);
    expect(screen.getByText(/No compensation was provided/)).toBeInTheDocument();
  });

  it("renders a decorative quotation mark", () => {
    const { container } = render(<TestimonialsHeart items={ITEMS} />);
    const section = container.querySelector("#testimonials");
    // decorative quote mark is aria-hidden
    const quoteMarks = section?.querySelectorAll('[aria-hidden="true"]');
    expect(quoteMarks && quoteMarks.length).toBeGreaterThan(0);
  });

  it("lifts card on hover", () => {
    const { container } = render(<TestimonialsHeart items={ITEMS} />);
    const card = container.querySelector("article") as HTMLElement;
    expect(card.style.transform).toBe("none");
    fireEvent.mouseEnter(card);
    expect(card.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(card);
    expect(card.style.transform).toBe("none");
  });
});
