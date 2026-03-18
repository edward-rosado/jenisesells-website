/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { TestimonialsMinimal } from "@/components/sections/testimonials/TestimonialsMinimal";
import type { TestimonialItem } from "@/lib/types";

const ITEMS: TestimonialItem[] = [
  { text: "Victoria helped us find our dream penthouse.", reviewer: "Alexandra P.", rating: 5, source: "Zillow" },
  { text: "Exceptional market knowledge.", reviewer: "James R.", rating: 5 },
  { text: "Truly white-glove experience.", reviewer: "Margaret L.", rating: 4 },
];

describe("TestimonialsMinimal", () => {
  it("renders section with id=testimonials", () => {
    const { container } = render(<TestimonialsMinimal items={ITEMS} />);
    expect(container.querySelector("section#testimonials")).toBeInTheDocument();
  });

  it("renders default heading 'Client Testimonials' when title is not provided", () => {
    render(<TestimonialsMinimal items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "Client Testimonials" })).toBeInTheDocument();
  });

  it("renders custom heading when title is provided", () => {
    render(<TestimonialsMinimal items={ITEMS} title="What Our Clients Say" />);
    expect(screen.getByRole("heading", { level: 2, name: "What Our Clients Say" })).toBeInTheDocument();
  });

  it("renders all testimonial texts", () => {
    render(<TestimonialsMinimal items={ITEMS} />);
    expect(screen.getByText(/Victoria helped us find our dream penthouse/)).toBeInTheDocument();
    expect(screen.getByText(/Exceptional market knowledge/)).toBeInTheDocument();
    expect(screen.getByText(/Truly white-glove experience/)).toBeInTheDocument();
  });

  it("renders all reviewer names", () => {
    render(<TestimonialsMinimal items={ITEMS} />);
    expect(screen.getByText(/Alexandra P\./)).toBeInTheDocument();
    expect(screen.getByText(/James R\./)).toBeInTheDocument();
    expect(screen.getByText(/Margaret L\./)).toBeInTheDocument();
  });

  it("renders star ratings using clampRating", () => {
    render(<TestimonialsMinimal items={ITEMS} />);
    // First item has rating 5 — full stars
    expect(screen.getAllByText("★★★★★")).toHaveLength(2);
    // Third item has rating 4
    expect(screen.getByText("★★★★☆")).toBeInTheDocument();
  });

  it("renders FTC_DISCLAIMER at the bottom", () => {
    render(<TestimonialsMinimal items={ITEMS} />);
    expect(screen.getByText(/unedited excerpts/i)).toBeInTheDocument();
    expect(screen.getByText(/no compensation/i)).toBeInTheDocument();
  });

  it("renders dark background on section", () => {
    const { container } = render(<TestimonialsMinimal items={ITEMS} />);
    const section = container.querySelector("section#testimonials");
    expect(section!.style.background).toContain("color-primary");
  });

  it("renders testimonial text in italic style", () => {
    render(<TestimonialsMinimal items={ITEMS} />);
    const quote = screen.getByText(/Victoria helped us find our dream penthouse/);
    expect(quote.style.fontStyle).toBe("italic");
  });

  it("renders star ratings with accent color", () => {
    render(<TestimonialsMinimal items={ITEMS} />);
    const stars = screen.getAllByRole("img");
    expect(stars[0].style.color).toContain("color-accent");
  });

  it("renders empty section gracefully when items is empty", () => {
    render(<TestimonialsMinimal items={[]} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("clamps rating to 5 for values over 5", () => {
    render(<TestimonialsMinimal items={[{ text: "Test", reviewer: "X", rating: 10 }]} />);
    expect(screen.getByText("★★★★★")).toBeInTheDocument();
  });

  it("applies hover lift on mouse enter", () => {
    render(<TestimonialsMinimal items={ITEMS} />);
    const starEl = screen.getAllByRole("img")[0];
    const card = starEl.parentElement as HTMLElement;
    fireEvent.mouseEnter(card);
    expect(card.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(card);
    expect(card.style.transform).toBe("none");
  });

  it("clamps rating to 0 for negative values", () => {
    render(<TestimonialsMinimal items={[{ text: "Test", reviewer: "X", rating: -1 }]} />);
    expect(screen.getByText("☆☆☆☆☆")).toBeInTheDocument();
  });
});
