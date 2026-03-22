/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { TestimonialsRustic } from "@/components/sections/testimonials/TestimonialsRustic";
import { FTC_DISCLAIMER } from "@/components/sections/types";
import type { TestimonialItem } from "@/features/config/types";

const ITEMS: TestimonialItem[] = [
  {
    text: "James found us the perfect equestrian estate. His knowledge of the Loudoun County market is unmatched.",
    reviewer: "The Reynolds Family",
    rating: 5,
    source: "Zillow",
  },
  {
    text: "We sold our farm in under 30 days at full asking price. James delivered.",
    reviewer: "Margaret & Tom H.",
    rating: 5,
  },
  {
    text: "Professional, patient, and deeply knowledgeable about rural properties.",
    reviewer: "Dr. Charles F.",
    rating: 4,
    source: "Google",
  },
];

describe("TestimonialsRustic", () => {
  it("renders the section heading with default title", () => {
    render(<TestimonialsRustic items={ITEMS} />);
    expect(
      screen.getByRole("heading", { level: 2, name: "What Our Clients Say" })
    ).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<TestimonialsRustic items={ITEMS} title="Client Stories" />);
    expect(
      screen.getByRole("heading", { level: 2, name: "Client Stories" })
    ).toBeInTheDocument();
  });

  it("renders all review text", () => {
    render(<TestimonialsRustic items={ITEMS} />);
    expect(
      screen.getByText(
        "James found us the perfect equestrian estate. His knowledge of the Loudoun County market is unmatched."
      )
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "We sold our farm in under 30 days at full asking price. James delivered."
      )
    ).toBeInTheDocument();
  });

  it("renders all reviewer names", () => {
    render(<TestimonialsRustic items={ITEMS} />);
    expect(screen.getByText(/The Reynolds Family/)).toBeInTheDocument();
    expect(screen.getByText(/Margaret & Tom H\./)).toBeInTheDocument();
    expect(screen.getByText(/Dr\. Charles F\./)).toBeInTheDocument();
  });

  it("renders star ratings", () => {
    render(<TestimonialsRustic items={ITEMS} />);
    // Should render star rating aria-labels
    const stars = screen.getAllByRole("img");
    expect(stars.length).toBeGreaterThanOrEqual(3);
    expect(stars[0]).toHaveAttribute("aria-label", "5 out of 5 stars");
  });

  it("renders section with id=testimonials", () => {
    const { container } = render(<TestimonialsRustic items={ITEMS} />);
    expect(container.querySelector("#testimonials")).toBeInTheDocument();
  });

  it("renders FTC disclaimer", () => {
    render(<TestimonialsRustic items={ITEMS} />);
    expect(screen.getByText(FTC_DISCLAIMER)).toBeInTheDocument();
  });

  it("renders warm cream card background", () => {
    const { container } = render(<TestimonialsRustic items={ITEMS} />);
    const section = container.querySelector("section");
    // Section or cards should use warm cream color
    expect(section?.style.background).toBeTruthy();
  });

  it("renders subtle border on cards", () => {
    const { container } = render(<TestimonialsRustic items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
    expect(articles[0].style.border).toContain("1px solid");
  });

  it("applies hover lift on mouse enter", () => {
    const { container } = render(<TestimonialsRustic items={ITEMS} />);
    const card = container.querySelectorAll("article")[0] as HTMLElement;
    fireEvent.mouseEnter(card);
    expect(card.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(card);
    expect(card.style.transform).toBe("none");
  });

  it("renders clamped rating — 6 becomes 5 stars", () => {
    const items: TestimonialItem[] = [
      { text: "Great!", reviewer: "Alice", rating: 6 },
    ];
    render(<TestimonialsRustic items={items} />);
    const star = screen.getByRole("img");
    expect(star).toHaveAttribute("aria-label", "5 out of 5 stars");
  });
});
